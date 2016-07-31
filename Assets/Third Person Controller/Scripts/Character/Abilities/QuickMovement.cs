using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Quick Movement ability allows the character to play a quick stop or quick turn animation.
    /// </summary>
    public class QuickMovement : Ability
    {
        [Tooltip("Can the character quick turn?")]
        [SerializeField] protected bool m_CanQuickTurn = true;
        [Tooltip("Perform a quick stop if the character's speed is over this value")]
        [SerializeField] protected float m_QuickTurnSpeed = 3;
        [Tooltip("Check for a quick turn after the specified number of seconds. This gives the character some time to actually start making the turn")]
        [SerializeField] protected float m_QuickTurnWait = 0.1f;
        [Tooltip("Quick turn can be activated if the character makes a turn greater than the specified angle?")]
        [SerializeField] protected float m_QuickTurnAngle = 160;
        [Tooltip("The normalized length of one movement cycle")]
        [SerializeField] protected float m_MovementCycleNormalizedLength = 0.5f;
        [Tooltip("Determines the correct leg to quick turn from")]
        [SerializeField] protected float m_MovementCycleLegOffset = 0.2f;
        [Tooltip("Can the character quick stop?")]
        [SerializeField] protected bool m_CanQuickStop = true;
        [Tooltip("Perform a quick stop if the character's speed is over this value")]
        [SerializeField] protected float m_QuickStopSpeed = 3;

        // Internal variables
        private float m_QuickTurnStartTime = -1;
        private bool m_CheckQuickStop;
        private bool m_ShouldQuickStop;
        private bool m_ShouldQuickTurn;
        private bool m_WaitForTransition;

        /// <summary>
        /// Executed on every ability to allow the ability to update.
        /// </summary>
        public override void UpdateAbility()
        {
            base.UpdateAbility();

            // No quick movements can run if the character is not on the ground.
            if (IsActive || !m_Controller.Grounded) {
                return;
            }

            // Only the x and z velocity matter. The y velocity may be a non-zero value if just landing from a jump or moving up a slope.
            var velocity = m_Controller.Velocity;
            velocity.y = 0;
            var speed = velocity.magnitude;
            if (m_CanQuickTurn) {
                if (speed > m_QuickTurnSpeed) {
                    m_QuickTurnStartTime = Time.time;
                }

                // Quick turn can only be activated after a small amount of time has elapsed and the character changed directions. This gives the character some time to actually start making the turn.
                if (m_QuickTurnStartTime + m_QuickTurnWait > Time.time) {
                    // The character angle depends on the movement type. Do not apply any smoothing so the future angle is used.
                    var targetAngle = m_Transform.eulerAngles.y;
                    if (m_Controller.Movement == RigidbodyCharacterController.MovementType.Adventure && !m_Controller.Aiming) {
                        if (m_Controller.InputVector != Vector3.zero) {
                            targetAngle = Quaternion.LookRotation(m_Controller.LookRotation * m_Controller.InputVector.normalized).eulerAngles.y;
                        }
                    } else {
                        targetAngle = m_Controller.LookRotation.eulerAngles.y;
                    }
                    m_ShouldQuickTurn = Mathf.Abs(Mathf.DeltaAngle(m_Transform.eulerAngles.y, targetAngle)) > m_QuickTurnAngle;
                }
            }

            if (m_CanQuickStop) {
                if (speed > m_QuickStopSpeed) {
                    m_CheckQuickStop = true;
                }
                // Quick stop cannot play if the quick turn animation is activated.
                if (!m_ShouldQuickTurn) {
                    // Only activate quick stop if the character stops moving and there is no chance the quick turn animation will run.
                    if (m_Controller.InputVector.magnitude < 0.1f && m_CheckQuickStop && (!m_CanQuickTurn || m_QuickTurnStartTime + m_QuickTurnWait < Time.time)) {
                        m_ShouldQuickStop = true;
                        m_CheckQuickStop = false;
                    }
                }
            }
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return m_ShouldQuickStop || m_ShouldQuickTurn;
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // The ability only affects the base layer
            if (layer != m_AnimatorMonitor.BaseLayerIndex) {
                return string.Empty;
            }

            if (m_ShouldQuickTurn) {
                // Calculate which leg is behind, so as to leave that leg trailing in the quick turn animation.
                var runCycle = Mathf.Repeat(m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + m_MovementCycleLegOffset, m_MovementCycleNormalizedLength);
                if (runCycle < m_MovementCycleNormalizedLength / 2) {
                    return "Quick Movement.Quick Turn Left";
                } else {
                    return "Quick Movement.Quick Turn Right";
                }
            }

            if (m_ShouldQuickStop) {
                return "Quick Movement.Quick Stop";
            }

            return string.Empty;
        }

        /// <summary>
        /// The ability has been started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_WaitForTransition = true;
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // Stop the ability after the animation is done playing. GetNextAnimatorStateInfo hash will be 0 when the Animator is not in a transition.
#if UNITY_4_6 || UNITY_4_7
            if (!m_WaitForTransition && m_Animator.GetNextAnimatorStateInfo(0).nameHash == 0) {
#else
            if (!m_WaitForTransition && !m_Animator.IsInTransition(0)) {
#endif
                if (m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1) {
                    StopAbility();
                }
            } else {
                m_WaitForTransition = false;
            }

            // Stop the quick stop early if the character starts to move again.
            if (m_ShouldQuickStop && m_Controller.InputVector.sqrMagnitude > 0.01f) {
                StopAbility();
            }

            return true;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // Rotate according to the animations.
            if (m_ShouldQuickTurn) {
                m_Transform.rotation *= m_Controller.RootMotionRotation;
                m_Controller.RootMotionRotation = Quaternion.identity;
                return false;
            }
            // Quick stop does not need to rotate.
            return true;
        }

        /// <summary>
        /// The Animator has changed positions or rotations.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its OnAnimatorMove method?</returns>
        public override bool AnimatorMove()
        {
            // Move according to root motion.
            m_Controller.RootMotionForce = m_Animator.deltaPosition * m_Controller.RootMotionSpeedMultiplier;

            // Rotate according to root motion.
            m_Controller.RootMotionRotation *= m_Animator.deltaRotation;
            return false;
        }

        /// <summary>
        /// Stops the ability.
        /// </summary>
        private void StopQuickMovementAbility()
        {
            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            m_ShouldQuickStop = m_ShouldQuickTurn = false;
            m_QuickTurnStartTime = -1;
            m_CheckQuickStop = false;

            base.AbilityStopped();
        }
    }
}