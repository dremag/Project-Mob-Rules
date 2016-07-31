using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Dodge ability allows the character to quickly move to the left, right, or back.
    /// </summary>
    public class Dodge : Ability
    {
        [Tooltip("Does the Dodge ability require the character to be aiming?")]
        [SerializeField] protected bool m_RequireAim = true;
        [Tooltip("Should the ability start on a double press?")]
        [SerializeField] protected bool m_StartOnDoublePress = true;

        // Internal variables
        private enum DodgeType { Right, Left, Back, None }
        private DodgeType m_DodgeType;
        private float[] m_InitialMovementTime = new float[] { -1, -1 };
        private bool[] m_MovementRelease = new bool[] { false, false };

        /// <summary>
        /// Register for any events that the ability should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDodgeComplete", DodgeComplete);
        }

        /// <summary>
        /// Unregister for any events that the ability was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorDodgeComplete", DodgeComplete);
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            if (m_RequireAim && !m_Controller.Aiming) {
                return false;
            }

            // Start the ability if the left, right, or back buttons are double pressed.
            if (m_StartOnDoublePress) {
                for (int i = 0; i < 2; ++i) {
                    var input = i == 0 ? m_Controller.InputVector.x : m_Controller.InputVector.z;
                    if (Mathf.Abs(input) > 0) {
                        // The character can dodge left or right so check the sign value.
                        DodgeType dodgeType;
                        if (i == 0) {
                            dodgeType = input > 0 ? DodgeType.Right : DodgeType.Left;
                        } else {
                            dodgeType = input > 0 ? DodgeType.None : DodgeType.Back;
                        }
                        // There is no foward dodge.
                        if (dodgeType == DodgeType.None) {
                            return false;
                        }
                        if (m_DodgeType == dodgeType && m_MovementRelease[i] && m_InitialMovementTime[i] + 0.2f > Time.time) {
                            m_MovementRelease[i] = false;
                            return true;
                        }
                        m_DodgeType = dodgeType;
                        m_InitialMovementTime[i] = Time.time;
                        m_MovementRelease[i] = false;
                    } else if (m_InitialMovementTime[i] != -1 && input == 0) {
                        m_MovementRelease[i] = true;
                    }
                }
                return false;
            }

            // If the ability isn't being started with a double press then use the regular start methods.
            return base.CanStartAbility();
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // The ability only affects the base, upper, and any layers that the item specifies.
            if (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex && !m_AnimatorMonitor.ItemUsesAbilityLayer(this, layer)) {
                return string.Empty;
            }

            switch (m_DodgeType) {
                case DodgeType.Left:
                    return "Dodge.Dodge Left";
                case DodgeType.Right:
                    return "Dodge.Dodge Right";
                case DodgeType.Back:
                    return "Dodge.Dodge Back";
            }

            // How'd this happen?
            return string.Empty;
        }

        /// <summary>
        /// Should the ability override the item's high priority state?
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>True if the ability should override the item state.</returns>
        public override bool OverrideItemState(int layer)
        {
            return true;
        }

        /// <summary>
        /// The character wants to use the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        public override bool CanUseItem()
        {
            return false;
        }

        /// <summary>
        /// Callback when the animation is complete. Stop the ability.
        /// </summary>
        private void DodgeComplete()
        {
            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            // Reset the changed values.
            for (int i = 0; i < 2; ++i) {
                m_MovementRelease[i] = false;
                m_InitialMovementTime[i] = -1;
            }
        }
    }
}