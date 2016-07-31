using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Ledge Strafe ability allows the character to place their back against a wall and strafe.
    /// </summary>
    public class LedgeStrafe : Ability
    {
        [Tooltip("The maximum amount of distance that the character can start to strafe from")]
        [SerializeField] protected float m_StartStrafeDistance = 0.5f;
        [Tooltip("The layers that can be used to strafe on")]
        [SerializeField] protected LayerMask m_StrafeLayer;
        [Tooltip("The normalized speed to move to the strafe point")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f; 
        [Tooltip("The offset between the strafe point and the point that the character should strafe")]
        [SerializeField] protected float m_StrafeOffset;
        [Tooltip("Can move and continue to strafe behind objects as long as the new strafe object has a normal angle difference less than this amount")]
        [SerializeField] protected float m_StrafeAngleThreshold;
        [Tooltip("The speed that the character can rotate while strafing")]
        [SerializeField] protected float m_StrafeRotationSpeed;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private bool m_RaycastDidHit;
        private Vector3 m_StrafeNormal;
        private bool m_CanStrafe;
        
        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The character can strafe if on the ground and near a strafe object.
            return m_Controller.Grounded && Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_StartStrafeDistance, m_StrafeLayer.value);
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            // While moving to the strafe point the player no longer has control until the character has arrived.
            m_AnimatorMonitor.SetHorizontalInputValue(0, 0);
            m_Controller.StopMovement();
            m_Controller.ForceRootMotion = true;
            m_CanStrafe = false;

            // Start moving to the strafe point.
            m_StrafeNormal = m_RaycastHit.normal;
            var targetPoint = m_RaycastHit.point + m_RaycastHit.normal * (m_Controller.CapsuleCollider.radius + m_StrafeOffset);
            targetPoint.y = m_Transform.position.y;

            MoveToTarget(targetPoint, m_Transform.rotation, m_MinMoveToTargetSpeed, InPosition);
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            m_CanStrafe = true;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (!m_CanStrafe || (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex && !m_AnimatorMonitor.ItemUsesAbilityLayer(this, layer))) {
                return string.Empty;
            }
            return "Ledge Strafe.Movement";
        }

        /// <summary>
        /// Prevent the controller from having control when the MoveToTarget coroutine is updating.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // Leave strafe if the player tries to move backwards while in cover or is no longer on the ground.
            if (m_Controller.RelativeInputVector.z < -0.1f || !m_Controller.Grounded) {
                StopAbility();
                return true;
            }
            return m_CanStrafe;
        }

        /// <summary>
        /// Only allow movement on the relative x axis to prevent the character from moving away from the strafe object.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            var coverNormalRotation = Quaternion.LookRotation(m_StrafeNormal);
            var relativeForce = Quaternion.Inverse(coverNormalRotation) * m_Controller.RootMotionForce;
            relativeForce.z = 0;
            if ((m_RaycastDidHit = Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, -m_StrafeNormal, out m_RaycastHit, 
                                                        m_StrafeOffset + (m_Controller.CapsuleCollider.radius * 2), m_StrafeLayer.value))) {
                // Keep the character sticking to the wall by applying a small backward force.
                if (m_RaycastHit.distance > m_StrafeOffset + m_Controller.CapsuleCollider.radius) {
                    relativeForce.z -= 0.01f;
                }
            }
            m_Controller.RootMotionForce = coverNormalRotation * relativeForce;
            return true;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // Ensure the strafe normal is correct.
            if (m_RaycastDidHit) {
                if (Quaternion.Angle(Quaternion.LookRotation(m_StrafeNormal), Quaternion.LookRotation(m_RaycastHit.normal)) < m_StrafeAngleThreshold) {
                    m_StrafeNormal = m_RaycastHit.normal;
                }
            }
            // Rotate to face in the same direction as the strafe normal.
            var rotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.LookRotation(m_StrafeNormal), m_StrafeRotationSpeed * Time.deltaTime);
            m_AnimatorMonitor.SetYawValue(m_Controller.Aiming ? 0 : Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y));
            m_Controller.SetRotation(rotation);
            return false;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            // The character may not be able to move if there is no strafe object to back up against.
            var canMove = CanMoveDirection(m_Controller.InputVector.x < 0);
            m_AnimatorMonitor.SetHorizontalInputValue(canMove ? -m_Controller.InputVector.x : 0);
            m_AnimatorMonitor.SetForwardInputValue(0);

            return false;
        }

        /// <summary>
        /// Can the character move in the requested direction?
        /// </summary>
        /// <param name="right">Move in the relative right direction?</param>
        /// <returns>True if the character can move.</returns>
        private bool CanMoveDirection(bool right)
        {
            var position = m_Transform.TransformPoint((m_Controller.CapsuleCollider.radius + m_Controller.SkinWidth) * (right ? 1 : -1), 0, 0) + m_Controller.CapsuleCollider.center;
            return Physics.Raycast(position, -m_Transform.forward, out m_RaycastHit, m_Controller.CapsuleCollider.radius + m_Controller.SkinWidth + m_StrafeOffset * 2, m_StrafeLayer.value);
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
            m_CanStrafe = false;

            m_Controller.ForceRootMotion = false;
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
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return !m_CanStrafe;
        }
    }
}