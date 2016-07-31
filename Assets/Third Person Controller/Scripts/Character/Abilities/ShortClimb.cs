using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Short Climb ability allows the character to climb over short objects.
    /// </summary>
    public class ShortClimb : Ability
    {
        [Tooltip("The maximum amount of distance that the character can start moving towards the climb position")]
        [SerializeField] protected float m_MoveToClimbDistance = 0.5f;
        [Tooltip("The layers which the character can climb from")]
        [SerializeField] protected LayerMask m_ClimbLayer;
        [Tooltip("The maximum height of the object that the character can climb from")]
        [SerializeField] protected float m_MaxClimbHeight = 3;
        [Tooltip("The normalized speed that the character moves towards the climb point")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f;
        [Tooltip("The depth offset to start the climb animation. If -1 the character will start climbing from the current position")]
        [SerializeField] protected float m_StartClimbOffset = 0.2f;
        [Tooltip("The horizontal offset between the climb point and the point that the character places their hands")]
        [SerializeField] protected float m_HorizontalMatchTargetOffset = -0.3f;
        [Tooltip("The vertical offset between the climb point and the point that the character places their hands")]
        [SerializeField] protected float m_VerticalMatchTargetOffset;
        [Tooltip("The normalized starting value of the character pushing off the climb object")]
        [SerializeField] protected float m_StartMatchTarget = 0.05f;
        [Tooltip("The normalized ending value of the character pushing off the climb object")]
        [SerializeField] protected float m_StopMatchTarget = 0.1f;

        // Internal variables
        private bool m_IsClimbing;
        private bool m_HangClimb;
        private bool m_FromSwim;
        private bool m_TallClimb;
        private bool m_FrameWait;
        private RaycastHit m_RaycastHit;
        private Vector3 m_ClimbPosition;
        private MatchTargetWeightMask m_MatchTargetWeightMask;

        // SharedFields
        private SharedMethod<string, bool, bool> m_ChangeCameraState = null;

        // Component references
        private Rigidbody m_Rigidbody;
        private Hang m_Hang;
        private Swim m_Swim;
        private Transform m_ClimbableObject;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
            m_Hang = GetComponent<Hang>();
            m_Swim = GetComponent<Swim>();

            m_MatchTargetWeightMask = new MatchTargetWeightMask(Vector3.one, 0);
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The ability can be started if it is already active.
            if (IsActive) {
                return true;
            }

            // Fire a raycast in front of the character. If it his an object then there is a chance that the character can climb over the object.
            if (!Physics.Raycast(m_Transform.position + m_Transform.up * 0.1f, m_Transform.forward, out m_RaycastHit, m_MoveToClimbDistance, m_ClimbLayer)) {
                return false;
            }

            var origRaycastHit = m_RaycastHit;

            // Do not climb over an object if it has the ClimbableObject component - this isn't necessary for ShortClimb.
            if (origRaycastHit.collider.GetComponent<ClimbableObject>() != null) {
                return false;
            }

            // If the hang ability is active then allow the character to short climb.
            Vector3 climbPoint;
            if (m_Hang == null || !m_Hang.IsActive) {
                // Only allow the character to climb if they are on the ground.
                if (!m_Controller.Grounded) {
                    return false;
                }

                // There's a chance that the item can be climbed over. Fire a raycast from the maximum climb height down to ensure there is a top to the object.
                var startPoint = origRaycastHit.point + m_Transform.forward * 0.01f; // Add a small amount to ensure the object is hit from the top down.
                startPoint.y = m_Transform.position.y + m_MaxClimbHeight + 0.01f;
                if (!Physics.Raycast(startPoint, -m_Transform.up, out m_RaycastHit, m_MaxClimbHeight)) {
                    return false;
                }

                // Prevent there being an object on top of the climb point.
                startPoint = m_Transform.position + m_Controller.CapsuleCollider.center;
                startPoint.y = m_Transform.position.y + m_MaxClimbHeight + 0.01f;
                if (Physics.Raycast(startPoint, m_Transform.forward, m_MoveToClimbDistance)) {
                    return false;
                }

                climbPoint = m_RaycastHit.point;
                m_HangClimb = false;
                m_FromSwim = m_Swim != null && m_Swim.IsActive;
            } else {
                // The character may have just started to hang in which case the character should not immediately try to climb.
                if (!m_Hang.CanStartAbility(this)) {
                    return false;
                }
                climbPoint = m_Transform.position;
                climbPoint.y = m_Hang.VerticalPosition;
                m_HangClimb = true;
                m_FromSwim = false;
            }

            // If the ability is started then the top of the climb object will be needed so MatchTargets can correctly position the character.
            m_ClimbPosition = climbPoint + Quaternion.LookRotation(-origRaycastHit.normal) * (Vector3.right * m_HorizontalMatchTargetOffset) + m_Transform.up * m_VerticalMatchTargetOffset;
            // The climb animation is based on how tall the climb object is.
            m_TallClimb = m_ClimbPosition.y > m_Transform.position.y + m_Controller.CapsuleCollider.height;
            // Remember the climbable object for collision detection.
            m_ClimbableObject = m_RaycastHit.transform;

            // The hit object can be climbed over.
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            // Reset IsVaulting before calling the base class because the base class will call GetDestinationState and IsVaulting needs to be correct.
            m_IsClimbing = false;
            m_Controller.ForceRootMotion = true;
            m_Controller.Grounded = false;
            m_Controller.StopMovement();

            // Ignore the collisions between the character and the climbable object.
            var climbableColliders = m_ClimbableObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < climbableColliders.Length; ++i) {
                if (climbableColliders[i].enabled) {
                    LayerManager.IgnoreCollision(climbableColliders[i], m_Controller.CapsuleCollider);
                }
            }

            base.AbilityStarted();

            // Stop the ability when the climb is complete.
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorShortClimbComplete", ShortClimbComplete);

            // The camera should stay at the same height and not check for object obstruction to give as smooth movement as possible.
            if (!m_FromSwim) {
                if (m_ChangeCameraState != null) {
                    m_ChangeCameraState.Invoke("StaticHeight", true);
                }
            }

            // Start moving to the short climb point.
            if (m_StartClimbOffset != -1 && !m_HangClimb && !m_FromSwim) {
                Physics.Raycast(m_Transform.position + m_Transform.up * 0.1f, m_Transform.forward, out m_RaycastHit, m_MoveToClimbDistance, m_ClimbLayer);
                var climbPoint = m_RaycastHit.point + m_RaycastHit.normal * (m_Controller.CapsuleCollider.radius + m_StartClimbOffset);
                climbPoint.y = m_Transform.position.y;
                MoveToTarget(climbPoint, m_Transform.rotation, m_MinMoveToTargetSpeed, InPosition);
            } else {
                InPosition();
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (!m_IsClimbing || (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex)) {
                return string.Empty;
            }

            if (m_HangClimb) {
                return "Short Climb.Hang Climb";
            }
            if (m_TallClimb) {
                return "Short Climb.Tall Climb";
            }
            return "Short Climb.Short Climb";
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            // Animator.MatchTarget doesn't work with a nonkinematic Rigidbody.
            m_Rigidbody.isKinematic = true;
            m_IsClimbing = true;
            m_FrameWait = true;

            m_AnimatorMonitor.DetermineStates(true);
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            if (m_IsClimbing) {
                // Wait a frame after climbing has started to ensure the transition to the value state has started.
                if (!m_FrameWait && !m_Animator.IsInTransition(m_AnimatorMonitor.BaseLayerIndex)) {
                    // m_StartMatchTarget cannot be lower then the current normalized time otherwise Animator.MatchTarget will not work.
                    var startMatchTarget = Mathf.Max(m_StartMatchTarget, m_Animator.GetCurrentAnimatorStateInfo(m_AnimatorMonitor.BaseLayerIndex).normalizedTime);
                    // Position the character's left hand on top of the short climb position.
                    if (startMatchTarget < m_StopMatchTarget) {
                        m_Animator.MatchTarget(m_ClimbPosition, Quaternion.identity, AvatarTarget.LeftHand, m_MatchTargetWeightMask, startMatchTarget, m_StopMatchTarget);
                    }
                }
                m_FrameWait = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Move over the short climb object based off of the root motion forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            var relativeForce = Quaternion.Inverse(m_Transform.rotation) * m_Controller.RootMotionForce;
            // Do not allow the character to move backwards when climbing.
            if (relativeForce.z < 0) {
                relativeForce.z = 0;
            }
            m_Controller.SetPosition(m_Transform.position + m_Transform.rotation * relativeForce);
            m_Controller.RootMotionForce = Vector3.zero;

            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
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
        /// <returns>False to prevent the character from interacting with an item.</returns>
        public override bool CanInteractItem()
        {
            return !m_IsClimbing;
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

        /// <summary>
        /// The short climb animation sequence has ended.
        /// </summary>
        private void ShortClimbComplete()
        {
            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            m_IsClimbing = false;
            m_Controller.ForceRootMotion = false;

            // Enable the disabled components again.
            m_Rigidbody.isKinematic = false;
            m_Controller.Grounded = true;

            // Reenable the collisions between the character and the climbable object.
            var climbableColliders = m_ClimbableObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < climbableColliders.Length; ++i) {
                if (climbableColliders[i].enabled) {
                    LayerManager.RevertCollision(climbableColliders[i]);
                }
            }

            // Don't listen for the stop event anymore.
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorShortClimbComplete", ShortClimbComplete);

            // Wait a small amount of time before reverting the camera settings to make the camera movement as smooth as possible.
            if (!m_FromSwim) {
                Scheduler.Schedule(0.2f, RevertCameraSettings);
            }

            base.AbilityStopped();
        }

        /// <summary>
        /// Wait a small delay until the camera settings are reverted. This is done to prevent the camera from trying to look at the character's head
        /// while the character is still changing positions from the short climb ending pose.
        /// </summary>
        private void RevertCameraSettings()
        {
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke("StaticHeight", false);
            }
        }
    }
}