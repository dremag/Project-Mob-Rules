using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Fall ability allows the character to play a falling animation when the character has a negative y velocity.
    /// </summary>
    public class Fall : Ability
    {
        [Tooltip("The minimum height that the ability starts. Set to 0 to fall for any negative velocity")]
        [SerializeField] protected float m_MinFallHeight = 0;

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The ground distance must be greater then the minimum fall height if a value is set.
            RaycastHit hit;
            if (m_MinFallHeight != 0 && Physics.Raycast(m_Transform.position + m_Transform.up, -m_Transform.up, out hit, m_MinFallHeight + m_Transform.up.y, LayerManager.Mask.Ground)) {
                if (hit.distance < m_MinFallHeight + Vector3.up.y) { // Account for the y offset.
                    return false;
                }
            }
            // Fall can be started if the character is no on the ground and has a negative velocity.
            return !m_Controller.Grounded && m_Controller.Velocity.y < -0.001f;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerGrounded", OnGrounded);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorLand", OnLanded);
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

            // The Int Data parameter will contain an index value used to determine which leg should be in front.
            var prevStateData = m_AnimatorMonitor.IntDataValue;
            var stateName = string.Empty;
            if (prevStateData == 0) {
                stateName = "Fall";
            } else {
                stateName = "Fall Movement";
            }

            return "Fall." + stateName;
        }

        /// <summary>
        /// Should item collisions be allowed while the ability is active?
        /// </summary>
        /// <returns>True if item collisions should be allowed.</returns>
        public override bool AllowItemCollisions()
        {
            return true;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnControllerGrounded", OnGrounded);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorLand", OnLanded);
        }

        /// <summary>
        /// The character has changed grounded state. 
        /// </summary>
        /// <param name="grounded">Is the character on the ground?</param>
        private void OnGrounded(bool grounded)
        {
            if (grounded) {
                // Move to the fall end state when the character lands.
                m_AnimatorMonitor.SetStateValue(1);
            }
        }

        /// <summary>
        /// The land end animation has finished playing so the ability can now end.
        /// </summary>
        private void OnLanded()
        {
            StopAbility();
        }
    }
}