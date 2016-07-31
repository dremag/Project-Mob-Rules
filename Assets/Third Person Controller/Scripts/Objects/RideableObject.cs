using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The RideableObject represents any Third Person Controller character object that can be ridden by another character. When the character is mounted on the object
    /// it will then have control.
    /// </summary>
    public class RideableObject : MonoBehaviour
    {
        [Tooltip("A reference to the Transform where the character can mount on the right side")]
        [SerializeField] protected Transform m_RightMount;
        [Tooltip("A reference to the Transform where the character can mount on the left side")]
        [SerializeField] protected Transform m_LeftMount;
        [Tooltip("A reference to the Transform where the character should mount to")]
        [SerializeField] protected Transform m_MountParent;

        // Internal variables
        private bool m_UseRightMount;

        // Component references
        private GameObject m_GameObject;

        // Exposed properties
        public bool UseRightMount { get { return m_UseRightMount; } }
        public Transform MountParent { get { return m_MountParent; } }

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            if (m_MountParent == null) {
                m_MountParent = transform;
            }
        }

        /// <summary>
        /// The object should wait until the character is mounted before having input control.
        /// </summary>
        public void Start()
        {
            EventHandler.ExecuteEvent(m_GameObject, "OnAllowGameplayInput", false);
            EventHandler.ExecuteEvent("OnInputAttachCharacter", gameObject);
        }

        /// <summary>
        /// Returns the closest mount position to the character.
        /// </summary>
        /// <param name="position">The position of the character.</param>
        /// <returns>The closest mount position.</returns>
        public Transform GetClosestMount(Vector3 position)
        {
            if (m_UseRightMount = ((m_RightMount.position - position).sqrMagnitude < (m_LeftMount.position - position).sqrMagnitude)) {
                return m_RightMount;
            }
            return m_LeftMount;
        }

        /// <summary>
        /// The character has mounted on the object. Allow for gameplay input.
        /// </summary>
        public void Mounted()
        {
            EventHandler.ExecuteEvent(m_GameObject, "OnAllowGameplayInput", true);
        }

        /// <summary>
        /// The character has dismounted on the object. Disallow gameplay input.
        /// </summary>
        public void Dismount()
        {
            EventHandler.ExecuteEvent(m_GameObject, "OnAllowGameplayInput", false);
        }
    }
}