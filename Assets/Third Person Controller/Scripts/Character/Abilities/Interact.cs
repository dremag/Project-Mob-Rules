using UnityEngine;
using System.Collections;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Interact ability allows the character to interact with another object.
    /// </summary>
    public class Interact : Ability
    {
        [Tooltip("The ID of the IInteract object that the character can interact with. -1 indicates any")]
        [SerializeField] protected int m_InteractID = -1;
        [Tooltip("The name of the base layer interact state. Can be empty")]
        [SerializeField] protected string m_BaseStateName;
        [Tooltip("The name of the upper body interact state. Can be empty")]
        [SerializeField] protected string m_UpperBodyStateName = "Interact";
        [Tooltip("The name of the left arm interact state. Can be empty")]
        [SerializeField] protected string m_LeftArmStateName;
        [Tooltip("The name of the right arm interact state. Can be empty")]
        [SerializeField] protected string m_RightArmStateName;
        [Tooltip("The normalized speed that the character moves towards the target interact position")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f;
        [Tooltip("Can the item be equipped while interacting?")]
        [SerializeField] protected bool m_CanHaveItemEquipped;

        // Internal Variables
        private IInteractable m_Interactable = null;
        private bool m_IsReady;
        private bool m_InPosition;

        /// <summary>
        /// Register for any events that the ability should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<IInteractable>(m_GameObject, "OnInteractableHasInteractable", OnHasInteractable);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorInteractionComplete", OnInteractionComplete);
        }

        /// <summary>
        /// Unregister for any events that the ability was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<IInteractable>(m_GameObject, "OnInteractableHasInteractable", OnHasInteractable);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorInteractionComplete", OnInteractionComplete);
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            if (!m_Controller.Aiming && m_Interactable != null && m_Interactable.CanInteract()) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_IsReady = true;
            if (m_Interactable.RequiresTargetInteractorPosition()) {
                var targetPosition = m_Interactable.GetTargetInteractorPosition(m_Transform);
                var targetRotation = m_Interactable.GetTargetInteractorRotation();
                MoveToTarget(targetPosition, targetRotation, m_MinMoveToTargetSpeed, InPosition);
            } else {
                m_InPosition = true;
                m_AnimatorMonitor.DetermineStates();
                m_Interactable.Interact();
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (!m_InPosition || (layer > m_AnimatorMonitor.RightArmLayerIndex)) {
                return string.Empty;
            }

            if (layer == m_AnimatorMonitor.BaseLayerIndex) {
                return m_BaseStateName;
            }
            if (layer == m_AnimatorMonitor.UpperLayerIndex) {
                return m_UpperBodyStateName;
            }
            if (layer == m_AnimatorMonitor.LeftArmLayerIndex) {
                return m_LeftArmStateName;
            }
            return m_RightArmStateName;
        }

        /// <summary>
        /// Callback when the character either has an interactable target or no longer has an interactable target.
        /// </summary>
        /// <param name="interactable">The itneractable target. Can be null.</param>
        private void OnHasInteractable(IInteractable interactable)
        {
            // Only set the interactable if the target type is -1 or equal to the ID.
            if (interactable == null || m_InteractID == -1 || m_InteractID == interactable.GetInteractableID()) {
                m_Interactable = interactable;
            }
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            m_InPosition = true;
            m_Interactable.Interact();
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            return m_InPosition;
        }

        /// <summary>
        /// Callback when the character has interacted with the interactable.
        /// </summary>
        private void OnInteractionComplete()
        {
            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();
            m_InPosition = false;
            m_IsReady = false;
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if the Animator should not update to reflect the current state.</returns>
        public override bool HasAnimatorControl()
        {
            return m_InPosition;
        }

        /// <summary>
        /// Can the character have an item equipped while the ability is active?
        /// </summary>
        /// <returns>False to indicate that the character cannot have an item equipped.</returns>
        public override bool CanHaveItemEquipped()
        {
            return m_CanHaveItemEquipped;
        }

        /// <summary>
        /// The character wants to interact with the item.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return !m_IsReady || m_CanHaveItemEquipped;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            if (layer == m_AnimatorMonitor.UpperLayerIndex) {
                return false;
            }
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Is this ability unique? Unique abilities prevent multiple of the same ability type from being added. Editor Only.
        /// </summary>
        /// <returns>False to indicate that multiple Interact abilities can be added.</returns>
        public static bool IsUniqueAbility()
        {
            return false;
        }
#endif
    }
}