using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Uses a LineRender to render a laser in the direction that the Item is aiming.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LaserSight : MonoBehaviour
    {
        [Tooltip("The speed at which the laser texture scrolls")]
        [SerializeField] protected float m_ScrollSpeed = -0.5f;
        [Tooltip("The maximum length of the laser")]
        [SerializeField] protected float m_MaxLength = 1000;
        [Tooltip("The minimum width of the laser")]
        [SerializeField] protected float m_MinWidth = 0.3f;
        [Tooltip("The maximum width of the laser")]
        [SerializeField] protected float m_MaxWidth = 0.7f;
        [Tooltip("The speed at which the laser changes width")]
        [SerializeField] protected float m_PulseSpeed = 0.5f;

        // SharedFields
#if ENABLE_MULTIPLAYER
        protected SharedMethod<bool> m_IsNetworked = null;
#else
        private SharedMethod<bool> m_IndependentLook = null;
#endif
        private SharedMethod<bool, Vector3> m_TargetLookPosition = null;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private Material m_Material;
        private float m_DeltaWidth;

        // Component references
        private Transform m_Transform;
        private LineRenderer m_LineRenderer;
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_LineRenderer = GetComponent<LineRenderer>();

            m_Material = m_LineRenderer.material;
            m_LineRenderer.SetWidth(m_MinWidth, m_MinWidth);
            m_DeltaWidth = m_MaxWidth - m_MinWidth;
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            m_Character = transform.GetComponentInParent<Inventory>().gameObject;
            SharedManager.InitializeSharedFields(m_Character, this);
            // Independent look characters do not need to communicate with the camera. Do not initialze the SharedFields on the network to prevent non-local characters from
            // using the main camera to determine their look direction. TargetLookPosition has been implemented by the NetworkMonitor component.
#if !ENABLE_MULTIPLAYER
            if (!m_IndependentLook.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
#endif
        }

        /// <summary>
        /// Updates the laser values to make it appear as though the laser is active. Will also set the LineRenderer's position to the object hit to prevent the laser from going through
        /// any objects.
        /// </summary>
        private void Update()
        {
            // Scroll the laser's texture to give it an appearance that the laser is active.
            var offset = m_Material.mainTextureOffset;
            offset.x += m_ScrollSpeed * Time.deltaTime;
            m_Material.mainTextureOffset = offset;

            // Pulse the width of the laser.
            var width = m_MinWidth + Mathf.PingPong(Time.time * m_PulseSpeed, m_DeltaWidth);
            m_LineRenderer.SetWidth(width, width);

            Vector3 direction;
            // If TargetLookPosition is null then use the forward direction. It may be null if the AI agent doesn't have the AIAgent component attached.
            if (m_TargetLookPosition == null) {
                direction = m_Transform.forward;
            } else {
                direction = (m_TargetLookPosition.Invoke(true) - m_Transform.position).normalized;
            }

            // Prevent the laser from going through objects.
            if (Physics.Raycast(m_Transform.position, direction, out m_RaycastHit, m_MaxLength, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                m_Transform.rotation = Quaternion.LookRotation(direction);
                m_LineRenderer.SetPosition(1, m_RaycastHit.distance * Vector3.forward);
            } else {
                m_LineRenderer.SetPosition(1, (m_MaxLength * Vector3.forward));
            }
        }
    }
}