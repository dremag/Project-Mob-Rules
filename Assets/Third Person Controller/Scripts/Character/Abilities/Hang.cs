using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Hang ability allows the character to hang and shimmy from a tall object.
    /// </summary>
    public class Hang : Ability
    {
        [Tooltip("The character can start to hang if within the specified distance")]
        [SerializeField] protected float m_StartHangDistance = 0.5f;
        [Tooltip("The difference between the top of the hang object and the top of the character")]
        [SerializeField] protected Vector3 m_HangOffset = new Vector3(0, 0.2f, 0.2f);
        [Tooltip("The layer of the objects that the character can hang on")]
        [SerializeField] protected LayerMask m_HangLayer;
        [Tooltip("Strafe offset to apply when checking for cover while shimmying")]
        [SerializeField] protected float m_ShimmyOffset = 0.1f;
        [Tooltip("Can move and continue to take hang on objects as long as the new hang object has a normal angle difference less than this amount")]
        [SerializeField] protected float m_AngleThreshold = 1;
        [Tooltip("The speed that the character can rotate while hanging")]
        [SerializeField] protected float m_RotationSpeed = 4;
        [Tooltip("The character can hang for at least as long as the specified duration before another ability can play")]
        [SerializeField] protected float m_MinHangDuration = 0.5f;

        // Exposed priperties
        public float VerticalPosition { get { return m_Transform.position.y + m_Controller.CapsuleCollider.height + m_HangOffset.y; } }

        // Internal variables
        private float m_StartTime;
        private float m_EndTime;
        private RaycastHit m_RaycastHit;
        private Vector3 m_HangPoint;
        private Vector3 m_HangNormal;
        private bool m_ItemUnequipped;

        // Component references
        private Rigidbody m_Rigidbody;
        private ShortClimb m_ShortClimb;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
            m_ShortClimb = GetComponent<ShortClimb>();
        }

        /// <summary>
        /// Executed on every ability to allow the ability to update. The ability may need to update if it needs to do something when inactive or show a GUI icon when the ability can be started.
        /// </summary>
        public override void UpdateAbility()
        {
            base.UpdateAbility();

            // Toggle the item equip state before the character starts to hang so the unequip animation doesn't play as the character is hanging there.
            if (!IsActive) {
                if (!m_Controller.Grounded) {
                    if (Physics.Raycast(m_Transform.position + m_Transform.up * (m_Controller.CapsuleCollider.height + m_HangOffset.y), m_Transform.forward, out m_RaycastHit, 
                                            m_Controller.CapsuleCollider.radius + m_StartHangDistance, m_HangLayer.value)) {
                        if (m_EndTime + 0.5f < Time.time && m_ItemEquipped.Get() && !m_ItemUnequipped) {
                            EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                            m_ItemUnequipped = true;
                            m_EquipItem = true;
                        }
                        return;
                    } 
                }
                if (m_ItemUnequipped && !CanStartAbility()) {
                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                    m_ItemUnequipped = false;
                    m_EquipItem = false;
                }
            }
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // Can only hang if the character is in the air.
            if (m_Controller.Grounded) {
                return false;
            }


            // Determine if there is an object which the character can hang from.
            if (Physics.Raycast(m_Transform.position + m_Transform.up * (m_Controller.CapsuleCollider.height + m_HangOffset.y), m_Transform.forward, out m_RaycastHit, 
                                    m_Controller.CapsuleCollider.radius + m_StartHangDistance, m_HangLayer.value)) {
                // Do not hang on objects that have the ClimbableObject component.
                if (m_RaycastHit.collider.GetComponent<ClimbableObject>() != null) {
                    return false;
                }

                m_HangNormal = -m_RaycastHit.normal;
                m_HangPoint = m_RaycastHit.point;
                // There should not be an object directly above the hang point
                return !Physics.Raycast(m_Transform.position + m_Transform.up * (m_Controller.CapsuleCollider.height + m_HangOffset.y + 0.1f), m_Transform.forward, out m_RaycastHit, 
                                            m_Controller.CapsuleCollider.radius + m_StartHangDistance, m_HangLayer.value);
            }

            return false;
        }

        /// <summary>
        /// Can the specified ability start?
        /// </summary>
        /// <param name="ability">The ability that is trying to start.</param>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility(Ability ability)
        {
            if (ability is ShortClimb) {
                return m_StartTime + m_MinHangDuration < Time.time;
            }
            return true;
        }

        /// <summary>
        /// The ability has been started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_Rigidbody.useGravity = false;
            m_StartTime = Time.time;
            m_Controller.StopMovement();
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

            return "Hang.Hang";
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // Stop hanging if the character moves away from the hang object or if the short climb ability should start.
            if (m_Controller.InputVector.z < 0) {
                StopAbility();
            } else if (m_Controller.InputVector.z > 0 && m_ShortClimb != null) {
                if (m_Controller.TryStartAbility(m_ShortClimb, true)) {
                    if (m_EquipItem) {
                        m_EquipItem = false;
                        m_ShortClimb.EquipItem = true;
                    }
                    StopAbility();
                }
            }
            return base.Move();
        }

        /// <summary>
        /// Apply any movement.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            var relativeForce = Quaternion.Inverse(m_Transform.rotation) * m_Controller.RootMotionForce;
            var horizontalMovement = relativeForce.x;
            //Debug.Log(horizontalMovement + " " + m_Controller.InputVector.x);
            // The horizontal movement will be a non-zero number if the character is currently shimmying. If the character is not shimmying then use the input vector.
            if (Mathf.Abs(horizontalMovement) < 0.01f) {
                horizontalMovement = m_Controller.InputVector.x;
            }
            // Do not allow horizontal movement if there is not a hanging object in the direction of the desired movement.
            if (Mathf.Abs(horizontalMovement) > 0) {
                if (!Physics.Raycast(m_Transform.position + m_Transform.up * (m_Controller.CapsuleCollider.height + m_HangOffset.y) + m_Transform.right * m_ShimmyOffset * (horizontalMovement > 0 ? 1 : -1),
                                        m_Transform.forward, m_Controller.CapsuleCollider.radius + m_HangOffset.z, m_HangLayer.value)) {
                    relativeForce.x = 0;
                    var inputVector = m_Controller.InputVector;
                    inputVector.x = 0;
                    m_Controller.InputVector = inputVector;
                }
            }

            if (Physics.Raycast(m_Transform.position + m_Transform.up * (m_Controller.CapsuleCollider.height + m_HangOffset.y), m_Transform.forward, out m_RaycastHit, 
                                        m_Controller.CapsuleCollider.radius + m_StartHangDistance, m_HangLayer.value)) {
                if (Quaternion.Angle(Quaternion.LookRotation(m_HangNormal), Quaternion.LookRotation(-m_RaycastHit.normal)) < m_AngleThreshold) {
                    m_HangNormal = -m_RaycastHit.normal;
                    m_HangPoint = m_RaycastHit.point;
                }
            }

            // The relative y and z position is fixed.
            var distance = m_Transform.InverseTransformPoint(m_HangPoint);
            relativeForce.y = distance.y - m_Controller.CapsuleCollider.height - m_HangOffset.y;
            relativeForce.z = distance.z - m_HangOffset.z;
            m_Controller.SetPosition(m_Transform.position + (m_Transform.rotation * relativeForce));
            m_Controller.RootMotionForce = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            m_Transform.rotation *= m_Controller.RootMotionRotation;
            m_Controller.RootMotionRotation = Quaternion.identity;

            // While hanging always face towards from the hang point.
            var coverRotation = Quaternion.LookRotation(m_HangNormal);
            var rotation = Quaternion.Slerp(m_Transform.rotation, coverRotation, m_RotationSpeed * Time.fixedDeltaTime);
            m_AnimatorMonitor.SetYawValue(m_Controller.Aiming ? 0 : Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y));
            m_Transform.rotation = rotation;
            return false;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            m_AnimatorMonitor.SetHorizontalInputValue(m_Controller.InputVector.x);

            return false;
        }

        /// <summary>
        /// Perform checks to determine if the character is on the ground.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckGround method?</returns>
        public override bool CheckGround()
        {
            return false;
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
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>False to indicate that the character cannot interact with an item while climbing.</returns>
        public override bool CanInteractItem()
        {
            return false;
        }

        /// <summary>
        /// Can the character have an item equipped while the ability is active?
        /// </summary>
        /// <returns>False to indicate that the character cannot have an item equipped.</returns>
        public override bool CanHaveItemEquipped()
        {
            return false;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>False to indicate that the IK should not be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return false;
        }

        /// <summary>
        /// Should the input vector be local to the character's rotation when ensuring movement is valid?
        /// </summary>
        /// <returns>True if local movement value should be used.</returns>
        public override bool UseLocalMovement()
        {
            return true;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_Rigidbody.useGravity = true;
            m_ItemUnequipped = false;
            m_EndTime = Time.time;
        }
    }
}