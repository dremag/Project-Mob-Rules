using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Generic ability is a generic ability implementation. There are no transitions and the ability will end after the state is complete or ended through an event.
    /// This ability is great if you want to play an animation that does not affect movement.
    /// </summary>
    public class Generic : Ability
    {
        [Tooltip("The name of the base state. An empty value indicates the layer is not used")]
        [SerializeField] protected string m_BaseStateName;
        [Tooltip("The name of the upper body state. An empty value indicates the layer is not used")]
        [SerializeField] protected string m_UpperBodyStateName;
        [Tooltip("Should the ability be stopped after the animation state has finished?")]
        [SerializeField] protected bool m_StopOnStateComplete = true;
        [Tooltip("If Stop on State Complete is enabled, which layer should be used to determine if the state is complete?")]
        [SerializeField] protected int m_StopOnStateCompleteLayer;
        [Tooltip("The unique ID of the ability. Used if stopping the ability with an event. -1 indicates no use")]
        [SerializeField] protected int m_ID = -1;
        [Tooltip("Can the item be equipped?")]
        [SerializeField] protected bool m_CanHaveItemEquipped = true;
        [Tooltip("Should IK be used on the lower body layer?")]
        [SerializeField] protected bool m_UseLowerBodyIK = true;
        [Tooltip("Should IK be used on the upper body layer?")]
        [SerializeField] protected bool m_UseUpperBodyIK = true;

        // Internal variables
        private bool m_WaitForTransition;

        // Exposed properties.
        public int ID { get { return m_ID; } }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_WaitForTransition = true;
            EventHandler.RegisterEvent(m_GameObject, "OnGenericAbilityStop", StopGenericAbility);
            EventHandler.RegisterEvent<int>(m_GameObject, "OnGenericAbilityStopID", StopGenericAbility);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (layer == m_AnimatorMonitor.BaseLayerIndex) {
                return m_BaseStateName;
            }
            if (layer == m_AnimatorMonitor.UpperLayerIndex) {
                return m_UpperBodyStateName;
            }
            return string.Empty;
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // GetNextAnimatorStateInfo hash will be 0 when the Animator is not in a transition.
#if UNITY_4_6 || UNITY_4_7
            if (m_StopOnStateComplete && !m_WaitForTransition && m_Animator.GetNextAnimatorStateInfo(0).nameHash == 0) {
#else
            if (m_StopOnStateComplete && !m_WaitForTransition && m_Animator.GetNextAnimatorStateInfo(0).fullPathHash == 0) {
#endif
                if (m_Animator.GetCurrentAnimatorStateInfo(m_StopOnStateCompleteLayer).normalizedTime > 1) {
                    StopAbility();
                }
            } else {
                m_WaitForTransition = false;
            }

            return false;
        }

        /// <summary>
        /// Stops the generic ability.
        /// </summary>
        /// <param name="id">The ID of the generic ability to stop.</param>
        private void StopGenericAbility()
        {
            StopGenericAbility(m_ID);
        }

        /// <summary>
        /// Stops the generic ability.
        /// </summary>
        /// <param name="id">The ID of the generic ability to stop.</param>
        private void StopGenericAbility(int id)
        {
            if (id == -1 || m_ID == id) {
                StopAbility();
            }
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>False to indicate that the item cannot be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return m_CanHaveItemEquipped;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            if (layer == m_AnimatorMonitor.BaseLayerIndex) {
                return m_UseLowerBodyIK;
            }
            if (layer == m_AnimatorMonitor.UpperLayerIndex) {
                return m_UseUpperBodyIK;
            }
            return false;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            EventHandler.UnregisterEvent(m_GameObject, "OnGenericAbilityStop", StopGenericAbility);
            EventHandler.UnregisterEvent<int>(m_GameObject, "OnGenericAbilityStopID", StopGenericAbility);
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