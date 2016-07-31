using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Creates an explision which applies a force and damage to any object that is within the specified radius.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        [Tooltip("Should the explosion explode when the object is enabled?")]
        [SerializeField] protected bool m_ExplodeOnEnable;
        [Tooltip("The duration of the explosion")]
        [SerializeField] protected float m_Lifespan;
        [Tooltip("How far out the explosion affects other objects")]
        [SerializeField] protected float m_Radius;
        [Tooltip("The amount of force the explosion applies to other Rigidbody objects")]
        [SerializeField] protected float m_ImpactForce;
        [Tooltip("Optionally specify an event to send to the object hit on damage")]
        [SerializeField] protected string m_DamageEvent;
        [Tooltip("The amount of damage the explosion applies to other objects with the Health component")]
        [SerializeField] protected float m_DamageAmount;
        [Tooltip("Sound to play during the explosion")]
        [SerializeField] protected AudioClip m_Sound;

        // Internal variables
        private ScheduledEvent m_ExplisionEvent;
        private HashSet<GameObject> m_GameObjectExplosions = new HashSet<GameObject>();
        private Collider[] m_HitColliders;
        private GameObject m_Originator;
      
        // Exposed properties
        public GameObject Originator { set { m_Originator = value; } }

        // Component references
        private Transform m_Transform;
        private AudioSource m_AudioSource;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_AudioSource = GetComponent<AudioSource>();
#if !(UNITY_5_0 || UNITY_5_1 || UNITY_5_2)
            m_HitColliders = new Collider[10];
#endif
        }

        /// <summary>
        /// Schedule an explosion if it should explode when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            if (m_ExplodeOnEnable) {
                m_ExplisionEvent = Scheduler.Schedule(0.1f, DoExplosion);
            }
        }

        /// <summary>
        /// Do the explosion.
        /// </summary>
        private void DoExplosion()
        {
            Explode(m_Originator);
        }

        /// <summary>
        /// Do the explosion.
        /// </summary>
        /// <param name="originator">The originator of the object.</param>
        public void Explode(GameObject originator)
        {
            // Cancel the explosion event if it exists.
            Scheduler.Cancel(ref m_ExplisionEvent);

            // Loop through all of the nearby colliders and apply an explosion force and damage.
            Rigidbody colliderRigidbody = null;
            Health health = null;
#if UNITY_5_0 || UNITY_5_1 || UNITY_5_2
            m_HitColliders = Physics.OverlapSphere(m_Transform.position, m_Radius);
            var hitCount = m_HitColliders.Length;
#else
            var hitCount = Physics.OverlapSphereNonAlloc(m_Transform.position, m_Radius, m_HitColliders);
#endif
            for (int i = 0; i < hitCount; ++i) {
                // A GameObject can contain multiple colliders. Prevent the explosion from occurring on the same GameObject multiple times.
                if (m_GameObjectExplosions.Contains(m_HitColliders[i].gameObject)) {
                    continue;
                }
                m_GameObjectExplosions.Add(m_HitColliders[i].gameObject);

                // If the Health component exists it will apply an explosive force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
                if ((health = m_HitColliders[i].transform.GetComponentInParent<Health>()) != null) {
                    // The further out the collider is, the less it is damaged.
                    var direction = m_Transform.position - m_HitColliders[i].transform.position;
                    var damageModifier = Mathf.Max(1 - (direction.magnitude / m_Radius), 0.01f);
                    health.Damage(m_DamageAmount * damageModifier, m_Transform.position, direction.normalized * -m_ImpactForce * damageModifier, m_Radius, originator);
                } else if ((colliderRigidbody = m_HitColliders[i].GetComponent<Rigidbody>()) != null) {
                    colliderRigidbody.AddExplosionForce(m_ImpactForce, m_Transform.position, m_Radius);
                }

                // Execute any custom events.
                if (!string.IsNullOrEmpty(m_DamageEvent)) {
                    EventHandler.ExecuteEvent(m_HitColliders[i].gameObject, m_DamageEvent, m_DamageAmount, m_Transform.position, m_Transform.forward * -m_ImpactForce);
                }
            }
            m_GameObjectExplosions.Clear();

            // Boom.
            if (m_Sound != null) {
                m_AudioSource.clip = m_Sound;
                m_AudioSource.Play();
            }

            Scheduler.Schedule(m_Lifespan, Destroy);
        }

        /// <summary>
        /// Place ourselves back in the ObjectPool.
        /// </summary>
        private void Destroy()
        {
            ObjectPool.Destroy(gameObject);
        }
    }
}