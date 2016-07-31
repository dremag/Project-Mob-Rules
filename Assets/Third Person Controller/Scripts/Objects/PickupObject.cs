using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Base class for any object that can be picked up. This class stores a separate active value so the GameObject active state will be persisted to the clients over a network.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public abstract class PickupObject : NetworkBehaviour
#else
    public abstract class PickupObject : MonoBehaviour
#endif
    {
        [Tooltip("The sound to play when the object is picked up")]
        [SerializeField] protected AudioClip m_PickupSound;
        [Tooltip("The amount of time to enable the trigger after the rigidbody has stopped moving")]
        [SerializeField] protected float m_TriggerEnableDelay;
        [Tooltip("Should the item be picked up when the character enters the trigger?")]
        [SerializeField] protected bool m_PickupOnTriggerEnter = true;

        // Internal variables
#if ENABLE_MULTIPLAYER
        [SyncVar(hook = "SetDepleted")]
#endif
        private bool m_IsDepleted = false;
        private ScheduledEvent m_TriggerEnableEvent;

        // Exposed properties
        protected bool IsDepleted { get { return m_IsDepleted; } }
        public bool PickupOnTriggerEnter { get { return m_PickupOnTriggerEnter; } }

        // Component references
        private GameObject m_GameObject;
        private Renderer m_Renderer;
        private AudioSource m_AudioSource;
        private Rigidbody m_Rigidbody;
        private Collider m_Collider;
        private Collider m_Trigger;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Renderer = GetComponent<Renderer>();
            if (m_PickupSound != null) {
                m_AudioSource = GetComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
            }
            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody != null) {
                // Get a reference to the trigger and non-trigger collider. The collider will be disabled when the rigidbody has stopped moving. The trigger will be enabled
                // when the rigidbody has stopped moving.
                var colliders = GetComponents<Collider>();
                for (int i = 0; i < colliders.Length; ++i) {
                    if (colliders[i].isTrigger) {
                        m_Trigger = colliders[i];
                    } else {
                        m_Collider = colliders[i];
                    }
                }
            }

            m_IsDepleted = false;
        }

        /// <summary>
        /// Start checking the velocity if a rigidbody is attached.
        /// </summary>
        private void Start()
        {
            if (m_Rigidbody != null) {
                Scheduler.Schedule(0.2f, CheckVelocity);
            }
        }

        /// <summary>
        /// The object has been enabled. Set IsDepleted to false and initialize the rigidbody.
        /// </summary>
        private void OnEnable()
        {
            m_IsDepleted = false;
            m_Renderer.enabled = enabled;
            if (m_Rigidbody != null) {
                m_Rigidbody.isKinematic = false;
                m_Collider.enabled = true;
                if (m_TriggerEnableEvent != null) {
                    Scheduler.Cancel(ref m_TriggerEnableEvent);
                }
                m_Trigger.enabled = false;
            }
        }

        /// <summary>
        /// Enables the trigger.
        /// </summary>
        private void EnableTrigger()
        {
            m_Trigger.enabled = true;
            m_TriggerEnableEvent = null;
        }

        /// <summary>
        /// The object has been disabled. Set IsDepleted to true.
        /// </summary>
        private void OnDisable()
        {
            m_IsDepleted = true;
        }

        /// <summary>
        /// Disable the component when the rigidbody has settled.
        /// </summary>
        private void CheckVelocity()
        {
            if (m_Rigidbody.velocity.sqrMagnitude < 0.01f) {
                m_Rigidbody.isKinematic = true;
                m_Collider.enabled = false;
                m_TriggerEnableEvent = Scheduler.Schedule(m_TriggerEnableDelay, EnableTrigger);
                return;
            }
            Scheduler.Schedule(0.2f, CheckVelocity);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Activate or deactivate the object.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            // Immediately activate or deactive the GameObject based on the depletion value.
            m_GameObject.SetActive(!m_IsDepleted);
        }

#endif
        /// <summary>
        /// The object has been picked up. Add any effects.
        /// </summary>
        protected void ObjectPickup()
        {
            // Add any pickup effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
            RpcObjectPickup();
#else
            ObjectPickupLocal();
#endif
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The object has been picked up on the client.
        /// </summary>
        [ClientRpc]
        private void RpcObjectPickup()
        {
            ObjectPickupLocal();
        }
#endif

        /// <summary>
        /// The object has been picked up. Add any effects.
        /// </summary>
        private void ObjectPickupLocal()
        {
            m_IsDepleted = true;

            // Play a pickup sound.
            if (m_PickupSound != null) {
                m_AudioSource.clip = m_PickupSound;
                m_AudioSource.Play();

                // The AudioSource will only play as long as the GameObject is active. Disable the renderer to hide the pickup object and completely deactivate the object
                // after the AudioSource is done playing.
                m_Renderer.enabled = false;
                Scheduler.Schedule(m_PickupSound.length, Deactivate);
            } else {
                // Deactivate the pickup for now. It can appear again if a Respawner component is attached to the GameObject.
                m_GameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Deactivates the GameObject.
        /// </summary>
        private void Deactivate()
        {
            m_GameObject.SetActive(false);
        }

        /// <summary>
        /// Set the depletion status of the object.
        /// </summary>
        /// <param name="value">The depleted state of the object.</param>
        private void SetDepleted(bool value)
        {
            m_IsDepleted = value;
        }
    }
}
