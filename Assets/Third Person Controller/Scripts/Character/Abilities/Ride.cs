using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Ride ability allows the character to ride a RideableObject.
    /// </summary>
    public class Ride : Ability
    {
        // A list of possible states the ride ability can be in.
        private enum RideID { None, Mount, Ride, Dismount }

        [Tooltip("The distance away from the RideableObject that the character can start to mount")]
        [SerializeField] protected float m_MountDistance = 0.75f;
        [Tooltip("The maximum angle that the character can mount onto the RideableObject")]
        [SerializeField] protected float m_MaxMountAngle = 25;
        [Tooltip("The layers that the character can ride on")]
        [SerializeField] protected LayerMask m_RideableLayer;
        [Tooltip("The normalized speed to move to the start climbing position")]
        [SerializeField] protected float m_MinMoveToTargetSpeed = 0.5f;

        // Internal variables
        private RideID m_RideID;
        private RaycastHit m_RaycastHit;
        private bool m_RPGCamera;

        // Component references
        private RideableObject m_RideableObject;
        private Rigidbody m_Rigidbody;
        private Collider[] m_Colliders;
#if !ENABLE_MULTIPLAYER
        private CameraMonitor m_CameraMonitor;
#endif

        /// <summary>
        /// Cache any componetn references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>();
        }

        protected override void Start()
        {
            base.Start();

#if !ENABLE_MULTIPLAYER
            // Store a reference to the camera so the ability can change views if the view is RPG.
            if (!m_IndependentLook.Invoke()) {
                var camera = Utility.FindCamera();
                if (camera != null) {
                    m_CameraMonitor = camera.GetComponent<CameraMonitor>();
                }
            }
#endif
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The character has to be on the ground.
            if (!m_Controller.Grounded) {
                return false;
            }

            // The character has to be near a RideableObject.
            if (Physics.Raycast(m_Transform.position + m_Transform.up * 0.1f, m_Transform.forward, out m_RaycastHit, m_MountDistance, m_RideableLayer.value) &&
                        (m_RideableObject = m_RaycastHit.transform.GetComponent<RideableObject>()) != null) {
                // The character has to be facing the mount position.
                var closestMount = m_RideableObject.GetClosestMount(m_Transform.position);
                if (Vector3.Angle(closestMount.forward, m_Transform.forward) < m_MaxMountAngle) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The ability has been started.
        /// </summary>
        protected override void AbilityStarted()
        {
            m_RideID = RideID.None;
            m_Rigidbody.useGravity = false;
            m_Controller.ForceRootMotion = true;

            base.AbilityStarted();

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorMounted", Mounted);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDismounted", Dismounted);

            // Ignore the collision between the character colliders and the rideable colliders.
            var rideableColliders = m_RideableObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < rideableColliders.Length; ++i) {
                if (rideableColliders[i].enabled) {
                    for (int j = 0; j < m_Colliders.Length; ++j) {
                        if (m_Colliders[j].enabled && m_Colliders[j].gameObject.activeSelf) {
                            LayerManager.IgnoreCollision(rideableColliders[i], m_Colliders[j]);
                        }
                    }
                }
            }

            // Move into mount position.
            var closestMount = m_RideableObject.GetClosestMount(m_Transform.position);
            MoveToTarget(closestMount.position, closestMount.rotation, m_MinMoveToTargetSpeed, InPosition);
        }

        /// <summary>
        /// The character has arrived at the mount position and is ready to play the mount animation.
        /// </summary>
        private void InPosition()
        {
            m_RideID = RideID.Mount;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The character has mounted on the object. Start to ride the object.
        /// </summary>
        private void Mounted()
        {
            if (m_RideID == RideID.Mount) {
                m_RideID = RideID.Ride;

#if !ENABLE_MULTIPLAYER
                // Change to third person mode if the camera view mode is set to RPG.
                if (m_CameraMonitor != null && m_CameraMonitor.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                    m_RPGCamera = true;
                    m_CameraMonitor.ViewMode = CameraMonitor.CameraViewMode.ThirdPerson;
                } else {
                    m_RPGCamera = false;
                }
#endif

                m_RideableObject.Mounted();
                m_Rigidbody.isKinematic = true;
                m_Transform.parent = m_RideableObject.MountParent;
                m_AnimatorMonitor.DetermineStates();
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (m_RideID == RideID.None || (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex)) {
                return string.Empty;
            }

            switch (m_RideID) {
                case RideID.Mount:
                    return (m_RideableObject.UseRightMount ? "Ride.Right Mount" : "Ride.Left Mount");
                case RideID.Ride:
                    return "Ride.Ride";
                case RideID.Dismount:
                    return (m_RideableObject.UseRightMount ? "Ride.Right Dismount" : "Ride.Left Dismount");
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns the normalized time that the state should transition to.
        /// </summary>
        /// <returns>The normalized time that the state should transition to.</returns>
        public override float GetNormalizedTime()
        {
            // When on a RideableObject the normalized time should match the rideable normalized time. This allows the character's animations
            // to stay in sync with the RideableObject's animations.
            if (m_RideID == RideID.Ride) {
                float normalizedTime;
                var rideableAnimator = m_RideableObject.GetComponent<Animator>();
                if (rideableAnimator != null) {
                    if (m_Animator.IsInTransition(0)) {
                        normalizedTime = rideableAnimator.GetNextAnimatorStateInfo(0).normalizedTime % 1;
                    } else {
                        normalizedTime = rideableAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1;
                    }
                    return normalizedTime;
                }
            }
            return base.GetNormalizedTime();
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            if (m_RideID == RideID.None) {
                return false;
            }

            return true;
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
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            if (m_RideID != RideID.Ride) {
                m_Controller.SetPosition(m_Transform.position + m_Controller.RootMotionForce);
                m_Controller.RootMotionForce = Vector3.zero;
            }

            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            if (m_RideID != RideID.Ride) {
                m_Transform.rotation *= m_Controller.RootMotionRotation;
                m_Controller.RootMotionRotation = Quaternion.identity;
            }

            return false;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            // Allow the rider to lean on slopes.
            m_AnimatorMonitor.SetFloatDataValue(Mathf.DeltaAngle(m_Transform.eulerAngles.x, 0));
            return true;
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
        /// Interaction includes being able to Aim, Use or Reload the item.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return m_RideID != RideID.Ride;
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
        /// Can the ability be stopped?
        /// </summary>
        /// <returns>True if the ability can be stopped.</returns>
        public override bool CanStopAbility()
        {
            // Don't stop the ability if the object isn't on flat ground.
            return Mathf.Abs(m_RideableObject.transform.eulerAngles.x) < 0.1f;
        }

        /// <summary>
        /// The character should start to dismount from the object.
        /// </summary>
        private void StartDismount()
        {
            if (m_RideID == RideID.Ride) {
                m_RideID = RideID.Dismount;

                m_RideableObject.Dismount();
                m_Rigidbody.isKinematic = false;
                m_Transform.parent = null;
                m_AnimatorMonitor.DetermineStates();
            }
        }

        /// <summary>
        /// Should the ability proceed with the stop? Returning false allows the abilities to perform any cleanup animations.
        /// </summary>
        /// <returns>True if the ability should be stopped. If false is returned the ability must call AbilityStopped later.</returns>
        protected override bool ShouldProceedWithStop()
        {
            StartDismount();
            return false;
        }

        /// <summary>
        /// The character has dismounted from the object. Stop the ability.
        /// </summary>
        private void Dismounted()
        {
            if (m_RideID == RideID.Dismount) {
                AbilityStopped();
            }
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            // The RideID will still be ride if the ability is force stopped.
            if (m_RideID == RideID.Ride) {
                StartDismount();
            }

            base.AbilityStopped();

            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorMounted", Mounted);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorDismounted", Dismounted);

            m_Rigidbody.useGravity = true;
            m_Controller.ForceRootMotion = false;
            m_RideID = RideID.None;

#if !ENABLE_MULTIPLAYER
            if (m_CameraMonitor != null && m_RPGCamera) {
                m_CameraMonitor.ViewMode = CameraMonitor.CameraViewMode.RPG;
                m_RPGCamera = false;
            }
#endif

            // Reenable the collision between the character colliders and the rideable colliders.
            var rideableColliders = m_RideableObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < rideableColliders.Length; ++i) {
                if (rideableColliders[i].enabled) {
                    LayerManager.RevertCollision(rideableColliders[i]);
                }
            }
        }
    }
}