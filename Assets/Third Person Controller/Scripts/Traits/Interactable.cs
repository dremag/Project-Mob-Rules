using UnityEngine;
using Opsive.ThirdPersonController.Abilities;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interactable objects can be interacted with such as a switch or a button. The object can be interacted with when an object within the interactor layer
    /// enters the trigger and is facing the direction of the interaction object.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Interactable : MonoBehaviour, IInteractable
    {
        [Tooltip("The ID of the Interactable. Used for ability filtering by the character. -1 indicates no ID")]
        [SerializeField] protected int m_ID = -1;
        [Tooltip("The object perform the interaction on. This object must implement the IInteractableTarget interface")]
        [SerializeField] protected MonoBehaviour m_Target;
        [Tooltip("The character can interact when the angle between the look direction and interactable object is less than this amount")]
        [SerializeField] protected float m_InteractorLookInteractMaxAngle = 30;
        [Tooltip("The maximum x offset that the character can be standing away from the Interactable in order to interact")]
        [SerializeField] protected float m_MaxHorizontalOffset = 0.5f;
        [Tooltip("The layer of objects that can perform the interaction")]
        [SerializeField] protected LayerMask m_InteractorLayer;
        [Tooltip("The offset that the interactor should move to when interacting. A value of -1 means no movement on that axis")]
        [SerializeField] protected Vector3 m_TargetInteractorOffset = new Vector3(-1, -1, -1);

        // Component references
        private IInteractableTarget m_InteractableTarget;
        private Transform m_Interactor;
        private Transform m_Transform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;

            if (!(m_Target is IInteractableTarget)) {
                Debug.LogError("Target does not subscribe to the IInteractableTarget iterface");
                return;
            }
            m_InteractableTarget = m_Target as IInteractableTarget;

            // Activate when an object with the specified interactor layer is within the trigger.
            enabled = false;
        }

        /// <summary>
        /// An object has entered the trigger. Determine if it is an interactor.
        /// </summary>
        /// <param name="other">The potential interactor.</param>
        private void OnTriggerEnter(Collider other)
        {
            if (Utility.InLayerMask(other.gameObject.layer, m_InteractorLayer.value)) {
                m_Interactor = other.transform;
                EventHandler.ExecuteEvent<IInteractable>(m_Interactor.gameObject, "OnInteractableHasInteractable", this);
                enabled = true;
            }
        }

        /// <summary>
        /// The interactor can no longer interact with the object if it leaves the trigger.
        /// </summary>
        /// <param name="other">The potential interactor.</param>
        private void OnTriggerExit(Collider other)
        {
            if (other.transform.Equals(m_Interactor)) {
                EventHandler.ExecuteEvent<IInteractable>(m_Interactor.gameObject, "OnInteractableHasInteractable", null);
                m_Interactor = null;
                enabled = false;
            }
        }

        /// <summary>
        /// Returns the ID of the interactable object.
        /// </summary>
        /// <returns>The ID of the interactable object.</returns>
        public int GetInteractableID()
        {
            return m_ID;
        }

        /// <summary>
        /// Determines if the Interactor can interact with the InteractableTarget. Cases where the Interactor cannot interact include not facing the Interactable object or the camera
        /// not looking at the Interactable object.
        /// </summary>
        /// <returns>True if the Interactor can interact with the InteractableTarget</returns>
        public bool CanInteract()
        {
            if (m_Interactor == null || !m_InteractableTarget.IsInteractionReady()) {
                return false;
            }

            // The character must be stading in front of the object and looking at it.
            var relativePosition = m_Transform.InverseTransformPoint(m_Interactor.position);
            var backward = -m_Transform.forward;
            var interactorForward = m_Interactor.forward;
            // Ignore the y direction.
            backward.y = interactorForward.y = 0;
            return Mathf.Abs(relativePosition.x) < m_MaxHorizontalOffset && relativePosition.z > 0 &&
                    Vector3.Angle(backward, interactorForward) < m_InteractorLookInteractMaxAngle;
        }

        /// <summary>
        /// Does the interactable object require the interactor to be in a target position?
        /// </summary>
        /// <returns>True if the interactor is required to be in a target position.</returns>
        public bool RequiresTargetInteractorPosition()
        {
            return m_TargetInteractorOffset.x != -1 || m_TargetInteractorOffset.y != -1 || m_TargetInteractorOffset.z != -1;
        }

        /// <summary>
        /// Returns the rotation that the interactor should face before interacting with the object.
        /// </summary>
        /// <returns>The target interactor rotation.</returns>
        public Quaternion GetTargetInteractorRotation()
        {
            // The character should be facing the opposite direction the interactable object is facing.
            return Quaternion.LookRotation(-m_Transform.forward);
        }

        /// <summary>
        /// Returns the position that the interactor should move to before interacting with the object.
        /// </summary>
        /// <param name="interactorTransform">The transform of the interactor.</param>
        /// <returns>The target interactor position.</returns>
        public Vector3 GetTargetInteractorPosition(Transform interactorTransform)
        {
            // Ignore the axis if the offset has a -1 value.
            var position = m_Transform.InverseTransformPoint(interactorTransform.position);
            if (m_TargetInteractorOffset.x != -1) {
                position.x = m_TargetInteractorOffset.x;
            }
            if (m_TargetInteractorOffset.y != -1) {
                position.y = m_TargetInteractorOffset.y;
            }
            if (m_TargetInteractorOffset.z != -1) {
                position.z = m_TargetInteractorOffset.z;
            }
            return m_Transform.TransformPoint(position);
        }

        /// <summary>
        /// The interactor is looking at the object and wants to perform an interaction. Perform that interaction.
        /// </summary>
        public void Interact()
        {
            EventHandler.RegisterEvent(m_Interactor.gameObject, "OnAnimatorInteracted", OnInteracted);
            EventHandler.ExecuteEvent(m_Interactor.gameObject, "OnInteractableInteract");
        }

        /// <summary>
        /// The animator has interacted with the target so the target should be notified.
        /// </summary>
        private void OnInteracted()
        {
            // Interactor may be null if the character leaves the interactable object before the interact animation has a chance to finish.
            if (m_Interactor != null) {
                EventHandler.UnregisterEvent(m_Interactor.gameObject, "OnAnimatorInteracted", OnInteracted);
                // In the timespan since the interaction was triggered the target may not longer be able to be interacted with.
                if (m_InteractableTarget != null && m_InteractableTarget.IsInteractionReady()) {
                    m_InteractableTarget.Interact();
                }
            }
        }
    }
}