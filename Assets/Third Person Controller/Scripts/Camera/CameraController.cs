using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The controller for a third person or top down camera. The camera will smoothly follow the character as the character moves. While in third person mode, 
    /// the camera will rotate around the specified character and reduce the amount of clipping and can also zoom when the character is zooming. Top down mode will
    /// follow the character with a birds eye view.
    /// </summary>
    [RequireComponent(typeof(CameraHandler))]
    [RequireComponent(typeof(CameraMonitor))]
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Tooltip("A list of all possible CameraStates")]
        [SerializeField] protected CameraState[] m_CameraStates;

        [Tooltip("Should the character be initialized on start?")]
        [SerializeField] protected bool m_InitCharacterOnStart = true;
        [Tooltip("The character that the camera is following")]
        [SerializeField] protected GameObject m_Character;
        [Tooltip("The transform of the object to look at")]
        [SerializeField] protected Transform m_Anchor;
        [Tooltip("Should the anchor be assigned automatically based on the bone?")]
        [SerializeField] protected bool m_AutoAnchor;
        [Tooltip("The bone in which the anchor will be assigned to if automatically assigned")]
        [SerializeField] protected HumanBodyBones m_AutoAnchorBone = HumanBodyBones.Head;
        [Tooltip("The Transform to use when determining how much to fade")]
        [SerializeField] protected Transform m_FadeTransform;

        [Tooltip("Optionally specify an anchor point to look at when the character dies. If no anchor is specified the character's position will be used")]
        [SerializeField] protected Transform m_DeathAnchor;
        [Tooltip("When the character dies should the camera start rotating around the character? If false the camera will just look at the player")]
        [SerializeField] protected bool m_UseDeathOrbit = true;
        [Tooltip("The speed at which the camera rotates when the character dies. Used by both the death orbit and regular look at")]
        [SerializeField] protected float m_DeathRotationSpeed = 5;
        [Tooltip("The speed at which the death orbit moves")]
        [SerializeField] protected float m_DeathOrbitMoveSpeed = 5;
        [Tooltip("How far away the camera should be orbiting the character when the character dies")]
        [SerializeField] protected float m_DeathOrbitDistance = 5;

        // Internal variables
        private Dictionary<string, CameraState> m_CameraStatesMap = new Dictionary<string, CameraState>();
        private CameraState m_ActiveState;
        private CameraState m_DefaultState;
        private bool m_ExclusiveState;

        private float m_Pitch;
        private float m_Yaw;
        private float m_StartPitch;
        private float m_StepZoom;
        private float m_PrevYaw;

        private float m_SmoothX;
        private float m_SmoothY;
        private float m_SmoothXVelocity;
        private float m_SmoothYVelocity;
        private float m_SmoothPitchVelocity;
        private float m_SmoothYawVelocity;

        private Vector3 m_SmoothPositionVelocity;
        private bool m_ApplyColliderOffset;
        private Vector3 m_AnchorStartOffset;
        private float m_StaticYDifference = -1;
        private RaycastHit m_RaycastHit;

        private List<Material> m_FadeMaterials;
        private bool m_CharacterFade;

        private bool m_CharacterHasDied;
        private Vector3 m_PrevTargetPosition;

        private float m_Recoil = 0;
        private float m_TargetRecoil = 0;

#if UNITY_EDITOR
        // Used by the editor to keep the camera state selection.
        [SerializeField]
        protected int m_SelectedCameraState = -1;
        public int SelectedCameraState { get { return m_SelectedCameraState; } set { m_SelectedCameraState = value; } }
#endif

        public CameraState[] CameraStates { get { return m_CameraStates; } set { m_CameraStates = value; } }

        // SharedFields
        private float SharedProperty_Recoil { get { return m_Recoil; } set { m_TargetRecoil = value; } }
        private CameraMonitor.CameraViewMode SharedProperty_ViewMode { get { return m_ActiveState.ViewMode; } }
        private Vector3 SharedProperty_CameraOffset { get { return m_ActiveState.CameraOffset; } }

        // Exposed properties
        public CameraState ActiveState { get { return m_ActiveState; } }
        public GameObject Character { get { return m_Character; } set { InitializeCharacter(value); } }
        public Transform Anchor { set { m_Anchor = value; InitializeAnchor(); ImmediatePosition(); } }
        public Transform DeathAnchor { set { m_DeathAnchor = value; } }
        public Transform FadeTransform { set { m_FadeTransform = value; } }
        public Transform TargetLock
        {
            set
            {
                if (m_ActiveState.UseTargetLock && m_PrevTargetLock != value) {
                    m_PrevTargetLock = value;
                    Animator targetLockAnimator;
                    Transform targetBone;
                    if (value != null && m_ActiveState.UseHumanoidTargetLock && (targetLockAnimator = value.GetComponent<Animator>()) != null &&
                                    (targetBone = targetLockAnimator.GetBoneTransform(m_ActiveState.HumanoidTargetLockBone))) {
                        m_TargetLock = targetBone;
                    } else {
                        m_TargetLock = value;
                    }
                }
            }
        }

        // Component references
        private static Camera m_Camera;
        private CameraHandler m_CameraHandler;
        private Transform m_Transform;
        private Transform m_CharacterTransform;
        private Transform m_TargetLock;
        private Transform m_PrevTargetLock;
        private RigidbodyCharacterController m_CharacterController;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_Camera = GetComponent<Camera>();
            m_CameraHandler = GetComponent<CameraHandler>();

#if UNITY_EDITOR
            if (GetComponent<CameraMonitor>() == null) {
                Debug.LogWarning("Warning: the CameraMonitor component is required on cameras with the CameraController. Please add this component.");
                gameObject.AddComponent<CameraMonitor>();
            }
#endif

            SharedManager.Register(this);

            m_StartPitch = m_Pitch = m_Transform.eulerAngles.x;

            // The active state is a unique state which is layered by the additional states.
            m_ActiveState = ScriptableObject.CreateInstance<CameraState>();
            if (m_CameraStates == null || m_CameraStates.Length == 0) {
                m_DefaultState = ScriptableObject.CreateInstance<CameraState>();
                m_CameraStates = new CameraState[] { m_DefaultState };
            } else {
                m_DefaultState = m_CameraStates[0];
            }
            for (int i = 0; i < m_CameraStates.Length; ++i) {
                m_CameraStatesMap.Add(m_CameraStates[i].name, m_CameraStates[i]);
            }
            ChangeState(m_DefaultState, true);
        }

        /// <summary>
        /// Unregister for any events that the camera was aware of.
        /// </summary>
        private void OnDisable()
        {
            Deactivate();
        }

        /// <summary>
        /// Unregister for any events that the camera was aware of and revert the character fade materials.
        /// </summary>
        private void Deactivate()
        {
            // Revert the fade.
            DisableFade();

            if (m_Character == null) {
                return;
            }
            
            EventHandler.UnregisterEvent(m_Character, "OnDeath", OnCharacterDeath);
            EventHandler.UnregisterEvent(m_Character, "OnRespawn", OnCharacterSpawn);
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            // If the character is not initialized on start then disable the controller - the controller won't function without a character.
            if (m_InitCharacterOnStart) {
                if (m_Character == null) {
                    Debug.LogWarning("Warning: No character has been assigned to the Camera Controller. It will automatically be assigned to the GameObject with the Player tag.");
                    m_Character = GameObject.FindGameObjectWithTag("Player");
                    if (m_Character == null) {
                        Debug.LogWarning("Error: Unable to find character with the Player tag. Disabling the Camera Controller.");
                        m_CameraHandler.enabled = enabled = false;
                        return;
                    }
                }
                InitializeCharacter(m_Character);
            } else {
                m_CameraHandler.enabled = enabled = m_Character != null;
            }
        }

        /// <summary>
        /// Initialize the camera to follow the character.
        /// </summary>
        /// <param name="character">The character to initialize. Can be null.</param>
        private void InitializeCharacter(GameObject character)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                return;
            }
#endif
            m_CameraHandler.Character = character;

            // Reset the variables back to their values before the character spawned.
            if (character == null) {
                Deactivate();
                m_CameraHandler.enabled = enabled = false;
                m_Character = character;
                return;
            }

            // Don't go through the initialization process again if the character has already been assigned.
            if (m_CharacterTransform == character.transform) {
                return;
            }

            m_Character = character;
            m_CharacterTransform = m_Character.transform;
            m_CharacterController = m_Character.GetComponent<RigidbodyCharacterController>();
            m_Yaw = m_CharacterTransform.eulerAngles.y;
            m_Transform.rotation = m_Transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0);
            m_ExclusiveState = false;

            RegisterEvents();
            InitializeAnchor();

            if (m_ActiveState.FadeCharacter) {
                // Cache a reference to all of the materials.
                var renderers = m_Character.GetComponentsInChildren<Renderer>();
                m_FadeMaterials = new List<Material>();
                for (int i = 0; i < renderers.Length; ++i) {
                    var materials = renderers[i].materials;
                    for (int j = 0; j < materials.Length; ++j) {
                        if (materials[j].HasProperty("_Color") && materials[j].HasProperty("_Mode")) {
                            m_FadeMaterials.Add(materials[j]);
                        }
                    }
                }
                if (m_FadeTransform == null) {
                    m_FadeTransform = m_Anchor;
                }
            }

            // All of the variables have initialized so position the camera now.
            ImmediatePosition();

            m_CameraHandler.enabled = enabled = true;
        }

        /// <summary>
        /// Initialize the anchor transform and related variables.
        /// </summary>
        private void InitializeAnchor()
        {
            // Assign the anchor to the bone transform if auto anchor is enabled. Otherwise use the character's transform.
            Transform anchor = null;
            if (m_AutoAnchor && (anchor = m_Character.GetComponent<Animator>().GetBoneTransform(m_AutoAnchorBone)) != null) {
                m_Anchor = anchor;
            } else {
                m_Anchor = m_CharacterTransform;
            }

            m_ApplyColliderOffset = m_Anchor == m_CharacterTransform;
            m_AnchorStartOffset = m_Anchor.position - m_CharacterTransform.position;
        }

        /// <summary>
        /// A new character has been attached. Register for any events that the camera should be made aware of.
        /// </summary>
        private void RegisterEvents()
        {
            EventHandler.RegisterEvent(m_Character, "OnDeath", OnCharacterDeath);
            EventHandler.RegisterEvent(m_Character, "OnRespawn", OnCharacterSpawn);
        }

        /// <summary>
        /// Update the camera's position and rotation while the character is alive. Use FixedUpdate because the character operates on a fixed timestep.
        /// </summary>
        private void FixedUpdate()
        {
            if (m_CharacterHasDied) {
                return;
            }
            // The camera can only directly be controlled in third person view.
            if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                UpdateInput();
                Rotate();
            }
            Move(Time.fixedDeltaTime);
            // No need to update the recoil or check for clipping if not in third person view.
            if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                UpdateRecoil();
                CheckForCharacterFade();
            } else if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.TopDown) {
                LookAtCharacter(m_Anchor, m_ActiveState.RotationSpeed);
            }
            m_CameraHandler.InputConsumed();
        }

        /// <summary>
        /// Update the camera's position and rotation within LateUpdate if the character has died.
        /// </summary>
        public void LateUpdate()
        {
            if (m_CharacterHasDied) {
                if (m_UseDeathOrbit) {
                    DeathOrbitMovement();
                } else {
                    LookAtCharacter((m_DeathAnchor != null ? m_DeathAnchor : m_CharacterTransform), m_DeathRotationSpeed);
                }
            }
        }

        /// <summary>
        /// Update the pitch and yaw according to the user input.
        /// </summary>
        private void UpdateInput()
        {
            m_SmoothX = Mathf.SmoothDamp(m_SmoothX, m_CameraHandler.Yaw, ref m_SmoothXVelocity, m_ActiveState.TurnSmoothing);
            m_SmoothY = Mathf.SmoothDamp(m_SmoothY, m_CameraHandler.Pitch, ref m_SmoothYVelocity, m_ActiveState.TurnSmoothing);

            // Allow the camera to zoom by stepping.
            if (m_ActiveState.StepZoomSensitivity > 0) {
                m_StepZoom = Mathf.Clamp(m_StepZoom + m_CameraHandler.StepZoom * m_ActiveState.StepZoomSensitivity * Time.deltaTime, m_ActiveState.MinStepZoom, m_ActiveState.MaxStepZoom);
            }
        }

        /// <summary>
        /// Use the smoothed X and Y to adjust the yaw and pitch.
        /// </summary>
        private void Rotate()
        {
            // Force the camera to look in a specific direction if it is locked onto a target.
            if (m_TargetLock != null && m_CharacterController.Aiming) {
                // Allow the CameraController to break from the target lock if the camera is trying to move a distance that is greater than the break force.
                if (Mathf.Abs(m_PrevYaw - m_CameraHandler.Yaw) > m_ActiveState.BreakForce) {
                    m_TargetLock = null;
                } else {
                    var direction = m_TargetLock.position - m_Transform.position;
                    var lookRotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.LookRotation(direction), m_ActiveState.TargetLockSpeed).eulerAngles;
                    m_Pitch = lookRotation.x;
                    m_Yaw = lookRotation.y;
                }
            } else {
                // The rotation can only happen so fast.
                m_Yaw += m_SmoothX * m_ActiveState.TurnSpeed;
                m_Pitch += m_SmoothY * m_ActiveState.TurnSpeed * -1;
                m_Pitch = Utility.ClampAngle(m_Pitch, m_ActiveState.MinPitchLimit, m_ActiveState.MaxPitchLimit);

                var turnSmoothing = m_ActiveState.TurnSmoothing;
                if (Mathf.Abs(m_ActiveState.MinYawLimit - m_ActiveState.MaxYawLimit) < 360) {
                    // The yaw limit is relative to the character.
                    var lowerAngle = Utility.RestrictAngle(m_CharacterTransform.eulerAngles.y + m_ActiveState.MinYawLimit);
                    var upperAngle = Utility.RestrictAngle(m_CharacterTransform.eulerAngles.y + m_ActiveState.MaxYawLimit);
                    if (upperAngle < lowerAngle) {
                        upperAngle += 360;
                    }
                    // Keep the yaw in the same restricted angle to ease the smoothing.
                    m_Yaw = Utility.RestrictAngle(m_Yaw);
                    if (m_Yaw < 0) {
                        m_Yaw += 360;
                    }
                    m_Yaw = Mathf.SmoothDamp(m_Yaw, Mathf.Clamp(m_Yaw, lowerAngle, upperAngle), ref m_SmoothPitchVelocity, turnSmoothing);
                }

                // In most cases the character follows the camera. However, with the RPG view mode there are times when the camera should instead follow the character.
                if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.RPG && m_CameraHandler.RotateBehindCharacter) {
                    if (m_CharacterController.InputVector.sqrMagnitude > 0.01f) {
                        m_Yaw = Mathf.SmoothDamp(m_Yaw, m_Yaw + Mathf.DeltaAngle(m_Yaw, m_CharacterTransform.eulerAngles.y), ref m_SmoothYawVelocity, turnSmoothing);
                    }
                }
            }

            m_PrevYaw = m_CameraHandler.Yaw;
            m_Transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0);
        }

        /// <summary>
        /// Move between the current position and a new position specified by the new pitch, yaw, and zoom.
        /// </summary>
        /// <param name="deltaTime">The time since the last frame.</param>
        private void Move(float deltaTime)
        {
            m_Camera.fieldOfView = Mathf.Lerp(m_Camera.fieldOfView, m_ActiveState.FieldOfView, m_ActiveState.FieldOfViewSpeed * deltaTime);
            var lookPoint = m_Anchor.position + (m_ApplyColliderOffset ? m_CharacterController.CapsuleCollider.center : Vector3.zero) + Vector3.up * m_ActiveState.VerticalOffset;
            lookPoint += (m_ActiveState.CameraOffset.x * m_Transform.right) + (m_ActiveState.CameraOffset.y * m_CharacterTransform.up) + ((m_ActiveState.CameraOffset.z + m_StepZoom) * m_Transform.forward);

            // Prevent obstruction from other objects. Check for obstruction against the player position rather than the look position because the character should always be visible. It doesn't
            // matter as much if the look position isn't directly visible.
            var targetPosition = lookPoint;
            if (m_ActiveState.ObstructionCheck) {
                if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                    var anchorPosition = m_Anchor.position + (m_ApplyColliderOffset ? m_CharacterController.CapsuleCollider.center : Vector3.zero) + Vector3.up * m_ActiveState.VerticalOffset;
                    // Keep the anchor position above water.
                    if (Physics.Linecast(m_CharacterTransform.position, m_CharacterTransform.position + Vector3.up * m_CharacterController.CapsuleCollider.height, out m_RaycastHit, 1 << LayerManager.Water)) {
                        if (anchorPosition.y < m_RaycastHit.point.y) {
                            anchorPosition.y = m_RaycastHit.point.y;
                        }
                    }
                    var direction = targetPosition - anchorPosition;
                    var start = anchorPosition - direction.normalized * m_ActiveState.CollisionRadius;
                    // Fire a sphere to prevent the camera from colliding with other objects.
                    if (Physics.SphereCast(start, m_ActiveState.CollisionRadius, direction.normalized, out m_RaycastHit, direction.magnitude, m_ActiveState.IgnoreLayerMask)) {
                        // Move the camera in if the character isn't in view.
                        targetPosition = m_RaycastHit.point + m_RaycastHit.normal * 0.1f;

                        // Keep a constant height if there is nothing getting in the way of that position.
                        if (direction.y > 0) {
                            var constantHeightPosition = targetPosition;
                            constantHeightPosition.y = lookPoint.y;
                            direction = constantHeightPosition - anchorPosition;
                            start = anchorPosition - direction.normalized * m_ActiveState.CollisionRadius;
                            if (!Physics.SphereCast(start, m_ActiveState.CollisionRadius, direction.normalized, out m_RaycastHit, direction.magnitude, m_ActiveState.IgnoreLayerMask)) {
                                targetPosition = constantHeightPosition;
                            }
                        }
                    }

                    // Prevent the camera from clipping with the character.
                    if (m_CharacterController.CapsuleCollider.bounds.Contains(targetPosition)) {
                        targetPosition = m_CharacterController.CapsuleCollider.ClosestPointOnBounds(targetPosition);
                    }
                } else if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.TopDown) {
                    var direction = Quaternion.Euler(m_ActiveState.MinPitchLimit, 0, 0) * -Vector3.forward;
                    var step = 0f;
                    while (Physics.SphereCast(lookPoint, m_ActiveState.CollisionRadius, direction.normalized, out m_RaycastHit, m_ActiveState.ViewDistance, m_ActiveState.IgnoreLayerMask)) {
                        if (m_ActiveState.MinPitchLimit + step >= m_ActiveState.MaxPitchLimit) {
                            direction = Quaternion.Euler(m_ActiveState.MaxPitchLimit, 0, 0) * -Vector3.forward;
                            break;
                        }
                        step += m_ActiveState.ViewStep;
                        direction = Quaternion.Euler(m_ActiveState.MinPitchLimit + step, 0, 0) * -Vector3.forward;
                    }
                    targetPosition = lookPoint + direction * m_ActiveState.ViewDistance;
                } else { // 2.5D.
                    targetPosition = lookPoint - m_ActiveState.LookDirection * m_ActiveState.ViewDistance;
                }
            }

            // Keep the y position the same when requested.
            if (m_StaticYDifference != -1) {
                targetPosition.y = m_CharacterTransform.position.y + m_StaticYDifference;
            }

            // Set the new position.
            m_Transform.position = Vector3.SmoothDamp(m_Transform.position, targetPosition, ref m_SmoothPositionVelocity, m_ActiveState.MoveSmoothing);
        }

        /// <summary>
        /// A weapon has been fired. Update the recoil.
        /// </summary>
        private void UpdateRecoil()
        {
            // Use the recoil spring amount when the weapon is initially fired and the recoil is increasing in magnitude. Recoil dampening is then used
            // after the recoil has hit its peak and is settling back down to its original value.
            if (Mathf.Abs(m_TargetRecoil - m_Recoil) > 0.001f) {
                var currentVelocity = 0f;
                m_Recoil = Mathf.SmoothDamp(m_Recoil, m_TargetRecoil, ref currentVelocity, m_ActiveState.RecoilSpring);
                EventHandler.ExecuteEvent<float>(m_Character, "OnCameraUpdateRecoil", m_Recoil);
            } else if (m_Recoil != 0) {
                var currentVelocity = 0f;
                m_TargetRecoil = m_Recoil = Mathf.SmoothDamp(m_Recoil, 0, ref currentVelocity, m_ActiveState.RecoilDampening);
                EventHandler.ExecuteEvent<float>(m_Character, "OnCameraUpdateRecoil", m_Recoil);
                if (m_Recoil < 0.001f) {
                    m_Recoil = 0;
                }
            }
        }

        /// <summary>
        /// If the camera gets too close to the character then start to fade the character to prevent the camera from seeing inside the character.
        /// </summary>
        private void CheckForCharacterFade()
        {
            if (m_ActiveState.FadeCharacter) {
                var offset = m_FadeTransform.InverseTransformPoint(m_Transform.position);
                if (offset.magnitude <= m_ActiveState.StartFadeDistance) {
                    // Slowly fade the character away as the camera gets closer.
                    var amount = (offset.magnitude - m_ActiveState.EndFadeDistance) / (m_ActiveState.StartFadeDistance - m_ActiveState.EndFadeDistance);
                    for (int i = 0; i < m_FadeMaterials.Count; ++i) {
                        var color = m_FadeMaterials[i].color;
                        color.a = Mathf.Lerp(0, 1, amount);
                        m_FadeMaterials[i].color = color;
                        if (!m_CharacterFade) {
                            // Lots of work to switch to fade:
                            m_FadeMaterials[i].SetFloat("_Mode", 2);
                            m_FadeMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            m_FadeMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            m_FadeMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                            m_FadeMaterials[i].renderQueue = 3000;
                        }
                    }
                    m_CharacterFade = true;
                } else if (m_CharacterFade) {
                    // The camera is not near the character - no fade necessary.
                    DisableFade();
                    m_CharacterFade = false;
                }
            }
        }

        /// <summary>
        /// Disables the character fade.
        /// </summary>
        private void DisableFade()
        {
            if (m_FadeMaterials != null) {
                for (int i = 0; i < m_FadeMaterials.Count; ++i) {
                    var color = m_FadeMaterials[i].color;
                    color.a = 1;
                    m_FadeMaterials[i].color = color;
                    // Lots of work to switch to opaque:
                    m_FadeMaterials[i].SetFloat("_Mode", 0);
                    m_FadeMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    m_FadeMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    m_FadeMaterials[i].DisableKeyword("_ALPHABLEND_ON");
                    m_FadeMaterials[i].renderQueue = -1;
                }
            }
        }

        /// <summary>
        /// When the character dies the camera should orbit around the character.
        /// </summary>
        private void DeathOrbitMovement()
        {
            // If no death anchor point is specified then use the regular anchor.
            var anchor = (m_DeathAnchor != null ? m_DeathAnchor : m_Anchor);
            var rotation = Quaternion.identity;

            // Start rotating once the anchor position has stopped moving. This prevents the camera from jittering when both the camera and anchor are changing positions.
            if ((m_PrevTargetPosition - anchor.position).sqrMagnitude < .01) {
                // Keep rotating around the target transform until OnCharacterSpawn is called.
                rotation = Quaternion.AngleAxis(m_DeathRotationSpeed * Time.fixedDeltaTime, Vector3.up);
            }

            var direction = (m_Transform.position - anchor.position).normalized;
            var distance = m_DeathOrbitDistance;
            // Prevent clipping with other objects.
            if (Physics.SphereCast(anchor.position, m_ActiveState.CollisionRadius, direction.normalized, out m_RaycastHit, distance, m_ActiveState.IgnoreLayerMask)) {
                distance = m_RaycastHit.distance;
            }
            
            // Set the rotation and position.
            var targetPosition = anchor.position + (rotation * direction * distance);
            m_Transform.position = Vector3.MoveTowards(m_Transform.position, targetPosition, m_DeathOrbitMoveSpeed);
            m_Transform.rotation = Quaternion.LookRotation(-direction);
            m_PrevTargetPosition = anchor.position;
        }

        /// <summary>
        /// When the character dies the camera should look at the character instead of orbiting around the character.
        /// </summary>
        /// <param name="anchor">The point to look at.</param>
        /// <param name="roationSpeed">The speed at which the rotation occurs.</param>
        private void LookAtCharacter(Transform anchor, float roationSpeed)
        {
            var rotation = Quaternion.LookRotation(anchor.position - m_Transform.position);
            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, rotation, roationSpeed * Time.fixedDeltaTime);
        }

        /// <summary>
        /// The character has died. Start orbiting around the player or looking at the player.
        /// </summary>
        private void OnCharacterDeath()
        {
            m_CharacterHasDied = true;
            m_TargetRecoil = m_Recoil = 0;
            DisableFade();
            // Set the previous target position to a negative number to prevent the first death orbit frame frame from thinking that there wasn't a change.
            m_PrevTargetPosition = -(m_DeathAnchor != null ? m_DeathAnchor : m_Anchor).position;
            EventHandler.ExecuteEvent<float>(m_Character, "OnCameraUpdateRecoil", m_Recoil);
        }

        /// <summary>
        /// The character has respawned. Reset the variables and move to the correct position.
        /// </summary>
        private void OnCharacterSpawn()
        {
            m_CharacterHasDied = false;
            ImmediatePosition();
        }

        /// <summary>
        /// Immediately reset the position/rotation of the camera with the starting rotation.
        /// </summary>
        public void ImmediatePosition()
        {
            if (m_ActiveState.ViewMode != CameraMonitor.CameraViewMode.Pseudo3D) {
                ImmediatePosition(Quaternion.Euler(m_StartPitch, m_CharacterTransform.eulerAngles.y, 0));
            } else {
                ImmediatePosition(Quaternion.LookRotation(m_Anchor.position - m_Transform.position));
            }
        }

        /// <summary>
        /// Immediately reset the position/rotation of the camera.
        /// </summary>
        /// <param name="targetRotation">The target rotation of the camera.</param>
        public void ImmediatePosition(Quaternion targetRotation)
        {
            m_Transform.rotation = targetRotation;
            m_Pitch = m_Transform.eulerAngles.x;
            m_Yaw = m_Transform.eulerAngles.y;
            var lookPoint = m_CharacterTransform.position + m_AnchorStartOffset + (m_ApplyColliderOffset ? m_CharacterController.CapsuleCollider.center : Vector3.zero) + 
                                (m_ActiveState.CameraOffset.x * m_Transform.right) + (m_ActiveState.CameraOffset.y * m_CharacterTransform.up) + (m_ActiveState.CameraOffset.z * m_Transform.forward);
            if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                m_Transform.position = lookPoint;
            } else if (m_ActiveState.ViewMode == CameraMonitor.CameraViewMode.TopDown) {
                var direction = Quaternion.Euler(m_ActiveState.MinPitchLimit, 0, 0) * -Vector3.forward;
                m_Transform.position = lookPoint + direction * m_ActiveState.ViewDistance;
            } else { // 2.5D.
                m_Transform.position = lookPoint - m_ActiveState.LookDirection * m_ActiveState.ViewDistance;
            }
            m_Camera.fieldOfView = m_ActiveState.FieldOfView;
        }

        /// <summary>
        /// Returns the CameraState with the specified name.
        /// </summary>
        /// <param name="name">The name of the CameraState to retrieve.</param>
        /// <returns>The CameraState for the specified name.</returns>
        public CameraState StateWithName(string name)
        {
            CameraState state;
            if (m_CameraStatesMap.TryGetValue(name, out state)) {
                return state;
            }
            Debug.LogError("Error: Unable to find camera state with name " + name);
            return null;
        }

        /// <summary>
        /// Changes the camera state to the specified name.
        /// </summary>
        /// <param name="name">The name of the camera state.</param>
        /// <param name="apply">Is the state being applied? True if applied, false if reverted.</param>
        /// <returns>Was the state changed?</returns>
        public bool SharedMethod_ChangeCameraState(string name, bool apply)
        {
            return ChangeState(name, apply);
        }

        /// <summary>
        /// Changes the camera state to the specified name.
        /// </summary>
        /// <param name="name">The name of the camera state.</param>
        /// <param name="apply">Is the state being applied? True if applied, false if reverted.</param>
        /// <returns>Was the state changed?</returns>
        public bool ChangeState(string name, bool apply)
        {
            var state = StateWithName(name);
            if (state != null) {
                return ChangeState(state, apply);
            }
            return false;
        }

        /// <summary>
        /// Changes the camera state.
        /// </summary>
        /// <param name="state">The state to change.</param>
        /// <param name="apply">Is the state being applied? True if applied, false if reverted.</param>
        /// <returns>Was the state changed?</returns>
        public bool ChangeState(CameraState state, bool apply)
        {
            // Don't allow the state to be applied if current state is exclusive. For example, don't use the regular zoom state when a scope state is active.
            if (apply && m_ExclusiveState) {
                return false;
            }
            m_ExclusiveState = state.Exclusive;
            // Each parameter will only be applied if the state setting says to.
            if (state.ApplyViewMode) {
                m_ActiveState.ViewMode = (apply ? state.ViewMode : m_DefaultState.ViewMode);
            }
            if (state.ApplyPitchLimit) {
                m_ActiveState.MinPitchLimit = (apply ? state.MinPitchLimit : m_DefaultState.MinPitchLimit);
                m_ActiveState.MaxPitchLimit = (apply ? state.MaxPitchLimit : m_DefaultState.MaxPitchLimit);
            }
            if (state.ApplyYawLimit) {
                m_ActiveState.MinYawLimit = (apply ? state.MinYawLimit : m_DefaultState.MinYawLimit);
                m_ActiveState.MaxYawLimit = (apply ? state.MaxYawLimit : m_DefaultState.MaxYawLimit);
            }
            if (state.ApplyIgnoreLayerMask) {
                m_ActiveState.IgnoreLayerMask = (apply ? state.IgnoreLayerMask : m_DefaultState.IgnoreLayerMask);
            }
            if (state.ApplyMoveSmoothing) {
                m_ActiveState.MoveSmoothing = (apply ? state.MoveSmoothing : m_DefaultState.MoveSmoothing);
            }
            if (state.ApplyCameraOffset) {
                m_ActiveState.CameraOffset = (apply ? state.CameraOffset : m_DefaultState.CameraOffset);
            }
            if (state.ApplyFieldOfView) {
                m_ActiveState.FieldOfView = (apply ? state.FieldOfView : m_DefaultState.FieldOfView);
                m_ActiveState.FieldOfViewSpeed = (apply ? state.FieldOfViewSpeed : m_DefaultState.FieldOfViewSpeed);
            }
            if (state.ApplyTurn) {
                m_ActiveState.TurnSmoothing = (apply ? state.TurnSmoothing : m_DefaultState.TurnSmoothing);
                m_ActiveState.TurnSpeed = (apply ? state.TurnSpeed : m_DefaultState.TurnSpeed);
            }
            if (state.ApplyRotationSpeed) {
                m_ActiveState.RotationSpeed = (apply ? state.RotationSpeed : m_DefaultState.RotationSpeed);
            }
            if (state.ApplyView) {
                m_ActiveState.ViewDistance = (apply ? state.ViewDistance : m_DefaultState.ViewDistance);
                m_ActiveState.ViewStep = (apply ? state.ViewStep : m_DefaultState.ViewStep);
                m_ActiveState.LookDirection = (apply ? state.LookDirection : m_DefaultState.LookDirection);
            }
            if (state.ApplyStepZoom) {
                m_ActiveState.StepZoomSensitivity = (apply ? state.StepZoomSensitivity : m_DefaultState.StepZoomSensitivity);
                if (m_ActiveState.StepZoomSensitivity > 0) {
                    m_ActiveState.MinStepZoom = (apply ? state.MinStepZoom : m_DefaultState.MinStepZoom);
                    m_ActiveState.MaxStepZoom = (apply ? state.MaxStepZoom : m_DefaultState.MaxStepZoom);
                }
            }
            if (state.ApplyCollisionRadius) {
                m_ActiveState.CollisionRadius = (apply ? state.CollisionRadius : m_DefaultState.CollisionRadius);
            }
            if (state.ApplyFadeCharacter) {
                m_ActiveState.FadeCharacter = (apply ? state.FadeCharacter : m_DefaultState.FadeCharacter);
                if (m_ActiveState.FadeCharacter) {
                    m_ActiveState.StartFadeDistance = (apply ? state.StartFadeDistance : m_DefaultState.StartFadeDistance);
                    m_ActiveState.EndFadeDistance = (apply ? state.EndFadeDistance : m_DefaultState.EndFadeDistance);
                }
            }
            if (state.ApplyTargetLock) {
                m_ActiveState.UseTargetLock = (apply ? state.UseTargetLock : m_DefaultState.UseTargetLock);
                if (m_ActiveState.UseTargetLock) {
                    m_ActiveState.TargetLockSpeed = (apply ? state.TargetLockSpeed : m_DefaultState.TargetLockSpeed);
                    m_ActiveState.BreakForce = (apply ? state.BreakForce : m_DefaultState.BreakForce);
                    m_ActiveState.UseHumanoidTargetLock = (apply ? state.UseHumanoidTargetLock : m_DefaultState.UseHumanoidTargetLock);
                    m_ActiveState.HumanoidTargetLockBone = (apply ? state.HumanoidTargetLockBone : m_DefaultState.HumanoidTargetLockBone);
                }
            }
            if (state.ApplyRecoil) {
                m_ActiveState.RecoilSpring = (apply ? state.RecoilSpring : m_DefaultState.MinPitchLimit);
                m_ActiveState.RecoilSpring = (apply ? state.RecoilSpring : m_DefaultState.MinPitchLimit);
            }
            if (state.ApplyObstructionCheck) {
                m_ActiveState.ObstructionCheck = (apply ? state.ObstructionCheck : m_DefaultState.ObstructionCheck);
            }
            if (state.ApplyStaticHeight) {
                m_ActiveState.StaticHeight = (apply ? state.StaticHeight : m_DefaultState.StaticHeight);
                if (m_ActiveState.StaticHeight) {
                    m_StaticYDifference = m_Transform.position.y - m_CharacterTransform.position.y;
                } else {
                    m_StaticYDifference = -1;
                }
            }
            if (state.ApplyVerticalOffset) {
                m_ActiveState.VerticalOffset = (apply ? state.VerticalOffset : m_DefaultState.VerticalOffset);
            }
            return true;
        }
    }
}