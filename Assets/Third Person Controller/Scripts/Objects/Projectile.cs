using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A moving Destructable that applies a damage at the collision point.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : Destructable
    {
        [Tooltip("The initial speed of the projectile")]
        [SerializeField] protected float m_InitialSpeed;
        [Tooltip("How quickly the projectile should move")]
        [SerializeField] protected float m_Speed = 5;
        [Tooltip("The length of time the projectile should exist before it activates if no collision occurs")]
        [SerializeField] protected float m_Lifespan = 10;
        [Tooltip("Should the projectile be destroy when it collides with another object?")]
        [SerializeField] protected bool m_DestroyOnCollision = true;

        // Internal variables
        private ScheduledEvent m_ScheduledActivation;
        private Vector3 m_MovementForce;

        // Component references
        private Rigidbody m_Rigidbody;
        private TrailRenderer m_TrailRenderer;
        private GameObject m_Originator;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
            m_TrailRenderer = GetComponent<TrailRenderer>();
        }

        /// <summary>
        /// Initializes the projectile properties. This will be called from an object creating the projectile (such as a weapon).
        /// </summary>
        /// <param name="direction">The direction to move.</param>
        /// <param name="torque">The torque to apply.</param>
        /// <param name="originator">The originator of the projectile.</param>
        public void Initialize(Vector3 direction, Vector3 torque, GameObject originator)
        {
            enabled = true;
            m_Originator = originator;

            m_MovementForce = direction * m_Speed;

            // Don't add any forces if not on the server. The server will move the object.
#if ENABLE_MULTIPLAYER
            if (!isServer) {
                return;
            }
#endif
            // The projectile may be waiting for initialization.
            m_Rigidbody.isKinematic = false;
            if (m_Collider != null) {
                m_Collider.enabled = true;
            }

            if (m_InitialSpeed != 0) {
                m_Rigidbody.AddForce(m_InitialSpeed * direction, ForceMode.VelocityChange);
            }

            m_Rigidbody.AddRelativeTorque(torque);
        }

        /// <summary>
        /// Enables the TrailRenderer and schedules the projectile's activation if it isn't activated beforehand.
        /// </summary>
        private void OnEnable()
        {
            // Reset the TrailRenderer time if is a negative value. This is done to prevent the trail from being rendered when the object pool changes the position of the projectile.
            if (m_TrailRenderer && m_TrailRenderer.time < 0) {
                Scheduler.Schedule(0.001f, ResetTrails);
            }

            // The projectile can activate after it comes in contact with another object or after a specified amount of time. Do the scheduling here to allow
            // it to activate after a set amount of time.
            m_ScheduledActivation = Scheduler.Schedule(m_Lifespan, LifespanElapsed);
        }

        /// <summary>
        /// The projectile has been spawned but it shouldn't start to move yet. Disable anything that can interfere with the physics.
        /// </summary>
        public void WaitForInitialization()
        {
            enabled = false;
            m_Rigidbody.isKinematic = true;
            if (m_Collider != null) {
                m_Collider.enabled = false;
            }
        }

        /// <summary>
        /// When the TrailRenderer is pooled the trail can still be seen when it is switching positions. At this point the time has been set to a negative value and has waited
        /// a frame. By doing this the trail will not render when switching positions.
        /// </summary>
        private void ResetTrails()
        {
            m_TrailRenderer.time = -m_TrailRenderer.time;
        }

        /// <summary>
        /// Cancel the scheduled activation if the timer isn't what caused the projectile to deactivate, and disable the TrailRenderer.
        /// </summary>
        private void OnDisable()
        {
            Scheduler.Cancel(ref m_ScheduledActivation);

            // Set the TrailRenderer time to a negative value to prevent a trail from being added when the object pool changes the position of the projectile.
            if (m_TrailRenderer) {
                m_TrailRenderer.time = -m_TrailRenderer.time;
            }
        }

        /// <summary>
        /// Continuosuly apply a constant force if supplied.
        /// </summary>
        protected virtual void FixedUpdate()
        {
            // Don't add any forces if not on the server. The server will move the object.
#if ENABLE_MULTIPLAYER
            if (!isServer) {
                return;
            }
#endif
            if (m_Speed != 0) {
                m_Rigidbody.AddForce(m_MovementForce - m_Rigidbody.velocity, ForceMode.VelocityChange);
            }
        }

        /// <summary>
        /// The projectile has collided. Apply damage to the object hit and spawn various effects (explosion, decal, etc).
        /// </summary>
        /// <param name="originator">The originator of the projectile.</param>
        /// <param name="collisionTransform">The collision object that the destructable collided with. Can be null.</param>
        /// <param name="collisionPoint">The point of destruction.</param>
        /// <param name="collisionNormal">The normal at the destruction point.</param>
        /// <param name="destroy">Should the projectile be destroyed and placed back in the ObjectPool?</param>
        protected override void Collide(GameObject originator, Transform collisionTransform, Vector3 collisionPoint, Vector3 collisionNormal, bool destroy)
        {
            base.Collide(originator, collisionTransform, collisionPoint, collisionNormal, destroy);

            if (!destroy) {
                // If the projectile isn't being destroyed then set the parent, make it kinematic, and deactivate the collider to prevent it from interfering with other objects.
                m_Transform.parent = collisionTransform;
                m_Rigidbody.isKinematic = true;
                m_Collider.enabled = false;
                Scheduler.Cancel(ref m_ScheduledActivation);
            }
        }

        /// <summary>
        /// The projectile did not come into contact with any object and the lifespan has elapsed so destroy the projectile now.
        /// </summary>
        private void LifespanElapsed()
        {
#if ENABLE_MULTIPLAYER
            if (!isServer) {
                return;
            }
#endif
            Collide(m_Originator, null, m_Transform.position, m_Transform.up, true);
        }

        /// <summary>
        /// The projectile collided with an object. Destroy itself.
        /// </summary>
        /// <param name="other">The object that the projectile collided with.</param>
        public void OnTriggerEnter(Collider other)
        {
            // OnTriggerEnter sometimes gets called multiple times in a single frame so explicitly enable and disable the projectile.
            // If the projectile is already disabled then it doesn't need to schedule another activation.
            if (!enabled || other.isTrigger) {
                return;
            }

#if ENABLE_MULTIPLAYER
            if (!isServer) {
                return;
            }
#endif
            RaycastHit raycastHit;
            var distance = Vector3.Distance(m_Transform.position, other.transform.position);
            var direction = m_Rigidbody.velocity.normalized;
            // OnTriggerEnter doesn't supply position or normal information. Get that information with a raycast.
            if (Physics.Raycast(m_Transform.position - distance * direction, direction, out raycastHit, distance * 2, 1 << other.gameObject.layer)) {
                Collide(m_Originator, other.transform, raycastHit.point, raycastHit.normal, m_DestroyOnCollision);
            } else {
                Collide(m_Originator, other.transform, m_Transform.position, -m_Rigidbody.velocity.normalized, m_DestroyOnCollision);
            }
        }
    }
}