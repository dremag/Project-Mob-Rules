using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Jump ability allows the character to jump into the air. Jump is only active when the character has a positive y velocity.
    /// </summary>
    public class Jump : Ability
    {
        [Tooltip("The amount of force that should be applied when the character jumps")]
        [SerializeField] protected float m_Force = 5;
        [Tooltip("The force to apply for a double jump. 0 indicates that a double jump is not possible")]
        [SerializeField] protected float m_DoubleJumpForce;
        [Tooltip("Prevent the character from jumping too quickly after jumping")]
        [SerializeField] protected float m_RecurrenceDelay = 0.2f;
        [Tooltip("The normalized length of one movement cycle")]
        [SerializeField] protected float m_MovementCycleNormalizedLength = 0.5f;
        [Tooltip("Determines the correct leg to jump off of")]
        [SerializeField] protected float m_MovementCycleLegOffset;
        [Tooltip("Should uniform acceleration be used on a jump?")]
        [SerializeField] protected bool m_UniformAcceleration;

        // Internal variables
        private bool m_Jumping;
        private float m_JumpTime = -1;
        private float m_LandTime = -1;
        private bool m_DoubleJumped;

        // Component references
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Register for any events that the ability should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerGrounded", OnGrounded);
        }

        /// <summary>
        /// Unregister for any events that the ability was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnControllerGrounded", OnGrounded);
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return m_Controller.Grounded && m_LandTime + m_RecurrenceDelay < Time.time;
        }

        /// <summary>
        /// Can the specified ability start?
        /// </summary>
        /// <param name="ability">The ability that is trying to start.</param>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility(Ability ability)
        {
            // Fall can only be started after the character has actually jumped.
            if (ability is Fall) {
                return m_Jumping;
            }
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();
            m_Controller.Grounded = false;

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorStartJump", OnStartJump);

            // Tell the ControllerHandler to listen for the double jump event.
            if (m_DoubleJumpForce != 0) {
                EventHandler.RegisterEvent(m_GameObject, "OnJumpAbilityDoubleJump", OnDoubleJump);
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityRegisterInput", InputName, "OnJumpAbilityDoubleJump");
            }
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

            var stateName = string.Empty;

            if (Mathf.Abs(m_AnimatorMonitor.ForwardInputValue) < 0.1f) {
                m_AnimatorMonitor.SetIntDataValue(0);
                stateName = "Jump Start";
            } else {
                // Calculate which leg is behind, so as to leave that leg trailing in the jump animation.
                var runCycle = Mathf.Repeat(m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + m_MovementCycleLegOffset, m_MovementCycleNormalizedLength);
                if (runCycle < m_MovementCycleNormalizedLength / 2) {
                    m_AnimatorMonitor.SetIntDataValue(1);
                    stateName = "Jump Up Left";
                } else {
                    m_AnimatorMonitor.SetIntDataValue(2);
                    stateName = "Jump Up Right";
                }
            }

            return "Jump." + stateName;
        }

        /// <summary>
        /// Perform checks to determine if the character is on the ground.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckGround method?</returns>
        public override bool CheckGround()
        {
            if (m_JumpTime != -1 && m_JumpTime + 0.2f < Time.time) {
                var colliderRadius = m_Controller.CapsuleCollider.radius - 0.05f;
                RaycastHit hit;
                if (Physics.SphereCast(m_Transform.position + m_Transform.up * colliderRadius * 2, colliderRadius, -m_Transform.up, out hit,
                                                                colliderRadius * 2 + m_Controller.SkinWidth, LayerManager.Mask.Ground)) {
                    StopAbility(true);
                }
            }
            return false;
        }

        /// <summary>
        /// Set the physic material based on the grounded and stepping state.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its SetPhysicsMaterial method?</returns>
        public override bool SetPhysicMaterial()
        {
            m_Controller.CapsuleCollider.material = m_Controller.AirFrictionMaterial;
            return false;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            // Set the Float Data parameter for the blend tree.
            m_AnimatorMonitor.SetFloatDataValue(m_Controller.Grounded ? 0 : m_Controller.Velocity.y);
            return true;
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
        /// The character has either landed or just left the ground.
        /// </summary>
        /// <param name="grounded">Is the character on the ground?</param>
        private void OnGrounded(bool grounded)
        {
            if (grounded) {
                if (m_Jumping) {
                    StopAbility();
                }
                // Remember the land time to prevent jumping more than the JumpReoccuranceDelay.
                m_LandTime = Time.time;
                m_Jumping = false;
            }
        }

        /// <summary>
        /// The start jump animation has finished playing so now the Rigidbody should have a force added to it.
        /// </summary>
        private void OnStartJump()
        {
            if (!m_Jumping) {
                m_Jumping = true;
                m_JumpTime = Time.time;
                var velocity = m_Rigidbody.velocity;
                // The velocity may be negative if going down a slope.
                if (velocity.y < 0) {
                    velocity.y = 0;
                }
                // Optionally apply a uniform acceleration formula to our jump force. This allows the character to jump an approximate number of units high
                // making it a easier to gauge the necessary force for your characters jump in platformers.
                velocity.y += (m_UniformAcceleration ? Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * m_Force) : m_Force);
                m_Rigidbody.velocity = velocity;
            }
        }

        /// <summary>
        /// Perform a double jump.
        /// </summary>
        private void OnDoubleJump()
        {
            // Do not allow multiple double jumps.
            if (!m_DoubleJumped) {
                m_DoubleJumped = true;
                var velocity = m_Rigidbody.velocity;
                // Optionally apply a uniform acceleration formula to our double jump force. This allows the character to jump an approximate number of units high
                // making it a easier to gauge the necessary force for your characters jump in platformers.
                velocity.y += (m_UniformAcceleration ? Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * m_DoubleJumpForce) : m_DoubleJumpForce);
                m_Rigidbody.velocity = velocity;
                // Optionally allow an animation to play when the character double jumps.
                m_AnimatorMonitor.SetIntDataValue(3);
            }
        }

        /// <summary>
        /// Can the ability be stopped?
        /// </summary>
        /// <returns>True if the ability can be stopped.</returns>
        public override bool CanStopAbility()
        {
            // The Jump ability is done if the velocity is less than a small value. Use a non-zero small value because the jump should stop if the character is 
            // intersecting with another object. The velocity will still be positive by the jump ability should end.
            if (m_Jumping && !m_Controller.Grounded && m_Controller.Velocity.y <= 0.001f) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_Jumping = false;
            m_JumpTime = -1;
            m_DoubleJumped = false;
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorStartJump", OnStartJump);

            // No longer listen for the double jump event.
            if (m_DoubleJumpForce != 0) {
                EventHandler.UnregisterEvent(m_GameObject, "OnJumpAbilityDoubleJump", OnDoubleJump);
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnregisterInput", InputName, "OnJumpAbilityDoubleJump");
            }
        }
    }
}