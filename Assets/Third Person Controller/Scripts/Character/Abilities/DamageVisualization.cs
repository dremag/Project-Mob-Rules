using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The DamageVisualization ability allows the character to react when getting hit by an object (bullet, rocket, basetball bat, etc).
    /// </summary>
    public class DamageVisualization : Ability
    {
        // Internal variables
        private enum DamageType { None, Front, Back }
        private DamageType m_DamageType;
        
        /// <summary>
        /// Register for any events that the ability should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<float, Vector3, Vector3, GameObject>(m_GameObject, "OnHealthDamageDetails", TookDamage);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDamageVisualizationComplete", DamageVisualizationComplete);
        }

        /// <summary>
        /// Unregister for any events that the ability was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<float, Vector3, Vector3, GameObject>(m_GameObject, "OnHealthDamageDetails", TookDamage);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorDamageVisualizationComplete", DamageVisualizationComplete);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // Only the additive layer can play a damage animation.
            if (m_DamageType == DamageType.None || layer != m_AnimatorMonitor.AdditiveLayerIndex) {
                return string.Empty;
            }

            return m_DamageType == DamageType.Front ? "Front Damage" : "Back Damage";
        }

        /// <summary>
        /// Can this ability run at the same time as another ability?
        /// </summary>
        /// <returns>True if this ability can run with another ability.</returns>
        public override bool IsConcurrentAblity()
        {
            return true;
        }

        /// <summary>
        /// The character took some damage at the specified position. Apply the animation in direction of the damage.
        /// </summary>
        /// <param name="amount">The total amount of damage inflicted on the character.</param>
        /// <param name="position">The position that the character took the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        private void TookDamage(float amount, Vector3 position, Vector3 force, GameObject attacker)
        {
            var relativePosition = m_Transform.InverseTransformPoint(position);
            // Do not show any damage if the position is less then the center of the capsule collider. The legs do not animation when getting hit.
            if (relativePosition.y < m_Controller.CapsuleCollider.center.y) {
                return;
            }
            m_DamageType = relativePosition.z > 0 ? DamageType.Front : DamageType.Back;

            StartAbility();
        }

        /// <summary>
        /// Callback when the animation is complete. Stop the ability.
        /// </summary>
        private void DamageVisualizationComplete()
        {
            m_DamageType = DamageType.None;
            StopAbility();
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return false;
        }
    }
}