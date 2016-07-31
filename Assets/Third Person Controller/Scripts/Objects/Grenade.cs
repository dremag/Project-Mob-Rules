using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A thrown object that will destroy itself after a set amount of time.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grenade : Destructable, IThrownObject
    {
        [Tooltip("The length of time the grenade should exist before it destroys itself")]
        [SerializeField] protected float m_Lifespan;

        // Component references
        private Rigidbody m_Rigidbody;
        private GameObject m_Originator;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Throws the grenade. This will be called from an object creating the grenade.
        /// </summary>
        /// <param name="force">The force to apply.</param>
        /// <param name="torque">The torque to apply.</param>
        /// <param name="originator">The originator of the grenade.</param>
        public void ApplyThrowForce(GameObject originator, Vector3 force, Vector3 torque)
        {
            m_Originator = originator;

            m_Rigidbody.AddRelativeForce(force, ForceMode.VelocityChange);

            m_Rigidbody.AddRelativeTorque(torque);

            Scheduler.Schedule(m_Lifespan, LifespanElapsed);
        }

        /// <summary>
        /// The grenade has outlived its lifespan and should now destroy itself.
        /// </summary>
        private void LifespanElapsed()
        {
            Collide(m_Originator, null, m_Transform.position, m_Transform.up, true);
        }
    }
}