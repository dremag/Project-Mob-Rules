using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The SpeedChange ability allows the character to move at a different rate. Optionally a stamina can be used to prevent character from changing speeds for too long.
    /// </summary>
    public class SpeedChange : Ability
    {
        [Tooltip("The speed multiplier when the ability is active")]
        [SerializeField] protected float m_SpeedChangeMultiplier = 2;
        [Tooltip("The minimum value the SpeedChangeMultiplier can change the value to")]
        [SerializeField] protected float m_MinSpeedChangeValue = -2;
        [Tooltip("The maximum value the SpeedChangeMultiplier can change the value to")]
        [SerializeField] protected float m_MaxSpeedChangeValue = 2;
        [Tooltip("Can the ability be active while the character is aiming?")]
        [SerializeField] protected bool m_CanAim;
        [Tooltip("Should the character have stamina while in a different speed?")]
        [SerializeField] protected bool m_UseStamina;
        [Tooltip("The amount of stamina the character has")]
        [SerializeField] protected float m_MaxStamina = 100;
        [Tooltip("The rate at which the stamina decreases while in a different speed")]
        [SerializeField] protected float m_StaminaDecreaseRate = 0.5f;
        [Tooltip("The rate at which the stamina increases while not in a different speed")]
        [SerializeField] protected float m_StaminaIncreaseRate = 0.1f;
        [Tooltip("Should the ability activate automatically when a controller is connected?")]
        [SerializeField] protected bool m_AutoStartController = false;

        // Internal variables
        private float m_CurrentStamina;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (m_AutoStartController && GetComponent<Input.PlayerInput>().IsControllerConnected()) {
                StartType = AbilityStartType.Automatic;
            }

            m_CurrentStamina = m_MaxStamina;
        }

        /// <summary>
        /// Executed on every ability to allow the ability to update.
        /// </summary>
        public override void UpdateAbility()
        {
            base.UpdateAbility();

            // Restore the stamina when not changing speeds.
            if (!IsActive && m_UseStamina && m_CurrentStamina < m_MaxStamina) {
                m_CurrentStamina = Mathf.Clamp(m_CurrentStamina + m_StaminaIncreaseRate, 0, m_MaxStamina);
            }
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
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // The character can't change while aiming. In addition, the ability should stop if the character runs out of stamina.
            var canChangeSpeeds = m_CanAim || !m_Controller.Aiming;
            // The stamina should only decrease if the character is moving.
            if (m_UseStamina && m_Controller.Moving) {
                m_CurrentStamina = Mathf.Clamp(m_CurrentStamina - m_StaminaDecreaseRate, 0, m_MaxStamina);
                if (m_CurrentStamina == 0) {
                    StopAbility();
                    return true;
                }
            }

            if (canChangeSpeeds) {
                var inputVector = m_Controller.InputVector;
                inputVector.x = Mathf.Clamp(inputVector.x * m_SpeedChangeMultiplier, m_MinSpeedChangeValue, m_MaxSpeedChangeValue);
                inputVector.z = Mathf.Clamp(inputVector.z * m_SpeedChangeMultiplier, m_MinSpeedChangeValue, m_MaxSpeedChangeValue);
                m_Controller.InputVector = inputVector;
            }

            return true;
        }

        /// <summary>
        /// Should the upper body be forced to have the same time as the lower body? 
        /// </summary>
        /// <returns>True to indicate that the upper body should be forced to have the same time as the lower body.</returns>
        public override bool ForceUpperBodySynchronization()
        {
            return true;
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if the Animator should not update to reflect the current state.</returns>
        public override bool HasAnimatorControl()
        {
            return false;
        }
    }
}