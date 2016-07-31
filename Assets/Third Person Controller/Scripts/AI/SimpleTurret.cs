using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A stationary turret that will fire a projectile at any object in the target layer. Uses a spherical trigger to determine any targets within range.
    /// </summary>
    public class SimpleTurret : MonoBehaviour
    {
        [Tooltip("The object that rotates on the turret")]
        [SerializeField] protected GameObject m_TurretHead;
        [Tooltip("The speed at which the head rotates")]
        [SerializeField] protected float m_RotationSpeed = 5.0F;
        [Tooltip("The point at which to do the actual firing")]
        [SerializeField] protected Transform m_FirePoint;
        [Tooltip("The projectile that is actually fired")]
        [SerializeField] protected GameObject m_Projectile;
        [Tooltip("The distance in which the turret can start firing its projectile")]
        [SerializeField] protected float m_FireRange = 20.0f;
        [Tooltip("The number of shots per second")]
        [SerializeField] protected float m_FireRate = 10.0f;

        [Tooltip("Optionally specify a muzzle flash that should appear when the weapon is fired")]
        [SerializeField] protected GameObject m_MuzzleFlash;
        [Tooltip("If Muzzle Flash is specified, the location is the position and rotation that the muzzle flash spawns at")]
        [SerializeField] protected Transform m_MuzzleFlashLocation;

        [Tooltip("Optionally specify any smoke that should appear when the weapon is fired")]
        [SerializeField] protected GameObject m_Smoke;
        [Tooltip("If Smoke is specified, the location is the position and rotation that the smoke spawns at")]
        [SerializeField] protected Transform m_SmokeLocation;

        [Tooltip("Optionally specify a sound that should play when the weapon is fired")]
        [SerializeField] protected AudioClip m_FireSound;
        [Tooltip("If Fire Sound is specified, play the sound after the specified delay")]
        [SerializeField] protected float m_FireSoundDelay;

        [Tooltip("The layers in which the turret should target")]
        [SerializeField] protected LayerMask m_TargetLayers;
        [Tooltip("Start firing when the difference between the turret and target's y position is less than this threshold")]
        [SerializeField] protected float m_TargetYDifference = 4;

        // Internal variables
        private float m_FireDelay;
        private float m_LastFireTime;
        private float m_SqrFireRange;

        // Component references
        private GameObject m_GameObject;
        private Transform m_Transform;
        private AudioSource m_AudioSource;
        private Collider[] m_Colliders;
        private Transform m_TargetTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = m_TurretHead != null ? m_TurretHead.transform : transform;
            m_AudioSource = GetComponent<AudioSource>();
            m_Colliders = GetComponentsInChildren<Collider>();
            
            m_FireDelay = 1.0f / m_FireRate;
            m_LastFireTime = -m_FireDelay;
            m_SqrFireRange = m_FireRange * m_FireRange;

            enabled = false;
        }

        /// <summary>
        /// Register for any events that the turret should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Unregister for any events that the turret was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// A collider has entered the trigger. Start targeting it if its layer is within the target layers.
        /// </summary>
        /// <param name="other">The potential target.</param>
        private void OnTriggerEnter(Collider other)
        {
            if (m_TargetTransform == null && Utility.InLayerMask(other.gameObject.layer, m_TargetLayers.value)) {
                m_TargetTransform = other.transform;
                enabled = true;

                // Stop targeting if the target dies.
                EventHandler.RegisterEvent(m_TargetTransform.gameObject, "OnDeath", OnTargetDeath);
            }
        }

        /// <summary>
        /// The target has potentially left the trigger. If it has then stop targeting the target.
        /// </summary>
        /// <param name="other">The potential target.</param>
        private void OnTriggerExit(Collider other)
        {
            if (m_TargetTransform != null && other.transform == m_TargetTransform) {
                StopTargeting();
                enabled = false;
            }
        }

        /// <summary>
        /// Check to see if a target is within range for an attack and rotate towards the target.
        /// </summary>
        private void Update()
        {
            if (m_TargetTransform == null) {
                return;
            }

            CheckForAttack();
            RotateTowardsTarget();
        }

        /// <summary>
        /// Keep facing the target so the turret can fire at any time.
        /// </summary>
        public void RotateTowardsTarget()
        {
            var targetRotation = Quaternion.Euler(0, Quaternion.LookRotation(m_Transform.position - m_TargetTransform.position).eulerAngles.y, 0);
            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, targetRotation, Time.deltaTime * m_RotationSpeed);
        }

        /// <summary>
        /// Determine if the turret can attack. The turret can attack if it hasn't fired recently and the target is in front of the turret.
        /// </summary>
        public void CheckForAttack()
        {
            if (m_LastFireTime + m_FireDelay < Time.time && Mathf.Abs(m_Transform.position.y - m_TargetTransform.position.y) < m_TargetYDifference &&
                                (m_Transform.position - m_TargetTransform.position).sqrMagnitude < m_SqrFireRange) {
                DoFire();
            }
        }

        /// <summary>
        /// Do the actual fire.
        /// </summary>
        public void DoFire()
        {
            m_LastFireTime = Time.time;

            // Spawn a projectile which will move in the direction that the turret is facing
            var projectile = ObjectPool.Instantiate(m_Projectile, m_FirePoint.position, m_Transform.rotation).GetComponent<Projectile>();
            projectile.Initialize(-m_Transform.forward, Vector3.zero, m_GameObject);
            var projectileCollider = projectile.GetComponent<Collider>();

            // Ignore all of the turret's colliders to prevent the projectile from detonating as a result of the turret. 
            if (projectileCollider != null) {
                for (int i = 0; i < m_Colliders.Length; ++i) {
                    LayerManager.IgnoreCollision(projectileCollider, m_Colliders[i]);
                }
            }

            // Spawn a muzzle flash.
            if (m_MuzzleFlash) {
                ObjectPool.Instantiate(m_MuzzleFlash, m_MuzzleFlashLocation.position, m_MuzzleFlashLocation.rotation, m_Transform);
            }

            // Spawn any smoke.
            if (m_Smoke) {
                ObjectPool.Instantiate(m_Smoke, m_SmokeLocation.position, m_SmokeLocation.rotation);
            }

            // Play a firing sound.
            if (m_FireSound) {
                m_AudioSource.clip = m_FireSound;
                if (m_FireSoundDelay > 0) {
                    m_AudioSource.PlayDelayed(m_FireSoundDelay);
                } else {
                    m_AudioSource.Play();
                }
            }
        }

        /// <summary>
        /// Stop targeting if the target has died, the turret has been destroyed, or the target is out of range.
        /// </summary>
        private void StopTargeting()
        {
            EventHandler.UnregisterEvent(m_TargetTransform.gameObject, "OnDeath", OnTargetDeath);
            m_TargetTransform = null;
        }

        /// <summary>
        /// Stop targeting if the target dies.
        /// </summary>
        private void OnTargetDeath()
        {
            StopTargeting();
        }

        /// <summary>
        /// Stop targeting if the turret is destroyed.
        /// </summary>
        private void OnDeath()
        {
            StopTargeting();
        }
    }
}