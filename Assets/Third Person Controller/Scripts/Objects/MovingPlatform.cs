using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Smoothly moves between two triggers. Can also rotate. Subscribes to IIteractableTarget.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class MovingPlatform : NetworkBehaviour, IInteractableTarget
#else
    public class MovingPlatform : MonoBehaviour, IInteractableTarget
#endif
    {
        [Tooltip("Should the platform start moving when the object is enabled?")]
        [SerializeField] protected bool m_MoveWhenEnabled;
        [Tooltip("The amount of dampening to make the movement smooth")]
        [SerializeField] protected float m_MoveDampening;
        [Tooltip("The speed at which the platform rotates")]
        [SerializeField] protected Vector3 m_RotationSpeed;
        [Tooltip("Should the platform stop moving when it arrives at a waypoint?")]
        [SerializeField] protected bool m_StopAtWaypoint = false;
        [Tooltip("Can the direction be changed while the platform is moving?")]
        [SerializeField] protected bool m_CanInteractWhileMoving = false;
        [Tooltip("An array of Transforms which the moving platform will move towards. When the ending waypoint is reached it will loop back to the start")]
        [SerializeField] protected Transform[] m_Waypoints;
        [Tooltip("The starting waypoint index to move towards")]
        [SerializeField] protected int m_StartWaypointIndex = 1;

        // Internal variables
        private int m_CurrentWaypoint;
        private bool m_Translate;
        private bool m_Rotate;
        private Vector3 m_Velocity;
        
        // Component references
        private Transform m_Transform;
        private AudioSource m_AudioSource;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_AudioSource = GetComponent<AudioSource>();

            m_Translate = m_Waypoints.Length > 0;
            m_Rotate = m_RotationSpeed.sqrMagnitude > 0;
            m_CurrentWaypoint = m_StartWaypointIndex - 1; // Subtract 1 because when Interact is called it will increment the current index by 1. 
        }

        /// <summary>
        /// Starts the movement.
        /// </summary>
        private void Start()
        {
            if (m_MoveWhenEnabled) {
                Scheduler.Schedule(0.1f, Interact);
            }

            enabled = false;
        }

        /// <summary>
        /// Moves and rotates the platform according to the current parameters.
        /// </summary>
        private void FixedUpdate()
        {
#if ENABLE_MULTIPLAYER
            // Don't add any forces if not on the server. The server will move the object.
            if (!isServer) {
                return;
            }
#endif

            if (m_Translate) {
                var position = Vector3.SmoothDamp(m_Transform.position, m_Waypoints[m_CurrentWaypoint].position, ref m_Velocity, m_MoveDampening);
                m_Transform.position = position;
                if ((m_Transform.position - m_Waypoints[m_CurrentWaypoint].position).sqrMagnitude < 0.1f) {
                    if (m_StopAtWaypoint) {
                        if (m_AudioSource) {
                            m_AudioSource.Stop();
                        }
                        enabled = false;
                    } else {
                        m_CurrentWaypoint = (m_CurrentWaypoint + 1) % m_Waypoints.Length;
                    }
                }
            }

            if (m_Rotate) {
                m_Transform.Rotate(m_RotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Is the platform ready to be interacted with?
        /// </summary>
        /// <returns>True if the platform is ready to be interacted with.</returns>
        public bool IsInteractionReady()
        {
            // The platform can be interacted with if it is currently disabled or CanInteractWhileMoving is true.
            return !enabled || m_CanInteractWhileMoving;
        }

        /// <summary>
        /// Start the platform movement or reverse the platform direction if the platform is already enabled.
        /// </summary>
        public void Interact()
        {
            if (m_Translate) {
                m_CurrentWaypoint = (m_CurrentWaypoint + 1) % m_Waypoints.Length;
            }
            if (m_AudioSource) {
                m_AudioSource.Play();
            }
            enabled = true;
        }
    }
}