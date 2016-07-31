using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Roll ability allows the character to roll. The character can only roll when moving in a relative positive z velocity.
    /// </summary>
    public class Roll : Ability
    {
        [Tooltip("Prevent the character from rolling too quickly after rolling")]
        [SerializeField] protected float m_RollRecurrenceDelay = 0.2f;

        // Internal variables
        private float m_LastRollTime;

        // SharedFields
        private SharedMethod<string, bool, bool> m_ChangeCameraState = null;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            
            m_LastRollTime = -m_RollRecurrenceDelay;
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return m_LastRollTime + m_RollRecurrenceDelay < Time.time && m_Transform.InverseTransformDirection(m_Controller.Velocity).z >= -0.1f;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_Controller.ForceRootMotion = true;

            // When starting to roll divide the collider height in half.
            var height = m_Controller.CapsuleCollider.height;
            height /= 2;
            var center = m_Controller.CapsuleCollider.center;
            center.y = center.y - (m_Controller.CapsuleCollider.height - height) / 2;
            m_Controller.CapsuleCollider.height = height;
            m_Controller.CapsuleCollider.center = center;

            // Keep the camera steady during the roll.
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke("StaticHeight", true);
            }
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorRollComplete", OnRollComplete);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // The ability only affects the base and upper layers.
            if (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex) {
                return string.Empty;
            }

            return "Roll.Roll";
        }

        /// <summary>
        /// The roll animation has ended. 
        /// </summary>
        private void OnRollComplete()
        {
            m_LastRollTime = Time.time;
            m_Controller.ForceRootMotion = false;
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke("StaticHeight", false);
            }
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorRollComplete", OnRollComplete);

            // When ending the crouch restore the collider height.
            var height = m_Controller.CapsuleCollider.height;
            height *= 2;
            m_Controller.CapsuleCollider.height = height;
            var center = m_Controller.CapsuleCollider.center;
            center.y = m_Controller.CapsuleCollider.height / 2;
            m_Controller.CapsuleCollider.center = center;

            StopAbility();
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>False to indicate that the item cannot be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return false;
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