using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// When on a narrow object the Balance ability will slow the character's movements down and stretch the character's hands out to balance on the object.
    /// </summary>
    public class Balance : Ability
    {
        // The current Animator state that balance should be in.
        private enum BalanceID { Movement, StumbleLeft, StumbleRight }

        [Tooltip("Any drop more than this value can be a balance object")]
        [SerializeField] protected float m_MinDropHeight = 1;
        [Tooltip("Extra padding applied to the left and right side of the character when determining if over a balance object")]
        [SerializeField] protected float m_SidePadding = 0.1f;
        [Tooltip("Start stumbing if the horizontal input value is greater than this value")]
        [SerializeField] protected float m_StumbleMagnitude = 0.5f;

        // Internal variables
        private BalanceID m_BalanceID;

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return IsOnBalanceObject();
        }

        /// <summary>
        /// Is the character on a object that they can balance on?
        /// </summary>
        /// <returns>True if the character is on a balance object.</returns>
        private bool IsOnBalanceObject()
        {
            var front = m_Controller.CapsuleCollider.radius * (m_Transform.InverseTransformDirection(m_Controller.Velocity).z > -0.01f ? 1 : -1);
            
            // The character is not over a balance object if they are in the air.
            if (!Physics.Raycast(m_Transform.TransformPoint(0, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), -m_Transform.up, m_MinDropHeight)) {
                return false;
            }

            // The character is not over a balance object if there is an object just to the left of the character.
            if (Physics.Raycast(m_Transform.TransformPoint(-m_Controller.CapsuleCollider.radius - m_SidePadding, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), -m_Transform.up, m_MinDropHeight)) {
                // Do not assume that because there is nothing immediately to the left of the character that they are not on a balance object. 
                // If the object is not narrow then the character is not on a balance object.
                if (Physics.Raycast(m_Transform.TransformPoint(-(m_Controller.CapsuleCollider.radius * 2) - m_SidePadding, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), -m_Transform.up, m_MinDropHeight)) {
                    return false;
                }
            }

            // The character is not over a balance object if there is an object just to the right of the character.
            if (Physics.Raycast(m_Transform.TransformPoint(m_Controller.CapsuleCollider.radius + m_SidePadding, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), -m_Transform.up, m_MinDropHeight)) {
                // Do not assume that because there is nothing immediately to the right of the character that they are not on a balance object.
                // If the object is not narrow then the character is not on a balance object.
                if (Physics.Raycast(m_Transform.TransformPoint(m_Controller.CapsuleCollider.radius * 2 + m_SidePadding, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), -m_Transform.up, m_MinDropHeight)) {
                    return false;
                }
            }

            // The character is not over a balance object if there is a wall to the left of the character.
            if (Physics.Raycast(m_Transform.TransformPoint(0, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), -m_Transform.right, m_MinDropHeight)) {
                return false;
            }

            // The character is not over a balance object if there is a wall to the right of the character.
            if (Physics.Raycast(m_Transform.TransformPoint(0, m_Controller.CapsuleCollider.center.y - m_Controller.CapsuleCollider.height / 2 + 0.1f, front), m_Transform.right, m_MinDropHeight)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The ability has been started.
        /// </summary>
        protected override void AbilityStarted()
        {
            m_BalanceID = BalanceID.Movement;

            base.AbilityStarted();
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

            return "Balance.Movement";
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            if (m_Controller.RelativeInputVector.x > m_StumbleMagnitude) {
                m_BalanceID = BalanceID.StumbleRight;
            } else if (m_Controller.RelativeInputVector.x < -m_StumbleMagnitude) {
                m_BalanceID = BalanceID.StumbleLeft;
            } else {
                m_BalanceID = BalanceID.Movement;
            }
            m_AnimatorMonitor.SetStateValue((int)m_BalanceID);

            return true;
        }

        /// <summary>
        /// Can the character have an item equipped while the ability is active?
        /// </summary>
        /// <returns>True if the character can have an item equipped.</returns>
        public override bool CanHaveItemEquipped()
        {
            return false;
        }

        /// <summary>
        /// Can the ability be stopped?
        /// </summary>
        /// <returns>True if the ability can be stopped.</returns>
        public override bool CanStopAbility()
        {
            return !IsOnBalanceObject();
        }
    }
}