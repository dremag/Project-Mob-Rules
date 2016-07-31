using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Push ability allows the character to push other objects.
    /// </summary>
    public class Push : Ability
    {
        [Tooltip("The layers that can be pushed")]
        [SerializeField] protected LayerMask m_PushableLayer;
        [Tooltip("Start pushing when the angle between the character and the pushable object is less than this amount")]
        [SerializeField] protected float m_StartPushMaxLookAngle = 15;
        [Tooltip("Start pushing when the distance between the character and the pushable object is less than this amount")]
        [SerializeField] protected float m_StartPushMaxDistance = 0.5f;
        [Tooltip("The normalized speed that the character moves towards the push point")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f;
        [Tooltip("The length of the character's arms")]
        [SerializeField] protected float m_ArmLength = 0.25f;
        [Tooltip("The amount of force to push with")]
        [SerializeField] protected float m_PushForce = 5;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private Vector3 m_PushableObjectCenterOffset;
        private Vector3 m_PushDirection;
        private bool m_InPosition;

        // Component references
        private PushableObject m_PushableObject;
        private Transform m_PushableTransform;
        
        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The character can push if the character is on the ground and a pushable object is near.
            if (m_Controller.Grounded && Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_StartPushMaxDistance, m_PushableLayer.value)) {
                // The character must be mostly looking at the puseable object.
                if (Vector3.Angle(-m_RaycastHit.normal, m_Transform.forward) < m_StartPushMaxLookAngle) {
                    // The pushable object must have the PushableObject component and is able to be pushed.
                    if ((m_PushableObject = (m_PushableTransform = m_RaycastHit.transform).GetComponent<PushableObject>()) != null && m_PushableObject.CanStartPush()) {
                        // The closest point between the character and the pushable object is needed in order to know how far out the character should start pushing from.
                        var closestPoint = m_RaycastHit.collider.ClosestPointOnBounds(m_Transform.position);
                        m_PushableObjectCenterOffset = ((m_RaycastHit.transform.position - closestPoint).magnitude + m_ArmLength) * m_RaycastHit.normal;
                        m_PushDirection = -m_RaycastHit.normal;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            // Prevent the existing velocity from interferring with the push position movement by stopping all movement.
            m_Controller.StopMovement();
            m_Controller.ForceRootMotion = true;

            // Move into push position.
            var targetPosition = m_PushableTransform.position + m_PushableObjectCenterOffset;
            targetPosition.y = m_Transform.position.y;

            MoveToTarget(targetPosition, Quaternion.LookRotation(m_PushDirection), m_MinMoveToTargetSpeed, InPosition);
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

            if (layer == m_AnimatorMonitor.BaseLayerIndex) {
                return "Push.Push";
            } else {
                return "Push.Push";
            }
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            // The character has arrived at the push position. Start pushing.
            m_InPosition = true;
            m_PushableObject.StartPush(m_Transform);
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// Prevent the controller from having control when the MoveToTarget coroutine is updating.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // Stop pushing if the character backs away from the push object.
            if (m_Controller.RelativeInputVector.z < 0) {
                StopAbility();
                return false;
            }

            // Return early if the character isn't in push position yet.
            if (!m_InPosition) {
                return false;
            }

            // There should be no horizontal movement when pushing.
            var relativeInputVector = m_Controller.RelativeInputVector;
            relativeInputVector.x = 0;
            // Determine the amount of force to apply to the push object. The amount of Root Motion force will determine how much force to apply.
            var force = m_PushDirection * m_Controller.RootMotionForce.magnitude * m_PushForce;
            // Stop moving forward if the object cannot be pushed anymore. This will happen if the object runs into a wall.
            if (!m_PushableObject.Push(force)) {
                m_Controller.InputVector = Vector3.zero;
            }
            m_Controller.RootMotionForce = Vector3.zero;

            return true;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public override bool CheckForExternalForces(float xPercent, float zPercent)
        {
            // If there is an external force then leave push.
            if ((Mathf.Abs(m_Controller.Velocity.x * (1 - xPercent)) + Mathf.Abs(m_Controller.Velocity.z * (1 - zPercent))) > 0.5f) {
                StopAbility();
            }
            return false;
        }

        /// <summary>
        /// Move with the PushableObject.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            // Don't use Root Motion to move - just stay with the object.
            var targetPosition = m_PushableTransform.position + m_PushableObjectCenterOffset;
            targetPosition.y = m_Transform.position.y;
            m_Controller.SetPosition(targetPosition);

            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // Always face the puseable object.
            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.LookRotation(m_PushDirection), m_Controller.RotationSpeed * Time.deltaTime);

            return false;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_PushableObject.StopPush();
            m_PushableTransform = null;
            m_PushableObject = null;
            m_Controller.ForceRootMotion = false;
            m_InPosition = false;
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if the Animator should not update to reflect the current state.</returns>
        public override bool HasAnimatorControl()
        {
            return m_InPosition;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            if (layer == m_AnimatorMonitor.UpperLayerIndex) {
                return false;
            }
            return true;
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
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return !m_InPosition;
        }
    }
}