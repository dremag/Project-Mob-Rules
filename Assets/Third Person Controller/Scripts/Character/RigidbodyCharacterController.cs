using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System.Collections;
using System.Collections.Generic;
using Opsive.ThirdPersonController.Abilities;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The RigidbodyCharacterController controls all of the character's movements. At a higher level it has three different types of movement: combat, adventure, and top down movement.
    /// In combat movement the camera is always behind the character and it allows the character to strafe and move backwards. In adventure movement the character can move forward
    /// in any direction and allows for a free camera movement. Top down movement moves relative to the camera and rotates to always look at the mouse.
    /// The controller uses a rigidbody and will respond to external forces.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
#if ENABLE_MULTIPLAYER
    public class RigidbodyCharacterController : NetworkBehaviour
#else
    public class RigidbodyCharacterController : MonoBehaviour
#endif
    {
        // Combat movement allows the character to move backwards and strafe. If the character has a camera following it then the character will always be
        // facing in the same direction as the camera. Adventure movement always moves the character in the direction they are facing and 
        // the camera can be facing any direction. Top down movement moves rotates the character in the direction of the mouse and moves relative to the camera.
        // RPG is a blend between Combat and Adventure movement types. Psuedo3D is used for 2.5D games. Point and Click moves the character according to the
        // point clicked. The PointClickControllerHandler is required. Four Legged allows movement for the four legged generic characters.
        public enum MovementType { Combat, Adventure, TopDown, RPG, Pseudo3D, PointClick, FourLegged }

        // Allows the character's position to be constrained on the x or z axis.
        public enum MovementConstraint { None, RestrictX, RestrictZ, RestrictXZ }

        [Tooltip("The movement type")]
        [SerializeField] protected MovementType m_MovementType = MovementType.Combat;
        [Tooltip("Should root motion be used?")]
        [SerializeField] protected bool m_UseRootMotion = true;
        [Tooltip("The multiplier of the root motion movement")]
        [SerializeField] protected float m_RootMotionSpeedMultiplier = 1;
        [Tooltip("The speed that the character can rotate")]
        [SerializeField] protected float m_RotationSpeed = 6;
        [Tooltip("The speed that the character can rotate while aiming")]
        [SerializeField] protected float m_AimRotationSpeed = 15;
        [Tooltip("Do not rotate if the character minus look angle is less than the specified threshold")]
        [SerializeField] protected float m_TorsoLookThreshold;
        [Tooltip("Is the character a local coop character?")]
        [SerializeField] protected bool m_LocalCoopCharacter;
        [Tooltip("Should the character stay aligned to the ground rotation?")]
        [SerializeField] protected bool m_AlignToGround;
        [Tooltip("If aligning to ground, specifies the speed that the character can rotate to align to the ground")]
        [SerializeField] protected float m_AlignToGroundRotationSpeed = 15;
        [Tooltip("If aligning to ground, specifies the depth offset when checking the ground normal")]
        [SerializeField] protected float m_AlignToGroundDepthOffset = 0.5f;
        [Tooltip("The speed while on the ground and not using root motion")]
        [SerializeField] protected Vector3 m_GroundSpeed = new Vector3(1, 0, 1);
        [Tooltip("The speed while in the air")]
        [SerializeField] protected Vector3 m_AirSpeed = new Vector3(0.5f, 0, 0.5f);
        [Tooltip("The amount of dampening force to apply while on the ground")]
        [SerializeField] protected float m_GroundDampening = 0.15f;
        [Tooltip("The amount of dampening force to apply while in the air")]
        [SerializeField] protected float m_AirDampening = 0.15f;
        [Tooltip("Should the top down and pseudo-2D movement types look in the direction of the movement?")]
        [SerializeField] protected bool m_LookInMoveDirection;
        [Tooltip("Force which keeps the character sticking to the ground while stationary")]
        [SerializeField] protected float m_GroundStickiness = 6;
        [Tooltip("The additional width of the character's collider")]
        [SerializeField] protected float m_SkinWidth = 0.08f;
        [Tooltip("An extra width used to determine if the player is on the ground while on a moving platform")]
        [SerializeField] protected float m_MovingPlatformSkinWidth = 0.5f;
        [Tooltip("Optionally restrict the x or z position")]
        [SerializeField] protected MovementConstraint m_MovementConstraint;
        [Tooltip("If restricting the x axis, the minimum x position the character can occupy")]
        [SerializeField] protected float m_MinXPosition;
        [Tooltip("If restricting the x axis, the maximum x position the character can occupy")]
        [SerializeField] protected float m_MaxXPosition;
        [Tooltip("If restricting the z axis, the minimum z position the character can occupy")]
        [SerializeField] protected float m_MinZPosition;
        [Tooltip("If restricting the z axis, the maximum z position the character can occupy")]
        [SerializeField] protected float m_MaxZPosition;
        [Tooltip("The maximum height that the character can step")]
        [SerializeField] protected float m_MaxStepHeight = 0.2f;
        [Tooltip("The offset relative to the character's position that should be used for checking if a step exists")]
        [SerializeField] protected Vector3 m_StepOffset = new Vector3(0, 0.07f, 0.1f);
        [Tooltip("The vertical speed that the character moves when taking a step")]
        [SerializeField] protected float m_StepSpeed = 1f;
        [Tooltip("The maximum slope angle that the character can move on (in degrees)")]
        [SerializeField] protected float m_SlopeLimit = 30f;
        [Tooltip("A -1 to 1 threshold for when the character should stop moving if another object collides with the character. A value of 1 indicates the object is directly " +
                 "in front of the character's move direction while a value of -1 indicates the object is directly behind the character's move direction")]
        [SerializeField] protected float m_StopMovementThreshold = 0.5f;
        [Tooltip("Should the character always aim?")]
        [SerializeField] protected bool m_AlwaysAim;
        [Tooltip("Should the character change into a combat movement type while aiming?")]
        [SerializeField] protected bool m_CombatMovementOnAim;
        [Tooltip("The character will rotate to face in the direction of the camera when using an item which requires aiming. If the character is not facing in the correct direction when trying " +
                 "to use an item, they will automatically rotate until an angle less than this value")]
        [SerializeField] protected float m_ItemUseRotationThreshold = 1;
        [Tooltip("The duration that the character should forcibly use the item")]
        [SerializeField] protected float m_ItemForciblyUseDuration = 0.3f;
        [Tooltip("The duration that the character should forcibly use the dual wielded item")]
        [SerializeField] protected float m_DualWieldItemForciblyUseDuration = 0.3f;
        [Tooltip("Optionally specify the CapsuleCollider if the current GameObject does not have one attached")]
        [SerializeField] protected CapsuleCollider m_CapsuleCollider;
        [Tooltip("Any other colliders that should also be affected by the friction material change")]
        [SerializeField] protected Collider[] m_LinkedColliders;
        [Tooltip("The friction material to use while on the ground and idle")]
        [SerializeField] protected PhysicMaterial m_GroundedIdleFrictionMaterial;
        [Tooltip("The friction material to use while on the ground and moving")]
        [SerializeField] protected PhysicMaterial m_GroundedMovingFrictionMaterial;
        [Tooltip("The friction material to use while stepping")]
        [SerializeField] protected PhysicMaterial m_StepFrictionMaterial;
        [Tooltip("The friction material to use while on a slope")]
        [SerializeField] protected PhysicMaterial m_SlopeFrictionMaterial;
        [Tooltip("The friction material to use while in the air")]
        [SerializeField] protected PhysicMaterial m_AirFrictionMaterial;
        [Tooltip("Abilities allow for extra functionalities such as cover or interact")]
        [SerializeField] protected Ability[] m_Abilities = new Ability[0];

        // Internal varaibles
        private Vector3 m_InputVector;
        private Quaternion m_LookRotation;
#if ENABLE_MULTIPLAYER
        [SyncVar(hook = "SetAimSync")]
#endif
        private bool m_Aim;
#if ENABLE_MULTIPLAYER
        [SyncVar]
#endif
        private bool m_ForceAim;
        private bool m_IsAiming;
        private bool m_IsForcedAiming;
        private bool m_PrevAlwaysAim;
        private MovementType m_PrevMovementType;

        private bool m_AIAgent;
        private bool m_Grounded;
        private bool m_Moving;
        private bool m_Rotating;
        private Vector3 m_Velocity;
        private Vector3 m_RootMotionForce;
        private Vector3 m_PrevRootMotionForce;
        private Quaternion m_RootMotionRotation = Quaternion.identity;
        private bool m_ForceRootMotion;
        private bool m_ForceItemRootMotion;
        private float m_PrevYRotation;
        private float m_Slope = -1;
        private bool m_Stepping;
        private Vector3 m_GroundVelocity;
        private Vector3 m_PrevGroundVelocity;
        private Vector3 m_AirVelocity;
        private Vector3 m_PrevAirVelocity;
        private float m_PrevGroundHeight;
        private ScheduledEvent m_ForcedItemUseEvent;
        private WaitForEndOfFrame m_EndOfFrame = new WaitForEndOfFrame();

        private RaycastHit m_RaycastHit;
        private RaycastHit m_GroundRaycastHit;
        private Transform m_Platform;
        private Vector3 m_PlatformPosition;
        private float m_PrevPlatformAngle;

        private float m_CapsuleColliderHeight;
        private Vector3 m_CapsuleColliderCenter;
        private RigidbodyConstraints m_PrevRigidbodyConstraints;

#if UNITY_EDITOR
        // Used by the editor to keep the ability list selection.
        [SerializeField] protected int m_SelectedAbility = -1;
        public int SelectedAbility { get { return m_SelectedAbility; } set { m_SelectedAbility = value; } }
#endif

        /// <summary>
        /// Maps a collision transform to the collision point.
        /// </summary>
        private class CollisionPoint
        {
            // Internal variables.
            private Transform m_Transform;
            private Vector3 m_Point;

            // Exposed properties
            public Transform Transform { get { return m_Transform; } }
            public Vector3 Point { get { return m_Point; } }

            /// <summary>
            /// Initializes the default values.
            /// </summary>
            /// <param name="transform">The referenced transform.</param>
            /// <param name="point">The point to set.</param>
            public void Initialize(Transform transform, Vector3 point)
            {
                m_Transform = transform;
                m_Point = point;
            }
        }
        private List<CollisionPoint> m_CollisionPoints = new List<CollisionPoint>();
        private HashSet<Transform> m_CollisionSet = new HashSet<Transform>();
        private Item m_ItemCollision;
        private Vector3 m_ItemCollisionDirection;

        // SharedFields
        private SharedMethod<bool> m_IsSwitchingItems = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;
        private bool SharedProperty_Aim { get { return Aim; } }

        // Component references
        private GameObject m_GameObject;
        private Transform m_Transform;
        private Rigidbody m_Rigidbody;
        private AnimatorMonitor m_AnimatorMonitor;

        // Exposed properties
        public MovementType Movement { get { return m_MovementType; } set { m_MovementType = value; } }
        public bool Moving { get { return m_Moving; } set { if (m_Moving != value) { m_Moving = value; m_AnimatorMonitor.DetermineStates(); } } }
        public float RotationSpeed { get { return m_RotationSpeed; } set { m_RotationSpeed = value; } }
        public bool AlwaysAim { get { return m_AlwaysAim; } set { m_AlwaysAim = value; if (m_AnimatorMonitor != null) m_AnimatorMonitor.DetermineStates(); } }
        public PhysicMaterial GroundedIdleFrictionMaterial { set { m_GroundedIdleFrictionMaterial = value; } get { return m_GroundedIdleFrictionMaterial; } }
        public PhysicMaterial GroundedMovingFrictionMaterial { set { m_GroundedMovingFrictionMaterial = value; } get { return m_GroundedMovingFrictionMaterial; } }
        public PhysicMaterial StepFrictionMaterial { set { m_StepFrictionMaterial = value; } get { return m_StepFrictionMaterial; } }
        public PhysicMaterial SlopeFrictionMaterial { set { m_SlopeFrictionMaterial = value; } get { return m_SlopeFrictionMaterial; } }
        public PhysicMaterial AirFrictionMaterial { set { m_AirFrictionMaterial = value; } get { return m_AirFrictionMaterial; } }
        public float SkinWidth { get { return m_SkinWidth; } }
        public bool Grounded { get { return m_Grounded; } set { m_Grounded = value; } }
        public bool Aim { get { return m_Aim; } set { SetAim(value); } }
        public bool Aiming { get { return m_Aim || m_ForceAim || m_AlwaysAim; } }
        public float SlopeLimit { get { return m_SlopeLimit; } }
        public Vector3 InputVector { get { return m_InputVector; } set { m_InputVector = value; } }
        public CapsuleCollider CapsuleCollider { get { return m_CapsuleCollider; } }
        public Vector3 RelativeInputVector
        {
            get
            {
                var relativeInputVector = Vector3.zero;
                if (m_MovementType == MovementType.Combat || m_MovementType == MovementType.Adventure || m_MovementType == MovementType.RPG || m_MovementType == MovementType.PointClick ||
                    m_MovementType == MovementType.FourLegged) {
                    relativeInputVector.x = m_InputVector.x;
                    relativeInputVector.z = m_InputVector.z;
                } else if (m_MovementType == MovementType.TopDown || m_MovementType == MovementType.Pseudo3D) {
                    relativeInputVector = m_Transform.InverseTransformDirection(m_InputVector.x, 0, m_InputVector.z);
                }
                return relativeInputVector;
            }
        }
        public Quaternion LookRotation { get { return m_LookRotation; } set { m_LookRotation = value; } }
        public bool LookInMoveDirection { get { return m_LookInMoveDirection; } set { m_LookInMoveDirection = value; } }
        public Vector3 RootMotionForce { get { return m_RootMotionForce; } set { m_RootMotionForce = value; } }
        public Quaternion RootMotionRotation { get { return m_RootMotionRotation; } set { m_RootMotionRotation = value; } }
        public float RootMotionSpeedMultiplier { get { return m_RootMotionSpeedMultiplier; } set { m_RootMotionSpeedMultiplier = value; } }
        public bool ForceRootMotion { set { m_ForceRootMotion = value; } }
        public bool ForceItemRootMotion { set { m_ForceItemRootMotion = value; } }
        public bool UsingRootMotion { get { return m_UseRootMotion || m_ForceRootMotion || m_ForceItemRootMotion; } }
        public bool UseRootMotion { get { return m_UseRootMotion; } set { m_UseRootMotion = value; } }
        public Vector3 Velocity { get { return m_Velocity; } set { m_Velocity = value; } }
        public Ability[] Abilities { get { return m_Abilities; } set { m_Abilities = value; } }

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_CapsuleCollider == null) {
                m_CapsuleCollider = GetComponent<CapsuleCollider>();
            }
            m_AnimatorMonitor = GetComponent<AnimatorMonitor>();

            SharedManager.Register(this);

            // An AI agent will not have the PlayerInput component.
            m_AIAgent = GetComponent<PlayerInput>() == null;

#if !(UNITY_4_6 || UNITY_4_7 || UNITY_5_0)
            // A networked character will have the NetworkIdentity component.
            var networkIdentity = GetComponent<UnityEngine.Networking.NetworkIdentity>();
#if ENABLE_MULTIPLAYER
            if (networkIdentity == null) {
                Debug.LogError("Error: The Multiplayer symbol is defined but the NetworkIdentity component was not was found. Please remove the symbol within the RigidbodyCharacterController inspector.");
            } else if (networkIdentity.localPlayerAuthority) {
                Debug.LogWarning("Warning: Local Player Authority is enabled on the NetworkIdentity component. This value must be disabled.");
                networkIdentity.localPlayerAuthority = false;
            }
#else
            if (networkIdentity != null) {
                Debug.LogError("Error: A NetworkIdentity component was found but the ENABLE_MULTIPLAYER symbol is not defined. Please define it within the RigidbodyCharacterController inspector. " +
                               "The source code must be imported for the symbol to be detected.");
            }
#endif
#endif

            m_PrevGroundHeight = m_Transform.position.y;
            m_CapsuleColliderHeight = m_CapsuleCollider.height;
            m_CapsuleColliderCenter = m_CapsuleCollider.center;
            SetPosition(m_Transform.position);
            SetRotation(m_Transform.rotation);
        }

        /// <summary>
        /// Register for any events that the controller should be aware of.
        /// </summary>
        protected void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorAiming", OnAiming);
            EventHandler.RegisterEvent(m_GameObject, "OnItemUse", OnItemUse);
            EventHandler.RegisterEvent(m_GameObject, "OnItemStopUse", OnItemStopUse);
            EventHandler.RegisterEvent(m_GameObject, "OnMeleeItemEndRecoil", ResetItemCollision);
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Unregister for any events that the controller was registered for.
        /// </summary>
        protected void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorAiming", OnAiming);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemUse", OnItemUse);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemStopUse", OnItemStopUse);
            EventHandler.UnregisterEvent(m_GameObject, "OnMeleeItemEndRecoil", ResetItemCollision);
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
        }
        
        /// <summary>
        /// Initialize the shared fields.
        /// </summary>
        protected void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
        }

        /// <summary>
        /// Should the character look independetly of the camera? AIAgents do not need to use the camera rotation nor do multiple active characters within the same scene.
        /// </summary>
        /// <returns>True if the character should look independently.</returns>
        private bool SharedMethod_IndependentLook()
        {
            return IndependentLook();
        }

        /// <summary>
        /// Should the character look independetly of the camera? AIAgents do not need to use the camera rotation nor do multiple active characters within the same scene.
        /// </summary>
        /// <returns>True if the character should look independently.</returns>
        public bool IndependentLook()
        {
            return m_AIAgent || m_LocalCoopCharacter;
        }

        /// <summary>
        /// Is the character an AI agent?
        /// </summary>
        /// <returns>True if the character is an AI agent.</returns>
        private bool SharedMethod_AIAgent()
        {
            return m_AIAgent;
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Is the character running as a server?
        /// </summary>
        /// <returns>True if the character is running as a server.</returns>
        private bool SharedMethod_IsServer()
        {
            return isServer;
        }

        /// <summary>
        /// Is the character running as a local player?
        /// </summary>
        /// <returns>True if the character is running as a local player.</returns>
        private bool SharedMethod_IsLocalPlayer()
        {
            return isLocalPlayer;
        }
#endif

        /// <summary>
        /// Moves the character according to the input. This method exists to allow AI to easily move the character instead of having to go through
        /// the ControllerHandler.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        public void Move(float horizontalMovement, float forwardMovement, Quaternion lookRotation)
        {
            // Store the velocity as it will be used by many of the functions below.
            m_Velocity = m_Rigidbody.velocity;

            // Store the input parameters.
            m_InputVector.x = horizontalMovement;
            m_InputVector.z = forwardMovement;
            m_LookRotation = lookRotation;

            var abilityHasControl = false;
            for (int i = 0; i < m_Abilities.Length; ++i) {
                m_Abilities[i].UpdateAbility();
                if (!m_Abilities[i].IsActive && m_Abilities[i].StartType == Ability.AbilityStartType.Automatic) {
                    TryStartAbility(m_Abilities[i]);
                }
                if (m_Abilities[i].IsActive) {
                    if (m_Abilities[i].StopType == Ability.AbilityStopType.Automatic && m_Abilities[i].CanStopAbility()) {
                        TryStopAbility(m_Abilities[i]);
                        continue;
                    }
                    if (!m_Abilities[i].Move()) {
                        abilityHasControl = true;
                    }
                }
            }
            if (abilityHasControl) {
                return;
            }

            // Is the character on the ground?
            CheckGround();

            // Are any external forces affecting the current velocity?
            CheckForExternalForces();

            // Ensures the current movement is valid.
            CheckMovement();

            // Set the correct physic material based on the grounded state.
            SetPhysicMaterial();

            // Update the velocity based on the grounded state.
            UpdateMovement();

            // Move with the platform if on a moving platform.
            UpdatePlatformMovement();

            // Rotate in the correct direction.
            UpdateRotation();

            // Update the animator so the correct animations will play.
            UpdateAnimator();

            // Update the collider to be sized correctly according to the animation played.
            UpdateCollider();

            // The velocity would have been modified by the above functions so reassign it when reassign velocity.
            m_Rigidbody.velocity = m_Velocity;
        }

        /// <summary>
        /// Updates the grounded state if the character is on the ground. Will also check for a moving platform.
        /// </summary>
        private void CheckGround()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CheckGround()) {
                    return;
                }
            }

            // If the character is on a platform extend the length to account for the platform's position change.
            var skinWidth = m_SkinWidth;
            if (m_Platform != null && m_Velocity.y < 0.1f) {
                skinWidth += m_MovingPlatformSkinWidth;
            }

            // Determine if the character is grounded by doing a spherecast from the character's knee to the ground.
            var grounded = Physics.SphereCast(m_Transform.position + m_Transform.up * m_CapsuleCollider.radius * 2, m_CapsuleCollider.radius, -m_Transform.up, out m_GroundRaycastHit,
                                                            m_CapsuleCollider.radius * 2 + skinWidth, LayerManager.Mask.Ground) || m_Stepping;
            if (grounded) {
                // Update the platform variables if on a moving platform. The characters position and rotation will change based off of that moving platform.
                if (m_GroundRaycastHit.transform != null && m_GroundRaycastHit.transform.gameObject.layer == LayerManager.MovingPlatform) {
                    if (m_Platform == null) {
                        m_Platform = m_GroundRaycastHit.transform;
                        m_PlatformPosition = m_Platform.position;
                        m_PrevPlatformAngle = m_Platform.eulerAngles.y;
                    }
                } else {
                    m_Platform = null;
                }
                // Prevent the velocity from forcing the character through the ground.
                // This raycast is set to LayerManager.Default rather than LayerManager.Mask.Ground for the PuppetMaster integration.
                if (Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, -m_Transform.up, out m_GroundRaycastHit,
                                                            m_CapsuleCollider.height / 2 - 0.001f, LayerManager.Default) && m_Transform.position.y < m_GroundRaycastHit.point.y) {
                    var position = m_Transform.position;
                    position.y = m_GroundRaycastHit.point.y;
                    m_Transform.position = position;
                    m_Velocity.y = 0;
                } else {
                    // Keep the character sticking to the ground.
                    m_Velocity.y -= m_GroundStickiness * Time.fixedDeltaTime;
                }
            } else if (!grounded) { // The character is no longer on the ground. Reset the related variables.
                m_Platform = null;
                if (m_Grounded) {
                    // Add a small force in the moving direction to prevent the character from toggling between grounded and not grounded state.
                    m_Velocity += m_Velocity.normalized * 0.2f;
                }
                // Save out the max height of the character in the air so the fall height can be calculated and the grounded check can ensure the player is on the ground.
                if (m_Transform.position.y > m_PrevGroundHeight) {
                    m_PrevGroundHeight = m_Transform.position.y;
                }
            }

            if (m_Grounded != grounded) {
                EventHandler.ExecuteEvent<bool>(m_GameObject, "OnControllerGrounded", grounded);
                // Other objects are interested in when the character lands (such as CharacterHealth to determine if any fall damage should be applied).
                if (grounded) {
                    m_AirVelocity = m_PrevAirVelocity = Vector3.zero;
                    EventHandler.ExecuteEvent<float>(m_GameObject, "OnControllerLand", (m_PrevGroundHeight - m_Transform.position.y));
                    m_PrevGroundHeight = float.NegativeInfinity;
                } else {
                    m_GroundVelocity = m_PrevGroundVelocity = Vector3.zero;
                }
            }
            m_Grounded = grounded;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// </summary>
        private void CheckForExternalForces()
        {
            var xPercent = 0f;
            var zPercent = 0f;
            // Calculate the percentage that the root motion force affected the current velocity. 
            if (m_Grounded && UsingRootMotion) {
                var prevTotalRootMotionForce = Mathf.Abs(m_PrevRootMotionForce.x) + Mathf.Abs(m_PrevRootMotionForce.z);
                xPercent = m_Velocity.x != 0 ? Mathf.Clamp01(Mathf.Abs(prevTotalRootMotionForce / m_Velocity.x)) : 1;
                zPercent = m_Velocity.z != 0 ? Mathf.Clamp01(Mathf.Abs(prevTotalRootMotionForce / m_Velocity.z)) : 1;
            }

            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CheckForExternalForces(xPercent, zPercent)) {
                    return;
                }
            }

            if (m_Grounded) {
                if (UsingRootMotion) {
                    // Only add a dampening to the non-root motion velocity. The root motion velocity has already had a dampening force added to it within UpdateMovment.
                    m_Velocity.x = ((m_Velocity.x * (1 - xPercent)) / (1 + m_GroundDampening)) + m_PrevRootMotionForce.x * xPercent;
                    m_Velocity.z = ((m_Velocity.z * (1 - zPercent)) / (1 + m_GroundDampening)) + m_PrevRootMotionForce.z * zPercent;
                } else {
                    // Don't use root motion so apply the ground dampening to the entire velocity.
                    m_Velocity.x /= (1 + m_GroundDampening);
                    m_Velocity.z /= (1 + m_GroundDampening);
                }
            } else {
                // Root motion doesn't affect the character at all in the air so just apply a dampening force.
                m_Velocity.x /= (1 + m_AirDampening);
                m_Velocity.z /= (1 + m_AirDampening);
            }
        }

        /// <summary>
        /// Ensure the current movement direction is valid. For example, don't allow the character to keep running into a wall.
        /// </summary>
        private void CheckMovement()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CheckMovement()) {
                    return;
                }
            }

            // Assume the character is not stepping or is on a slope. If the character is then it'll be updated within the check below.
            m_Slope = -1;
            m_Stepping = false;
            // Determine the relative direction of the input based on the movement type.
            var localInputVector = GetLocalInputVector(m_InputVector);
            var moveDirection = m_Velocity;
            moveDirection.y = 0;
            // If the character doesn't have any velocity then the input vector may be requesting the character to move. Use whichever vector is greater.
            if (localInputVector.sqrMagnitude > moveDirection.sqrMagnitude) {
                moveDirection = localInputVector;
            }
            if (moveDirection.sqrMagnitude > 0.001f) {
                // Fire a raycast in the direction that the character is moving. There is a chance that the character should step or stop moving if the raycast hits an object.
                if (Physics.Raycast(m_Transform.position + m_Transform.up * m_StepOffset.y, moveDirection.normalized, out m_RaycastHit, m_CapsuleCollider.radius + m_StepOffset.z, LayerManager.Mask.Ground)) {
                    // An object was hit. The character should stop moving or step if the object does not have a Rigidbody, the Rigidbody is kinematic, or the Rigidbody is much heavier than the character.
                    Rigidbody hitRigidbody = null;
                    if ((hitRigidbody = m_RaycastHit.transform.GetComponent<Rigidbody>()) == null || hitRigidbody.isKinematic == true || hitRigidbody.mass > m_Rigidbody.mass * 10) {
                        var direction = (m_RaycastHit.point - m_Transform.position);
                        direction.y = 0;
                        var hitPoint = m_RaycastHit.point;
                        // Stop moving if the object is taller than the max step height.
                        if (Physics.Raycast(m_Transform.position + m_Transform.up * (m_MaxStepHeight + 0.01f), direction, direction.magnitude + 0.01f, LayerManager.Mask.Ground)) {
                            localInputVector = Vector3.zero;
                            // Gently decrease the velocity to prevent a jerky stop.
                            var velocity = m_Velocity;
                            velocity.x *= m_GroundDampening;
                            velocity.z *= m_GroundDampening;
                            m_Velocity = velocity;
                        } else {
                            var slope = Mathf.Acos(Mathf.Clamp(m_RaycastHit.normal.y, -1, 1)) * Mathf.Rad2Deg;
                            if (slope <= m_SlopeLimit) {
                                m_Slope = slope;
                            } else {
                                // Get the top of the step. This will allow the character to eitehr step or stop depending on the angle.
                                Physics.Raycast(hitPoint + Vector3.up * m_MaxStepHeight + direction.normalized * 0.1f, Vector3.down, out m_RaycastHit, m_MaxStepHeight + 0.1f, LayerManager.Mask.Ground);
                                if ((Mathf.Acos(Mathf.Clamp(m_RaycastHit.normal.y, -1, 1)) * Mathf.Rad2Deg) <= m_SlopeLimit) {
                                    var position = m_Transform.position;
                                    // Use the Max Step Height for the top position - the step may not be that tall but the stepping will stop as soon as the character has cleared the step.
                                    position.y = Mathf.MoveTowards(position.y, m_Transform.position.y + m_MaxStepHeight, m_StepSpeed * Time.fixedDeltaTime);
                                    m_Transform.position = position;
                                    var velocity = m_Velocity;
                                    velocity.y = 0;
                                    m_Velocity = velocity;
                                    m_Stepping = true;
                                } else {
                                    localInputVector = Vector3.zero;
                                    // Gently decrease the velocity to prevent a jerky stop.
                                    var velocity = m_Velocity;
                                    velocity.x *= m_GroundDampening;
                                    velocity.z *= m_GroundDampening;
                                    m_Velocity = velocity;
                                }
                            }
                        }
                    }
                }
            }

            if (localInputVector != Vector3.zero) {
                // Check all of the collision contacts. Stop moving if the character is trying to move in the direction of an object that was hit.
                for (int i = 0; i < m_CollisionPoints.Count; ++i) {
                    var direction = m_CollisionPoints[i].Point - m_Transform.position;
                    direction.y = 0; // Ignore the y direction.
                    if (Vector3.Dot(direction.normalized, moveDirection.normalized) > m_StopMovementThreshold) {
                        localInputVector = Vector3.zero;
                        // Gently decrease the velocity to prevent a jerky stop.
                        var velocity = m_Velocity;
                        velocity.x *= m_GroundDampening;
                        velocity.z *= m_GroundDampening;
                        m_Velocity = velocity;
                        break;
                    }
                }

                // If the item has collided with an object then the character shouldn't keep moving in that colliding objects direction.
                if (m_ItemCollisionDirection != Vector3.zero) {
                    if (Vector3.Dot(m_ItemCollisionDirection, moveDirection.normalized) > m_StopMovementThreshold) {
                        localInputVector = Vector3.zero;
                    } else {
                        m_ItemCollisionDirection = Vector3.zero;
                    }
                }

                // Prevent moving in the x or z directions if there are constraints set.
                if (m_MovementConstraint != MovementConstraint.None) {
                    // Restrict the x axis if the constraint is set to anything but RestrictZ.
                    if (m_MovementConstraint != MovementConstraint.RestrictZ) {
                        if ((localInputVector.x < 0 && m_Transform.position.x < m_MinXPosition) ||
                            (localInputVector.x > 0 && m_Transform.position.x > m_MaxXPosition)) {
                            localInputVector.x = 0;
                            m_RootMotionForce.x = 0;
                        }
                    }

                    // Restrict the z axis if the constraint is set to anything but RestrictX.
                    if (m_MovementConstraint != MovementConstraint.RestrictX) {
                        if ((localInputVector.z < 0 && m_Transform.position.z < m_MinZPosition) ||
                            (localInputVector.z > 0 && m_Transform.position.z > m_MaxZPosition)) {
                            localInputVector.z = 0;
                            m_RootMotionForce.z = 0;
                        }
                    }
                }
            }

            var useLocalInputVector = true;
            switch (m_MovementType) {
                case MovementType.TopDown:
                case MovementType.Pseudo3D:
                    useLocalInputVector = false;
                    break;
            }

            // Abilities can override the localInputVector variable.
            if (!useLocalInputVector) {
                for (int i = 0; i < m_Abilities.Length; ++i) {
                    m_Abilities[i].UpdateAbility();
                    if (m_Abilities[i].IsActive && m_Abilities[i].UseLocalMovement()) {
                        useLocalInputVector = true;
                        break;
                    }
                }
            }

            if (useLocalInputVector) {
                var eulerAngles = (m_MovementType != MovementType.FourLegged ? m_LookRotation : m_Transform.rotation).eulerAngles;
                eulerAngles.x = 0;
                m_InputVector = Quaternion.Inverse(Quaternion.Euler(eulerAngles)) * localInputVector;
            } else {
                m_InputVector = localInputVector;
            }

            m_InputVector.y = 0;
        }

        /// <summary>
        /// Sets the physic material based on the current character state.
        /// </summary>
        private void SetPhysicMaterial()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].SetPhysicMaterial()) {
                    return;
                }
            }

            if (m_Grounded) {
                if (m_Stepping && m_StepFrictionMaterial != null) {
                    SetPhysicMaterial(m_StepFrictionMaterial);
                } else if (m_Slope != -1 && m_SlopeFrictionMaterial != null) {
                    SetPhysicMaterial(m_SlopeFrictionMaterial);
                } else if (m_Moving) {
                    SetPhysicMaterial(m_GroundedMovingFrictionMaterial);
                } else {
                    SetPhysicMaterial(m_GroundedIdleFrictionMaterial);
                }
            } else {
                if (m_AirFrictionMaterial != null) {
                    SetPhysicMaterial(m_AirFrictionMaterial);
                }
            }
        }

        /// <summary>
        /// Sets the CapsuleCollider and any linked colliders physic material.
        /// </summary>
        /// <param name="physicMaterial">The material to set.</param>
        private void SetPhysicMaterial(PhysicMaterial physicMaterial)
        {
            m_CapsuleCollider.material = physicMaterial;
            if (m_LinkedColliders != null) {
                for (int i = 0; i < m_LinkedColliders.Length; ++i) {
                    m_LinkedColliders[i].material = physicMaterial;
                }
            }
        }

        /// <summary>
        /// Update the grounded or in air movement.
        /// </summary>
        private void UpdateMovement()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].UpdateMovement()) {
                    return;
                }
            }

            // Move backwards if the character's item is colliding with a fixed object.
            if (m_ItemCollision != null) {
                var position = m_Transform.position;
                position -= m_ItemCollisionDirection * Time.fixedDeltaTime;
                m_Transform.position = position;
            }

            if (m_Grounded) {
                UpdateGroundedMovement();
            } else {
                UpdateAirborneMovement();
            }
        }

        /// <summary>
        /// Apply grounded forces while on the group. Y velocity should be 0.
        /// </summary>
        private void UpdateGroundedMovement()
        {
            // Directly add to the velocity if not using root motion. This movement will be instantaneous.
            if (UsingRootMotion) {
                var rootMotionForce = m_RootMotionForce / ((1 + m_GroundDampening) * Time.fixedDeltaTime);
                m_Velocity.x += (rootMotionForce.x - m_PrevRootMotionForce.x);
                m_Velocity.z += (rootMotionForce.z - m_PrevRootMotionForce.z);
                m_PrevRootMotionForce = rootMotionForce;
                m_RootMotionForce = Vector3.zero;
            } else {
                // Move in the correct direction.
                var inputVector = m_InputVector.normalized * Mathf.Max(Mathf.Abs(m_InputVector.x), Mathf.Abs(m_InputVector.z)); // Prevent the diagonal from moving faster.
                var localInputVector = GetLocalInputVector(Vector3.Scale(inputVector, m_GroundSpeed));
                m_GroundVelocity += localInputVector;
                m_GroundVelocity -= m_PrevGroundVelocity;

                if (m_GroundVelocity.sqrMagnitude < 0.01f) {
                    m_GroundVelocity = Vector3.zero;
                }

                m_Velocity += m_GroundVelocity;
                m_PrevGroundVelocity = m_GroundVelocity;
            }
        }

        /// <summary>
        /// While in the air root motion doesn't exist so apply the input forces manually.
        /// </summary>
        private void UpdateAirborneMovement()
        {
            // Let gravity handle vertical movement.
            m_AirVelocity.y = 0;

            // Move in the correct direction.
            var inputVector = m_InputVector.normalized * Mathf.Max(Mathf.Abs(m_InputVector.x), Mathf.Abs(m_InputVector.z)); // Prevent the diagonal from moving faster.
            var localInputVector = GetLocalInputVector(Vector3.Scale(inputVector, m_AirSpeed));
            m_AirVelocity += localInputVector;
            m_AirVelocity -= m_PrevAirVelocity;

            if (m_AirVelocity.sqrMagnitude < 0.01f) {
                m_AirVelocity = Vector3.zero;
            }

            m_Velocity += m_AirVelocity;
            m_PrevAirVelocity = m_AirVelocity;
        }

        /// <summary>
        /// Move and rotate while on a moving platform.
        /// </summary>
        private void UpdatePlatformMovement()
        {
            if (m_Platform == null)
                return;

            // Keep the same relative position.
            var target = m_Transform.position + m_Platform.position - m_PlatformPosition;
            m_Rigidbody.MovePosition(target);
            m_PlatformPosition = m_Platform.position;

            // Keep the same relative rotation.
            var eulerAngles = m_Transform.eulerAngles;
            eulerAngles.y -= Mathf.DeltaAngle(m_Platform.eulerAngles.y, m_PrevPlatformAngle);
            m_Transform.eulerAngles = eulerAngles;
            m_PrevPlatformAngle = m_Platform.eulerAngles.y;
            m_PrevYRotation = eulerAngles.y;
        }

        /// <summary>
        /// Rotate in the correct direction. The rotation direction depends on if the character movement type.
        /// </summary>
        private void UpdateRotation()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].UpdateRotation()) {
                    return;
                }
            }

            var eulerRotation = m_Transform.eulerAngles;

            if ((m_MovementType == MovementType.Adventure || m_MovementType == MovementType.FourLegged) && !Aiming) {
                // Face in the direction that the character is moving if not in combat mode.
                if (m_InputVector != Vector3.zero) {
                    if (m_MovementType == MovementType.Adventure) {
                        eulerRotation.y = Quaternion.LookRotation(m_LookRotation * m_InputVector.normalized).eulerAngles.y;
                    } else {
                        // Do not rotate when the character should move directly backwards.
                        if (Mathf.Abs(m_InputVector.x) > 0.01f || m_InputVector.z > -0.01f) {
                            eulerRotation.y = Quaternion.LookRotation(((Mathf.Abs(m_InputVector.x) < 0.01f && m_InputVector.z > 0.01) ? m_LookRotation : m_Transform.rotation) * m_InputVector.normalized).eulerAngles.y;
                            // Allow the character to take tight turns when turning and moving forward.
                            if (Mathf.Abs(m_InputVector.x) > 0.01f && m_InputVector.z > 0.01) {
                                eulerRotation.y += 90 * Mathf.Sign(m_InputVector.x);
                            }
                        }
                    }
                }
            } else {
                // Do not rotate if the delta angle between the character and the look rotation is less than a threshold. This wil allow the character's upper body to
                // look without having to rotate the entire character.
                var deltaAngle = Mathf.Abs(Mathf.DeltaAngle(eulerRotation.y, m_LookRotation.eulerAngles.y));
                if (deltaAngle < m_TorsoLookThreshold && !m_Rotating) {
                    return;
                }

                m_Rotating = deltaAngle > 0.01f;
                eulerRotation.y = m_LookRotation.eulerAngles.y;
            }

            eulerRotation.y = Mathf.LerpAngle(m_Transform.eulerAngles.y, eulerRotation.y, (Aiming ? m_AimRotationSpeed : m_RotationSpeed) * Time.deltaTime);

            var rotation = Quaternion.Euler(eulerRotation);
            if (m_AlignToGround && m_Grounded) {
                // The normal is determined by the height of the front and the back of the character.
                var frontPoint = m_Transform.position;
                if (Physics.Raycast(m_Transform.position + (m_Transform.up * 0.1f) + m_Transform.forward * m_AlignToGroundDepthOffset * Mathf.Sign(m_InputVector.z), -m_Transform.up, out m_GroundRaycastHit, float.MaxValue, LayerManager.Mask.Ground)) {
                    frontPoint = m_GroundRaycastHit.point;
                }
                var backPoint = frontPoint;
                if (Physics.Raycast(m_Transform.position + (m_Transform.up * 0.1f) + m_Transform.forward * m_AlignToGroundDepthOffset * -Mathf.Sign(m_InputVector.z), -m_Transform.up, out m_GroundRaycastHit, float.MaxValue, LayerManager.Mask.Ground)) {
                    backPoint = m_GroundRaycastHit.point;
                }
                var direction = (frontPoint - backPoint);
                var normal = Vector3.Cross(direction, Vector3.Cross(Vector3.up, direction)).normalized;
                // Do not rotate unless the slope is less than the slope limit.
                if (Mathf.Acos(normal.y) * Mathf.Rad2Deg < m_SlopeLimit) {
                    // Rotate the character to always stay flat on the ground.
                    var proj = (rotation * Vector3.forward) - (Vector3.Dot((rotation * Vector3.forward), normal)) * normal;
                    var targetEuler = Quaternion.LookRotation(proj, normal).eulerAngles;
                    targetEuler.z = m_Transform.eulerAngles.z;
                    rotation = Quaternion.Slerp(rotation, Quaternion.Euler(targetEuler), m_AlignToGroundRotationSpeed * Time.deltaTime);
                }
            }

            // Apply the rotation.
            m_Transform.rotation = rotation;
        }

        /// <summary>
        /// Update the animator with the correct parameters.
        /// </summary>
        private void UpdateAnimator()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].UpdateAnimator()) {
                    return;
                }
            }
            
            // The input direction depends on the movement type.
            Vector3 inputValue = Vector3.zero;
            if (m_MovementType == MovementType.Combat || m_MovementType == MovementType.RPG || m_MovementType == MovementType.PointClick || m_MovementType == MovementType.FourLegged ||
                (m_MovementType == MovementType.Adventure && Aiming)) {
                inputValue.x = m_InputVector.x;
                inputValue.z = m_InputVector.z;
                // Allow the yaw to contribute to the horizontal input value so the character will play the turn animation.
                if (m_MovementType == MovementType.FourLegged) {
                    inputValue.x = Mathf.Clamp(inputValue.x + Mathf.DeltaAngle(m_PrevYRotation, m_Transform.eulerAngles.y), -1, 1);
                }
            } else if (m_MovementType == MovementType.Adventure) {
                // Clamp to a value higher then one if the x or z value is greater then one. This can happen if the character is sprinting.
                var clampValue = Mathf.Max(Mathf.Abs(m_InputVector.x), Mathf.Max(Mathf.Abs(m_InputVector.z), 1));
                inputValue.z = Mathf.Clamp(m_InputVector.magnitude, -clampValue, clampValue);
            } else if (m_MovementType == MovementType.TopDown || m_MovementType == MovementType.Pseudo3D) {
                inputValue = m_Transform.InverseTransformDirection(m_InputVector.x, 0, m_InputVector.z);
            }
            m_AnimatorMonitor.SetHorizontalInputValue(inputValue.x);
            m_AnimatorMonitor.SetForwardInputValue(inputValue.z);
            m_AnimatorMonitor.SetYawValue(Mathf.DeltaAngle(m_PrevYRotation, m_Transform.eulerAngles.y));
            m_PrevYRotation = m_Transform.eulerAngles.y;

            Moving = m_InputVector.sqrMagnitude > 0.01f;
        }

        /// <summary>
        /// Size the collider according to the animation being played.
        /// </summary>
        private void UpdateCollider()
        {
            var curveData = m_AnimatorMonitor.GetColliderCurveData();
            m_CapsuleCollider.height = m_CapsuleColliderHeight - curveData;
            var center = m_CapsuleColliderCenter;
            center.y *= (m_CapsuleCollider.height / m_CapsuleColliderHeight);
            m_CapsuleCollider.center = center;
        }

        /// <summary>
        /// The character has collided with another object. 
        /// </summary>
        /// <param name="collision">The object that the character collided with.</param>
        private void OnCollisionEnter(Collision collision)
        {
            AddCollisionPoint(collision);
        }

        /// <summary>
        /// Tries to add the collision to the collision point list.
        /// </summary>
        /// <param name="collision">The object that the character collided with.</param>
        /// <returns>True if the collision was added to the collision point list.</returns>
        private bool AddCollisionPoint(Collision collision)
        {
            // The collision only matters if the contact point is above the step height. The CheckMovement raycast can handle the collisions below the step height.
            if (collision.contacts[0].point.y > m_Transform.position.y + m_MaxStepHeight && Utility.InLayerMask(collision.gameObject.layer, LayerManager.Mask.IgnoreInvisibleLayers)) {
                // The collision should be saved if the object does not have a Rigidbody, the Rigidbody is kinematic, or the Rigidbody is much heavier than the character.
                if ((collision.rigidbody == null || collision.rigidbody.isKinematic || collision.rigidbody.mass > m_Rigidbody.mass * 10) && !m_CollisionSet.Contains(collision.transform)) {
                    var collisionPoint = ObjectPool.Get<CollisionPoint>();
                    collisionPoint.Initialize(collision.transform, collision.contacts[0].point);
                    m_CollisionPoints.Add(collisionPoint);
                    m_CollisionSet.Add(collision.transform);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The item that the character has equipped has collided with another object. 
        /// </summary>
        /// <param name="item">The item that caused the collision.</param>
        /// <param name="collision">The object that the item collided with.</param>
        public void OnItemCollisionEnter(Item item, Collision collision)
        {
            // Don't collide with the character that is holding the item.
            if (collision.gameObject == m_GameObject || collision.gameObject.GetComponentInParent<RigidbodyCharacterController>() == this) {
                return;
            }

            // Abilities have the option of disallow item collisions.
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].AllowItemCollisions()) {
                    return;
                }
            }

            if (AddCollisionPoint(collision)) {
                m_ItemCollision = item;
                m_ItemCollisionDirection = m_Transform.forward;
                m_ItemCollisionDirection.y = 0;
                m_ItemCollisionDirection.Normalize();
            }
        }

        /// <summary>
        /// The character has stopped colliding with another object.
        /// </summary>
        /// <param name="collision">The object that the character is no longer colliding with.</param>
        private void OnCollisionExit(Collision collision)
        {
            // Remove the collision if it has been recorded.
            for (int i = m_CollisionPoints.Count - 1; i > -1; --i) {
                if (m_CollisionPoints[i].Transform == collision.transform) {
                    m_CollisionSet.Remove(collision.transform);
                    ObjectPool.Return(m_CollisionPoints[i]);
                    m_CollisionPoints.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Resets the Item Collision Direction to the default value.
        /// </summary>
        private void ResetItemCollision()
        {
            m_ItemCollisionDirection = Vector3.zero;
        }

        /// <summary>
        /// The item that the character has equipped has stopped colliding with another object.
        /// </summary>
        /// <param name="item">The item that caused the collision.</param>
        /// <param name="collision">The object that the item is no longer colliding with.</param>
        public void OnItemCollisionExit(Item item, Collision collision)
        {
            if (item == m_ItemCollision) {
                m_ItemCollision = null;
            }
            // collision will be null if the item is being disabled.
            if (collision != null) {
                OnCollisionExit(collision);
            }
        }

        /// <summary>
        /// Returns the input vector relative to the character's local rotation.
        /// </summary>
        /// <param name="inputVector">The current input vector.</param>
        /// <returns>The input vector relative to the character's local rotation.</returns>
        private Vector3 GetLocalInputVector(Vector3 inputVector)
        {
            var useLocalInputVector = true;
            switch (m_MovementType) {
                case MovementType.TopDown:
                case MovementType.Pseudo3D:
                    useLocalInputVector = false;
                    break;
            }

            // Abilities can override the localInputVector variable.
            if (!useLocalInputVector) {
                for (int i = 0; i < m_Abilities.Length; ++i) {
                    m_Abilities[i].UpdateAbility();
                    if (m_Abilities[i].IsActive && m_Abilities[i].UseLocalMovement()) {
                        useLocalInputVector = true;
                        break;
                    }
                }
            }

            if (useLocalInputVector) {
                var eulerAngles = (m_MovementType != MovementType.FourLegged ? m_LookRotation : m_Transform.rotation).eulerAngles;
                eulerAngles.x = eulerAngles.z = 0;
                inputVector = Quaternion.Euler(eulerAngles) * inputVector;
            }
            return inputVector;
        }

        /// <summary>
        /// Immediately sets the position. This is not a smooth movement.
        /// </summary>
        /// <param name="position">The target position.</param>
        public void SetPosition(Vector3 position)
        {
            m_Transform.position = position;

            // The old collisions are no longer relevant if the character is repositioned within a single frame.
            m_CollisionPoints.Clear();
            m_CollisionSet.Clear();
            m_ItemCollision = null;
        }

        /// <summary>
        /// Immediately sets the rotation. This is not a smooth rotation.
        /// </summary>
        /// <param name="rotation">The target rotation.</param>
        public void SetRotation(Quaternion rotation)
        {
            m_Rigidbody.rotation = m_Transform.rotation = rotation;
            m_PrevYRotation = rotation.eulerAngles.y;
        }

        /// <summary>
        /// Stops the Rigidbody from moving.
        /// </summary>
        public void StopMovement()
        {
            m_PrevRootMotionForce = m_RootMotionForce = m_Velocity = Vector3.zero;
            m_RootMotionRotation = Quaternion.identity;
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Tries to start the specified ability.
        /// </summary>
        /// <param name="ability">The ability to try to start.</param>
        /// <returns>True if the ability was started.</returns>
        public bool TryStartAbility(Ability ability)
        {
            return TryStartAbility(ability, false);
        }

        /// <summary>
        /// Tries to start the specified ability.
        /// </summary>
        /// <param name="ability">The ability to try to start.</param>
        /// <param name="ignorePriority">Should the ability priority be ignored?</param>
        /// <returns>True if the ability was started.</returns>
        public bool TryStartAbility(Ability ability, bool ignorePriority)
        {
            // Start the ability if it is not active and can be started.
            if (!ability.IsActive && ability.CanStartAbility()) {
                // If the ability is not a concurrent ability then it can only be started if it has a lower index than any other active abilities.
                if (!ignorePriority) {
                    if (!ability.IsConcurrentAblity()) {
                        for (int i = 0; i < m_Abilities.Length; ++i) {
                            if (m_Abilities[i].IsActive) {
                                var ignoreLocalPriority = m_Abilities[i].ShouldIgnorePriority() && ability.ShouldIgnorePriority(); 
                                if (m_Abilities[i].IsConcurrentAblity()) {
                                    // The ability cannot be started if a concurrent ability is active and has a lower index.
                                    if (((!ignoreLocalPriority && i < ability.Index) || ignoreLocalPriority) && !m_Abilities[i].CanStartAbility(ability)) {
                                        return false;
                                    }
                                } else {
                                    // The ability cannot be started if another ability is already active and has a lower index or if the active ability says the current ability cannot be started.
                                    if ((i < ability.Index && !ignoreLocalPriority) || !m_Abilities[i].CanStartAbility(ability)) {
                                        return false;
                                    } else {
                                        // Stop any abilities that have a higher index to prevent two non-concurrent abilities from running at the same time.
                                        m_Abilities[i].AbilityWillStart(ability);
                                        m_Abilities[i].StopAbility(true);
                                    }
                                }
                            }
                        }
                    } else {
                        for (int i = 0; i < m_Abilities.Length; ++i) {
                            // The ability cannot be started if the active ability says the current ability cannot be started.
                            if (m_Abilities[i].IsActive && !m_Abilities[i].CanStartAbility(ability)) {
                                return false;
                            }
                        }
                    }
                }
                // Prevent the character from aiming if the ability doesn't allow it.
                if (Aiming && !ability.CanInteractItem()) {
                    if (m_AlwaysAim) {
                        m_PrevAlwaysAim = true;
                        AlwaysAim = false;
                    }
                    Aim = false;
                }
                ability.StartAbility();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to stop all active abilities.
        /// </summary>
        public void TryStopAllAbilities()
        {
            TryStopAllAbilities(false);
        }

        /// <summary>
        /// Tries to stop all active abilities.
        /// </summary>
        /// <param name="force">Should the ability be force stopped?</param>
        public void TryStopAllAbilities(bool force)
        {
            for (int i = m_Abilities.Length - 1; i > -1; --i) {
                TryStopAbility(m_Abilities[i], force);
            }
        }

        /// <summary>
        /// Tries to stop the specified ability.
        /// </summary>
        /// <param name="ability">The ability to try to stop.</param>
        public void TryStopAbility(Ability ability)
        {
            TryStopAbility(ability, false);
        }

        /// <summary>
        /// Tries to stop the specified ability.
        /// </summary>
        /// <param name="ability">The ability to try to stop.</param>
        /// <param name="force">Should the ability be force stopped?</param>
        public void TryStopAbility(Ability ability, bool force)
        {
            if (ability.IsActive) {
                ability.StopAbility(force);
            }
        }

        /// <summary>
        /// An ability has been stopped.
        /// </summary>
        public void AbilityStopped()
        {
            // Reset the always aim status if no more abilities require the character not to aim.
            if (m_PrevAlwaysAim) {
                var alwaysAim = true;
                for (int i = 0; i < m_Abilities.Length; ++i) {
                    if (m_Abilities[i].IsActive && (!m_Abilities[i].CanInteractItem() || !m_Abilities[i].CanInteractItem())) {
                        alwaysAim = false;
                        break;
                    }
                }
                if (alwaysAim) {
                    m_PrevAlwaysAim = false;
                    AlwaysAim = true;
                }
            }
        }

        /// <summary>
        /// When an item wants to be used it will invoke this SharedMethod. Item may not be ready to used though (for example, if the item is a weapon then it first needs to be
        /// aimed). TryUseItem will call PrepareToUseItem if the character is not aiming or if the character is not rotated to face the target.
        /// </summary>
        /// <param name="item">The item that is trying to be used.</param>
        /// <returns>True if the item can be used.</returns>
        private bool SharedMethod_TryUseItem(Item item)
        {
            // In order to be able to use an item the character must be aiming and looking at the target. While in combat mode the character will always be looking at the target
            // so the item use threshold does not apply here.
            if (item.RequireAim && (!(m_IsAiming || m_IsForcedAiming) || (m_MovementType == MovementType.Adventure && Mathf.Abs(Mathf.DeltaAngle(m_Transform.eulerAngles.y, m_LookRotation.eulerAngles.y)) > (m_ItemUseRotationThreshold + Mathf.Epsilon)))) {
                // The character is either not aiming at the target or is not looking in the correct direction. Fix it.
                StartCoroutine(PrepareToUseItem(item));
                return false;
            }
            return true;
        }

        /// <summary>
        /// An item is about to be used. Start aiming and looking at the target. Will execute the OnItemReadyForUse event when the character has satisfied both of these conditions.
        /// </summary>
        /// <param name="item">The item that is trying to be used.</param>
        private IEnumerator PrepareToUseItem(Item item)
        {
            // ForceAim will start the aim animation and also force the rotation in to face the correct direction.
            m_ForceAim = true;
            m_IsForcedAiming = false;
            Scheduler.Cancel(ref m_ForcedItemUseEvent);
            m_AnimatorMonitor.DetermineStates();

            // Keep waiting until the character is aiming and looking at the target. The standard Move method will aim and rotate based off of the ForceAim variable so
            // this coroutine doesn't need to actually do anything beside wait and keep checking.
            while (item.RequireAim && (!(m_IsAiming || m_IsForcedAiming) || (m_MovementType == MovementType.Adventure && Mathf.Abs(Mathf.DeltaAngle(m_Transform.eulerAngles.y, m_LookRotation.eulerAngles.y)) > (m_ItemUseRotationThreshold + Mathf.Epsilon)))) {
                yield return m_EndOfFrame;
            }

            // The item is ready for use, send the event.
            EventHandler.ExecuteEvent(m_GameObject, "OnItemReadyForUse");
        }

        /// <summary>
        /// An item has been used so the force use schedule should be stopped.
        /// </summary>
        private void OnItemUse()
        {
            Scheduler.Cancel(ref m_ForcedItemUseEvent);
        }

        /// <summary>
        /// The weapon is no longer being fired. Reset the force aim variables after a small duration.
        /// </summary>
        private void OnItemStopUse()
        {
            if (m_ForceAim) {
                Scheduler.Cancel(ref m_ForcedItemUseEvent);
                m_ForcedItemUseEvent = Scheduler.Schedule(m_CurrentDualWieldItem.Get() == null ? m_ItemForciblyUseDuration : m_DualWieldItemForciblyUseDuration, StopForceUse);
            }
        }

        /// <summary>
        /// The weapon is no longer being fired. Call the corresponding server or client method.
        /// </summary>
        private void StopForceUse()
        {
            m_ForceAim = false;
            m_IsForcedAiming = false;
            m_ForcedItemUseEvent = null;
            if (!Aiming) {
                m_AnimatorMonitor.DetermineStates();
                EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", false);
            }
        }

        /// <summary>
        /// Enables or disables the main collider as well as any linked colliders.
        /// </summary>
        /// <param name="enable">Should the colliders be enabled?</param>
        private void EnableCollider(bool enable)
        {
            // If disabling the collider then it no longer needs to be ignored by any other objects.
            if (!enable) {
                LayerManager.RevertCollision(m_CapsuleCollider);
            }
            m_CapsuleCollider.enabled = enable;
            for (int i = 0; i < m_LinkedColliders.Length; ++i) {
                m_LinkedColliders[i].enabled = enable;
            }
        }

        /// <summary>
        /// The character should start or stop aiming. Will notify the server if on the network.
        /// </summary>
        /// <param name="aim"></param>
        private void SetAim(bool aim)
        {
#if ENABLE_MULTIPLAYER
            // Aiming must be run on the server.
            if (!isServer && isLocalPlayer) {
                CmdAim(aim);
            } else {
#endif
                AimLocal(aim);
#if ENABLE_MULTIPLAYER
            }
#endif 
        }

        /// <summary>
        /// The character should start or stop aiming.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        private void AimLocal(bool aim) 
        {
            // Don't change aim states if the character can't interact with the item.
            if (aim && !SharedMethod_CanInteractItem()) {
                return;
            }

            if (m_Aim != aim) {
                if (m_CombatMovementOnAim) {
                    if (aim) {
                        m_PrevMovementType = m_MovementType;
                        m_MovementType = MovementType.Combat;
                    } else {
                        m_MovementType = m_PrevMovementType;
                    }
                }
                m_Aim = aim;
                m_AnimatorMonitor.DetermineStates();
                // Change the aiming status immediately if the character is no longer aiming. If the character starts to aim then the OnAiming method will change the aiming status.
                if (!aim) {
                    m_IsAiming = false;
                    EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", false);
                } else {
                    EventHandler.ExecuteEvent(m_GameObject, "OnControllerStartAim");
                }
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The character should start or stop aiming on the server.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        [Command]
        private void CmdAim(bool aim)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcAim(aim);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                AimLocal(aim);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// The character should start or stop aiming on the client.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        [ClientRpc]
        private void RpcAim(bool aim)
        {
            AimLocal(aim);
        }

        /// <summary>
        /// Synchronizes the aim over a network call.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        private void SetAimSync(bool aim)
        {
            m_Aim = aim;
            m_AnimatorMonitor.DetermineStates();
        }
#endif

        /// <summary>
        /// Callback from the animator. The aim animation is done playing so the character is ready to use the item.
        /// </summary>
        private void OnAiming()
        {
            if (m_ForceAim && !m_IsForcedAiming) {
                m_IsForcedAiming = true;
                EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", true);
            }

            if ((m_Aim || m_AlwaysAim) && !m_IsAiming) {
                m_IsAiming = true;
                EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", true);
            }
        }

        /// <summary>
        /// Can the item be interacted with? Interactions include reload, equip, fire, etc.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        private bool SharedMethod_CanInteractItem()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CanInteractItem()) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Can the item be Used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        private bool SharedMethod_CanUseItem()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CanUseItem()) {
                    return false;
                }
            }
            // Cannot use the item if the inventory is switching items.
            if (m_IsSwitchingItems.Invoke()) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Should the upper body IK be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the upper body IK should be used.</returns>
        private bool SharedMethod_CanUseIK(int layer)
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CanUseIK(layer)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The character has died. Disable the unnecessary components.
        /// </summary>
        private void OnDeath()
        {
            TryStopAllAbilities();
            StopMovement();
            m_ForceAim = m_Aim = false;
            EnableCollider(false);
            m_CollisionPoints.Clear();
            m_CollisionSet.Clear();
            m_ItemCollision = null;
            m_PrevRigidbodyConstraints = m_Rigidbody.constraints;
            m_Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = false;
        }

        /// <summary>
        /// The character has respawned. Enable the necessary components.
        /// </summary>
        private void OnRespawn()
        {
            EnableCollider(true);
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.constraints = m_PrevRigidbodyConstraints;
            m_PrevYRotation = m_Transform.eulerAngles.y;
            m_PrevRootMotionForce = m_RootMotionForce = Vector3.zero;
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = true;
        }
    }
}