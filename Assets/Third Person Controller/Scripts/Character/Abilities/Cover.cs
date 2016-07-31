using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Cover ability allows the character to take cover behind other objects.
    /// </summary>
    public class Cover : Ability
    {
        // The current Animator state that cover should be in.
        private enum CoverIDs { None = -1, StandStill, StandPopLeft, StandPopRight, CrouchStill, CrouchPopLeft, CrouchPopRight, CrouchPopCenterLeft, CrouchPopCenterRight }

        [Tooltip("The maximum amount of distance that the character can take cover from")]
        [SerializeField] protected float m_TakeCoverDistance = 0.5f;
        [Tooltip("The layers which the character can take cover on")]
        [SerializeField] protected LayerMask m_CoverLayer;
        [Tooltip("The normalized speed that the character moves towards the cover point")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f; 
        [Tooltip("The speed that the character can rotate while taking cover")]
        [SerializeField] protected float m_TakeCoverRotationSpeed = 4;
        [Tooltip("The offset between the cover point and the point that the character should take cover at")]
        [SerializeField] protected float m_CoverOffset = 0.05f;
        [Tooltip("The additional offset used when determining if the character can stand")]
        [SerializeField] protected float m_CanStandOffset;
        [Tooltip("The radius of the sphere used to check for cover")]
        [SerializeField] protected float m_CoverSpherecastRadius = 0.05f;
        [Tooltip("Can move and continue to take cover behind objects as long as the new cover object has a normal angle difference less than this amount")]
        [SerializeField] protected float m_CoverAngleThreshold = 1;
        [Tooltip("The normalized cover strafe speed")]
        [SerializeField] protected float m_NormalizedStrafeSpeed = -0.5f;
        [Tooltip("Strafe offset to apply when checking for cover while strafing")]
        [SerializeField] protected float m_StrafeCoverOffset = 0.1f;
        [Tooltip("Was the cover point be predetermined ahead of time?")]
        [SerializeField] protected bool m_UsePredeterminedCoverPoint;
        [Tooltip("If predetermined cover point is true, the target cover point location")]
        [SerializeField] protected Vector3 m_PredeterminedCoverPoint;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private bool m_UseCoverNormal;
        private Vector3 m_CoverNormal;
        private Vector3 m_PopPosition;
        private CoverIDs m_PrevCoverID = CoverIDs.None;
        private CoverIDs m_CurrentCoverID = CoverIDs.None;
        private bool m_LookRight;
        private bool m_AgainstCover;
        private bool m_StandingCover;
        private bool m_ShouldPopFromCover;
        private bool m_CanMove;
        private bool m_CanTogglePop;
        private bool m_PoppedFromCover;

        // SharedFields
        private SharedMethod<bool> m_AIAgent = null;
        private SharedMethod<string, bool, bool> m_ChangeCameraState = null;

        // Component references
        private HeightChange m_HeightChange;

        // External properties
        public Vector3 PredeterminedCoverPoint { set { m_PredeterminedCoverPoint = value; m_UsePredeterminedCoverPoint = true; } }

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_HeightChange = GetComponent<HeightChange>();
        }
        
        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The character can take cover if on the ground and near a cover object.
            return m_Controller.Grounded && Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_TakeCoverDistance, m_CoverLayer.value);
        }

        /// <summary>
        /// Can the specified ability be started?
        /// </summary>
        /// <param name="ability">The ability that is trying to start.</param>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility(Ability ability)
        {
            // The HeightChange ability cannot start if there is only low cover available.
            if (ability is HeightChange && !m_StandingCover) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_PrevCoverID = m_CurrentCoverID = CoverIDs.None;
            m_Controller.ForceRootMotion = true;

            m_UseCoverNormal = false;
            m_PoppedFromCover = false;
            m_CoverNormal = m_RaycastHit.normal;

            // Start moving to the cover point.
            Vector3 coverPoint;
            if (m_UsePredeterminedCoverPoint) {
                coverPoint = m_PredeterminedCoverPoint;
            } else {
                coverPoint = m_RaycastHit.point + m_RaycastHit.normal * (m_Controller.CapsuleCollider.radius + m_CoverOffset);
            }
            coverPoint.y = m_Transform.position.y;
            MoveToTarget(coverPoint, m_Transform.rotation, m_MinMoveToTargetSpeed, InPosition);

            // Register for any interested events.
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAbilityHeightChange", OnHeightChange);
            EventHandler.RegisterEvent(m_GameObject, "OnControllerStartAim", OnStartAim);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (m_CurrentCoverID == CoverIDs.None || (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex && !m_AnimatorMonitor.ItemUsesAbilityLayer(this, layer))) {
                return string.Empty;
            }
            if (layer == m_AnimatorMonitor.BaseLayerIndex) {
                var coverID = m_CurrentCoverID;
                if (m_PrevCoverID != CoverIDs.None && !m_UseCoverNormal) {
                    coverID = m_PrevCoverID;
                }
                switch (coverID) {
                    case CoverIDs.StandStill:
                        if (m_UseCoverNormal) {
                            return "Cover.Stand Strafe";
                        }
                        return string.Format("Cover.Take Standing Cover {0}", m_LookRight ? "Right" : "Left");
                    case CoverIDs.CrouchStill:
                        if (m_UseCoverNormal) {
                            return "Cover.Crouch Strafe";
                        }
                        return string.Format("Cover.Take Crouching Cover {0}", m_LookRight ? "Right" : "Left");
                    case CoverIDs.StandPopLeft:
                        return "Cover.Stand Pop Left Hold";
                    case CoverIDs.CrouchPopLeft:
                        return "Cover.Crouch Pop Left Hold";
                    case CoverIDs.StandPopRight:
                        return "Cover.Stand Pop Right Hold";
                    case CoverIDs.CrouchPopRight:
                        return "Cover.Crouch Pop Right Hold";
                    case CoverIDs.CrouchPopCenterRight:
                        return "Cover.Crouch Pop Center Right Hold";
                    case CoverIDs.CrouchPopCenterLeft:
                        return "Cover.Crouch Pop Center Left Hold";
                }
            } else if (!m_Controller.Aiming) {
                return "Cover.Idle";
            }
            return string.Empty;
        }

        /// <summary>
        /// Can the AnimatorMonitor make state transitions?
        /// </summary>
        /// <param name="layer">The layer to check against.</param>
        /// <returns>True if the AnimatorMonitor should be able to make state transitions.</returns>
        public override bool AllowStateTransitions(int layer)
        {
            if (layer == m_AnimatorMonitor.BaseLayerIndex) {
                // Do not allow AnimatorMonitor state transitions after the lower body has arrived in cover. The ability will take care of all transitions.
                if (m_UseCoverNormal || m_PoppedFromCover) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Should the ability override the item's high priority state?
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>True if the ability should override the item state.</returns>
        public override bool OverrideItemState(int layer)
        {
            return layer == m_AnimatorMonitor.BaseLayerIndex;
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            // Determine if the cover is high cover by firing a ray from the character's upper body.
            m_StandingCover = Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center + (m_Transform.up * (m_Controller.CapsuleCollider.height / 2 + m_CanStandOffset)), m_Transform.forward, 
                                                out m_RaycastHit, (m_Controller.CapsuleCollider.radius + m_CoverOffset) * 2, m_CoverLayer.value);
            // Determine which direction to look.
            m_LookRight = !IntersectObject(-(m_Controller.CapsuleCollider.radius + m_StrafeCoverOffset * 2), m_Controller.CapsuleCollider.height / 2, m_CoverLayer.value);

            // Take lower cover if crouching.
            if (m_StandingCover && m_HeightChange != null) {
                m_StandingCover = !m_HeightChange.IsActive;
            }
            if (m_StandingCover) {
                m_CurrentCoverID = CoverIDs.StandStill;
            } else {
                m_CurrentCoverID = CoverIDs.CrouchStill;
            }

            m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
            m_AnimatorMonitor.DetermineStates();

            // The character has arrived in cover. The character no longer needs to move and the user has control again.
            m_AgainstCover = false;
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // Leave cover if the player tries to move backwards while in cover.
            if (m_Controller.RelativeInputVector.z < -0.1f) {
                StopAbility();
            }

            return m_CurrentCoverID != CoverIDs.None;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public override bool CheckForExternalForces(float xPercent, float zPercent)
        {
            // If there is an external force then get out of cover.
            if (((m_CurrentCoverID != CoverIDs.CrouchStill && m_CurrentCoverID != CoverIDs.StandStill) || Quaternion.Angle(Quaternion.LookRotation(m_CoverNormal), m_Transform.rotation) < m_CoverAngleThreshold) &&
                (Mathf.Abs(m_Controller.Velocity.x * (1 - xPercent)) + Mathf.Abs(m_Controller.Velocity.z * (1 - zPercent))) > 0.5f) {
                StopAbility();
            }
            return true;
        }

        /// <summary>
        /// Ensure the current movement direction is valid.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckMovement method?</returns>
        public override bool CheckMovement()
        {
            return false;
        }

        /// <summary>
        /// Only allow movement on the relative x axis to prevent the character from moving away from the cover point.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            // Return to the exact position that the character popped from.
            if (m_PoppedFromCover && !m_ShouldPopFromCover && (m_CurrentCoverID == CoverIDs.StandStill || m_CurrentCoverID == CoverIDs.CrouchStill)) {
                var position = m_Transform.position;
                var moveDelta = Mathf.Max(m_Controller.RootMotionForce.magnitude, 0.01f);
                position.x = Mathf.MoveTowards(position.x, m_PopPosition.x, moveDelta);
                position.z = Mathf.MoveTowards(position.z, m_PopPosition.z, moveDelta);
                m_Controller.SetPosition(position);
                m_Controller.RootMotionForce = Vector3.zero;
                // The character is done returning to cover when they have arrived at the pop position.
                if ((m_Transform.position - m_PopPosition).sqrMagnitude < 0.001f) {
                    m_Controller.SetPosition(m_PopPosition);
                    m_UseCoverNormal = m_CanTogglePop = true;
                    m_PoppedFromCover = false;
                }
                return false;
            }

            var coverNormalRotation = Quaternion.LookRotation(m_CoverNormal);
            var relativeForce = Quaternion.Inverse(coverNormalRotation) * m_Controller.RootMotionForce;
            // While in cover the character can only strafe if there continues to be cover in the direction that the character should strafe.
            if (m_UseCoverNormal) {
                m_CanMove = m_AIAgent.Invoke();
                if (!m_CanMove && Mathf.Abs(m_Controller.RelativeInputVector.x) > 0) {
                    m_CanMove = IntersectObject((m_Controller.CapsuleCollider.radius + m_StrafeCoverOffset) * (m_Controller.RelativeInputVector.x > 0 ? -1 : 1), 0, m_CoverLayer.value);
                }
                if (!m_CanMove) {
                    relativeForce.x = 0;
                }
                relativeForce.z = 0;
            }
            m_Controller.RootMotionForce = coverNormalRotation * relativeForce;

            // Don't use the velocity if popping.
            if (m_CurrentCoverID != CoverIDs.CrouchStill && m_CurrentCoverID != CoverIDs.StandStill) {
                m_Controller.SetPosition(m_Transform.position + m_Controller.RootMotionForce);
                m_Controller.RootMotionForce = Vector3.zero;
            }

            // Stop taking standing cover if there is only crouching cover available.
            if (m_HeightChange != null && !m_HeightChange.IsActive && m_UseCoverNormal) {
                var canStand = Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center + (m_Transform.up * (m_Controller.CapsuleCollider.height / 2 + m_CanStandOffset)), -m_CoverNormal, 
                                                    out m_RaycastHit, m_Controller.CapsuleCollider.radius + m_CoverOffset + 0.1f, m_CoverLayer.value);
                if (canStand != m_StandingCover) {
                    m_StandingCover = canStand;
                    m_CurrentCoverID += (int)CoverIDs.CrouchStill * (canStand ? -1 : 1);
                    m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
                    m_AnimatorMonitor.DetermineStates();
                }
            }
            return true;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            m_Transform.rotation *= m_Controller.RootMotionRotation;
            m_Controller.RootMotionRotation = Quaternion.identity;

            // While in cover always face away from the hit point.
            if (m_UseCoverNormal) {
                if (Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, -m_Transform.forward, out m_RaycastHit, m_CoverOffset + m_Controller.CapsuleCollider.radius / 2 + 0.01f, m_CoverLayer.value)) {
                    if (Quaternion.Angle(Quaternion.LookRotation(m_CoverNormal), Quaternion.LookRotation(m_RaycastHit.normal)) < m_CoverAngleThreshold) {
                        m_CoverNormal = m_RaycastHit.normal;
                    }
                }

                var coverRotation = Quaternion.LookRotation(m_CoverNormal);
                var rotation = Quaternion.Slerp(m_Transform.rotation, coverRotation, m_TakeCoverRotationSpeed * Time.fixedDeltaTime);
                m_AnimatorMonitor.SetYawValue(m_Controller.Aiming ? 0 : Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y));
                m_AgainstCover = Mathf.Abs(Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y)) < m_CoverAngleThreshold;
                m_Transform.rotation = rotation;
            }
            return false;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            m_AnimatorMonitor.SetForwardInputValue(0, 0.1f);
            if (Mathf.Abs(m_Controller.RelativeInputVector.x) > 0.1f) {
                m_LookRight = m_Controller.RelativeInputVector.x < 0;
            }
            m_AnimatorMonitor.SetFloatDataValue(m_LookRight ? 1 : -1, 0.1f);

            switch (m_CurrentCoverID) {
                case CoverIDs.StandStill:
                case CoverIDs.CrouchStill:
                    m_AnimatorMonitor.SetHorizontalInputValue(m_CanMove ? m_Controller.InputVector.x * m_NormalizedStrafeSpeed : 0, 0.1f);
                    if (m_CanTogglePop && m_ShouldPopFromCover && m_AgainstCover) {
                        var popCoverID = CoverIDs.None;
                        var popOffset = m_Controller.CapsuleCollider.radius + m_StrafeCoverOffset + m_CoverOffset;
                        var canStand = false;
                        if (!m_StandingCover) {
                            canStand = !Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center + (m_Transform.up * (m_Controller.CapsuleCollider.height / 2 + m_CanStandOffset)), -m_CoverNormal, 
                                                            out m_RaycastHit, m_Controller.CapsuleCollider.radius + m_CoverOffset + 0.1f, m_CoverLayer.value);
                        }
                        if (canStand) { // Can the character pop in the center?
                            popCoverID = m_LookRight ? CoverIDs.CrouchPopCenterRight : CoverIDs.CrouchPopCenterLeft;
                        } else if (!IntersectObject(popOffset, m_StandingCover ? m_Controller.CapsuleCollider.height / 2 : 0, -1)) { // Can the character pop to the right?
                            popCoverID = m_StandingCover ? CoverIDs.StandPopRight : CoverIDs.CrouchPopRight;
                        } else if (!IntersectObject(-popOffset, m_StandingCover ? m_Controller.CapsuleCollider.height / 2 : 0, -1)) { // Can the character pop to the left?
                            popCoverID = m_StandingCover ? CoverIDs.StandPopLeft : CoverIDs.CrouchPopLeft;
                        }

                        if (popCoverID != CoverIDs.None) {
                            m_CurrentCoverID = popCoverID;
                            m_UseCoverNormal = m_CanTogglePop = false;
                            m_PoppedFromCover = true;
                            m_PopPosition = m_Transform.position;
                            m_AnimatorMonitor.SetStateValue((int)popCoverID);
                        }
                    }
                    break;
                case CoverIDs.StandPopLeft:
                case CoverIDs.StandPopRight:
                case CoverIDs.CrouchPopLeft:
                case CoverIDs.CrouchPopRight:
                case CoverIDs.CrouchPopCenterRight:
                case CoverIDs.CrouchPopCenterLeft:
                    if (m_CanTogglePop && !m_ShouldPopFromCover) {
                        m_PrevCoverID = m_CurrentCoverID;
                        if (m_StandingCover) {
                            m_CurrentCoverID = CoverIDs.StandStill;
                        } else {
                            m_CurrentCoverID = CoverIDs.CrouchStill;
                        }
                        m_CanTogglePop = false;
                        if (m_ChangeCameraState != null) {
                            m_ChangeCameraState.Invoke(GetCoverCameraState(m_PrevCoverID), false);
                        }
                        m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// Does the spherecast hit an object?
        /// </summary>
        /// <param name="horizontalOffset">The horizontal offset to add to the character's position.</param>
        /// <param name="verticalOffset">The vertical offset to add to the character's position.</param>
        /// <param name="layerValue">The layermask to check against.</param>
        /// <returns>True if there still exists cover in the desired direction.</returns>
        private bool IntersectObject(float horizontalOffset, float verticalOffset, int layerValue)
        {
            // Fire a sphere to the left or the right of the character. Return if the spherecast hits an object.
            var position = (m_Transform.TransformPoint(horizontalOffset, verticalOffset, 0) + m_Controller.CapsuleCollider.center) + m_CoverNormal * m_CoverSpherecastRadius;
            return Physics.SphereCast(position, m_CoverSpherecastRadius, -m_CoverNormal, out m_RaycastHit, m_Controller.CapsuleCollider.radius + m_CoverSpherecastRadius + m_CoverOffset * 2 + 0.1f, layerValue);
        }

        /// <summary>
        /// Callback when the character starts aiming.
        /// </summary>
        private void OnStartAim()
        {
            OnAim(true);
        }

        /// <summary>
        /// Callback when the cahracter starts or stops aiming.
        /// </summary>
        /// <param name="aim">Is the character aiming?</param>
        private void OnAim(bool aim)
        {
            m_ShouldPopFromCover = aim;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// An item can be used if the character can pop from cover.
        /// </summary>
        /// <returns>True if the character can pop from cover.</returns>
        public override bool CanUseItem()
        {
            return m_CurrentCoverID != CoverIDs.CrouchStill && m_CurrentCoverID != CoverIDs.StandStill;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer) {
            if (layer == m_AnimatorMonitor.UpperLayerIndex) {
                return m_CurrentCoverID != CoverIDs.CrouchStill && m_CurrentCoverID != CoverIDs.StandStill;
            }
            return true;
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
        /// The height change ability has started or stopped. Change cover states to reflect the change.
        /// </summary>
        /// <param name="active">Did the ability start?</param>
        private void OnHeightChange(bool active)
        {
            // Adding CoverIDs.CrouchStill will toggle the state ids between standing and crouching.
            m_CurrentCoverID += (int)CoverIDs.CrouchStill * (active ? 1 : -1);
            m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
            m_StandingCover = !active;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The character has moved into cover position.
        /// </summary>
        private void InCover()
        {
            m_UseCoverNormal = m_CanTogglePop = true;
        }

        /// <summary>
        /// The character has finished popping from cover.
        /// </summary>
        private void PoppedFromCover()
        {
            m_CanTogglePop = true;
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke(GetCoverCameraState(m_CurrentCoverID), true);
            }
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_UsePredeterminedCoverPoint = false;
            m_ShouldPopFromCover = false;
            m_Controller.ForceRootMotion = false;
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke(GetCoverCameraState(m_CurrentCoverID), false);
            }
            m_CurrentCoverID = CoverIDs.None;

            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnAbilityHeightChange", OnHeightChange);
            EventHandler.UnregisterEvent(m_GameObject, "OnControllerStartAim", OnStartAim);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
        }

        /// <summary>
        /// Returns the CameraState for the given CoverID.
        /// </summary>
        /// <param name="coverID">The CoverID to retrieve the state of.</param>
        /// <returns>The CameraState for the given CoverID.</returns>
        private string GetCoverCameraState(CoverIDs coverID)
        {
            if (coverID == CoverIDs.CrouchPopCenterLeft) {
                return "CoverCenterLeft";
            } else if (coverID == CoverIDs.CrouchPopCenterRight) {
                return "CoverCenterRight";
            } else if (coverID == CoverIDs.CrouchPopLeft || coverID == CoverIDs.StandPopLeft) {
                return "CoverLeft";
            }
            return "CoverRight";
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if the Animator should not update to reflect the current state.</returns>
        public override bool HasAnimatorControl()
        {
            return m_CurrentCoverID != CoverIDs.None;
        }
    }
}