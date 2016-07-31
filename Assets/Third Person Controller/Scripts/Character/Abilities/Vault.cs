using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Vault ability allows the character to vault over other objects.
    /// </summary>
    public class Vault : Ability
    {
        [Tooltip("The maximum amount of distance that the character can start moving towards the vault position")]
        [SerializeField] protected float m_MoveToVaultDistance = 0.5f;
        [Tooltip("The layers which the character can take vault from")]
        [SerializeField] protected LayerMask m_VaultLayer;
        [Tooltip("The maximum height of the object that the character can vault from")]
        [SerializeField] protected float m_MaxVaultHeight = 2;
        [Tooltip("The maximum depth of the object that the character can vault from")]
        [SerializeField] protected float m_MaxVaultDepth = 1;
        [Tooltip("The normalized speed that the character moves towards the vault point")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f;
        [Tooltip("The depth offset to start the vault animation. If -1 the character will start vaulting from the current position")]
        [SerializeField] protected float m_StartVaultOffset = 0.2f;
        [Tooltip("The horizontal offset between the vault point and the point that the character places their hands")]
        [SerializeField] protected float m_HorizontalMatchTargetOffset = -0.2f;
        [Tooltip("The vertical offset between the climb point and the point that the character places their hands")]
        [SerializeField] protected float m_VerticalMatchTargetOffset;
        [Tooltip("The normalized starting value of the character pushing off the vaulting object")]
        [SerializeField] protected float m_StartMatchTarget = 0.05f;
        [Tooltip("The normalized ending value of the character pushing off the vaulting object")]
        [SerializeField] protected float m_StopMatchTarget = 0.1f;

        // Internal variables
        private bool m_IsVaulting;
        private bool m_FrameWait;
        private RaycastHit m_RaycastHit;
        private Vector3 m_VaultPosition;
        private MatchTargetWeightMask m_MatchTargetWeightMask;

        // SharedFields
        private SharedMethod<string, bool, bool> m_ChangeCameraState = null;

        // Component references
        private Rigidbody m_Rigidbody;
        private Transform m_VaultObject;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();

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

            // The character has to be on the ground.
            if (!m_Controller.Grounded) {
                return false;
            }

            // Fire a raycast in front of the character. If it his an object then there is a chance that the character can vault over the object.
            if (!Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_MoveToVaultDistance, m_VaultLayer.value)) {
                return false;
            }

            var origRaycastHit = m_RaycastHit;

            // Ensure the possible vaulting object has a depth less than the MaxVaultDepth.
            if (!Physics.Linecast(m_RaycastHit.point - m_RaycastHit.normal * m_MaxVaultDepth, m_RaycastHit.point, out m_RaycastHit, m_VaultLayer.value)) {
                return false;
            }

            // The collider has to match the previously hit collider.
            if (!m_RaycastHit.collider.Equals(origRaycastHit.collider)) {
                return false;
            }

            // Store the depth so the character can vault over the center of the object.
            var vaultDepth = Mathf.Abs(origRaycastHit.transform.InverseTransformVector(origRaycastHit.point - m_RaycastHit.point).z);

            // There's a chance that the item can be vaulted over. Fire a raycast from the maximum vault height down to ensure there is a top to the object.
            var startPoint = origRaycastHit.point + m_Transform.forward * 0.01f;// Add a small amount to ensure the object is hit from the top down.
            startPoint.y = m_Transform.position.y + m_MaxVaultHeight + 0.01f;
            if (!Physics.Raycast(startPoint, -m_Transform.up, out m_RaycastHit, m_MaxVaultHeight, m_VaultLayer)) {
                return false;
            }

            // The collider has to match the previously hit collider.
            if (!m_RaycastHit.collider.Equals(origRaycastHit.collider)) {
                return false;
            }

            // If the ability is started then the top of the vaulting object will be needed so MatchTargets can correctly position the character.
            m_VaultPosition = m_RaycastHit.point + Quaternion.LookRotation(-origRaycastHit.normal) * (Vector3.forward * vaultDepth / 2) + 
                                    Quaternion.LookRotation(-origRaycastHit.normal) * (Vector3.right * m_HorizontalMatchTargetOffset) +
                                    m_Transform.up * m_VerticalMatchTargetOffset;

            // Remember the vault object for collision management.
            m_VaultObject = origRaycastHit.transform;

            // The hit object can be vaulted over.
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            // Reset IsVaulting before calling the base class because the base class will call GetDestinationState and IsVaulting needs to be correct.
            m_IsVaulting = false;
            m_Controller.ForceRootMotion = true;

            // Ignore the collisions between the character and the vault object.
            var vaultColliders = m_VaultObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < vaultColliders.Length; ++i) {
                if (vaultColliders[i].enabled) {
                    LayerManager.IgnoreCollision(vaultColliders[i], m_Controller.CapsuleCollider);
                }
            }

            base.AbilityStarted();

            // Stop the ability when the vault is complete.
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorVaultComplete", VaultComplete);

            // The camera should stay at the same height and not check for object obstruction to give as smooth movement as possible.
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke("StaticHeight", true);
            }

            // Start moving to the vaulting point.
            if (m_StartVaultOffset != -1) {
                Physics.Raycast(m_Transform.position + m_Controller.CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_MoveToVaultDistance, m_VaultLayer.value);
                var vaultPoint = m_RaycastHit.point + m_RaycastHit.normal * (m_Controller.CapsuleCollider.radius + m_StartVaultOffset);
                vaultPoint.y = m_Transform.position.y;
                MoveToTarget(vaultPoint, m_Transform.rotation, m_MinMoveToTargetSpeed, InPosition);
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
            if (!m_IsVaulting || (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex)) {
                return string.Empty;
            }

            return "Vault.Vault";
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            // Animator.MatchTarget doesn't work with a nonkinematic Rigidbody.
            m_Rigidbody.isKinematic = true;
            m_IsVaulting = true;
            m_FrameWait = true;
            
            m_AnimatorMonitor.DetermineStates(true);
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            if (m_IsVaulting) {
                // Wait a frame after vaulting has started to ensure the transition to the value state has started.
                if (!m_FrameWait && !m_Animator.IsInTransition(m_AnimatorMonitor.BaseLayerIndex)) {
                    // m_StartMatchTarget cannot be lower then the current normalized time otherwise Animator.MatchTarget will not work.
                    var startMatchTarget = Mathf.Max(m_StartMatchTarget, m_Animator.GetCurrentAnimatorStateInfo(m_AnimatorMonitor.BaseLayerIndex).normalizedTime);
                    // Position the character's left hand on top of the vault position.
                    if (startMatchTarget < m_StopMatchTarget) {
                        m_Animator.MatchTarget(m_VaultPosition, Quaternion.identity, AvatarTarget.LeftHand, m_MatchTargetWeightMask, startMatchTarget, m_StopMatchTarget);
                    }
                }
                m_FrameWait = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Move over the vault object based off of the root motion forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            var force = Quaternion.Inverse(m_Transform.rotation) * m_Controller.RootMotionForce;
            // The character should never have a horizontal velocity or go backwards.
            force.x = 0;
            force.z = (force.z < 0 ? 0 : force.z);
            m_Controller.SetPosition(m_Transform.position + (m_Transform.rotation * force));
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
            return !m_IsVaulting;
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
        /// The vault animation sequence has ended.
        /// </summary>
        private void VaultComplete()
        {
            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_IsVaulting = false;
            m_Controller.ForceRootMotion = false;

            // Enable the disabled components again.
            m_Rigidbody.isKinematic = false;

            // Reenable the collisions between the character and the vault object.
            var vaultColliders = m_VaultObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < vaultColliders.Length; ++i) {
                if (vaultColliders[i].enabled) {
                    LayerManager.RevertCollision(vaultColliders[i]);
                }
            }

            // Don't listen for the stop event anymore.
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorVaultComplete", VaultComplete);

            // Wait a small amount of time before reverting the camera settings to make the camera movement as smooth as possible.
            Scheduler.Schedule(0.2f, RevertCameraSettings);
        }

        /// <summary>
        /// Wait a small delay until the camera settings are reverted. This is done to prevent the camera from trying to look at the character's head
        /// while the character is still changing positions from the vault ending pose.
        /// </summary>
        private void RevertCameraSettings()
        {
            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke("StaticHeight", false);
            }
        }
    }
}