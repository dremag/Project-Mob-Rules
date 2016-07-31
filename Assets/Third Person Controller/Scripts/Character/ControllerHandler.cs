using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using Opsive.ThirdPersonController.Abilities;
using Opsive.ThirdPersonController.Input;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the RigidbodyCharacterController.
    /// </summary>
    [RequireComponent(typeof(RigidbodyCharacterController))]
#if ENABLE_MULTIPLAYER
    public class ControllerHandler : NetworkBehaviour
#else
    public class ControllerHandler : MonoBehaviour
#endif
    {
        protected enum AimType { Down, Toggle, None }
        [Tooltip("Specifies if the character should aim when the button is down, toggled, or not at all")]
        [SerializeField] protected AimType m_AimType;

        // Internal variables
        private float m_HorizontalMovement;
        private float m_ForwardMovement;
        private Quaternion m_LookRotation;
        private bool m_AllowGameplayInput = true;
        private List<string> m_AbilityInputName;
        private List<string> m_AbilityInputEvent;
        private Vector3 m_PrevMousePosition;
        
        // Component references
        private GameObject m_GameObject;
        private Transform m_Transform;
        private RigidbodyCharacterController m_Controller;
        private PlayerInput m_PlayerInput;
        private Camera m_Camera;
        private Transform m_CameraTransform;

        /// <summary>
        /// Cache the component references and register for any network events.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_Controller = GetComponent<RigidbodyCharacterController>();
            m_PlayerInput = GetComponent<PlayerInput>();

#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent("OnNetworkStopClient", OnNetworkDestroy);
#endif
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<string, string>(m_GameObject, "OnAbilityRegisterInput", RegisterAbilityInput);
            EventHandler.RegisterEvent<string, string>(m_GameObject, "OnAbilityUnregisterInput", UnregisterAbilityInput);
        }

        /// <summary>
        /// Register for any events that the handler should be aware of.
        /// </summary>
        private void OnEnable()
        {
            m_Controller.RootMotionForce = Vector3.zero;
            m_Controller.RootMotionRotation = Quaternion.identity;
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Unregister for any events that the handler was registered for and stop the character from moving.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Initializes all of the SharedFields and default values.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);

            if ((enabled = m_PlayerInput != null)) {
                m_PrevMousePosition = m_PlayerInput.GetMousePosition();
                InitializeCamera();
                // Call update immediately to force the horizontal, forward, and look rotation values to update before FixedUpdate is called.
                Update();
            }

            SharedManager.InitializeSharedFields(m_GameObject, this);
        }

        /// <summary>
        /// Initializes the camera components.
        /// <returns>Tue if the camera initialized successfully.</returns>
        /// </summary>
        public bool InitializeCamera()
        {
            m_Camera = Camera.main;
            if (m_Camera == null) {
                Debug.LogError("Error: Unable to find the main camera. Ensure the camera uses the MainCamera tag.");
                return false;
            }
            m_CameraTransform = m_Camera.transform;

            CameraMonitor cameraMonitor;
            if ((cameraMonitor = m_Camera.GetComponent<CameraMonitor>()) != null) {
#if ENABLE_MULTIPLAYER
                // While in a networked game, only assign the camera's character property if the current instance is the local player. Non-local players have their own
                // cameras within their own client.
                if (cameraMonitor.Character == null && isLocalPlayer) {
#else
                if (cameraMonitor.Character == null) {
#endif
                    cameraMonitor.Character = gameObject;
                }
            }
            return true;
        }

        /// <summary>
        /// Accepts input and will perform an immediate action (such as crouching or jumping).
        /// </summary>
        private void Update()
        {
#if ENABLE_MULTIPLAYER
            if (!isLocalPlayer) {
                return;
            }
#endif
            if (!m_AllowGameplayInput) {
                m_HorizontalMovement = m_ForwardMovement = 0;
#if ENABLE_MULTIPLAYER
                CmdSetInputParameters(m_HorizontalMovement, m_ForwardMovement, m_LookRotation);
#endif
                return;
            }

            m_HorizontalMovement = m_PlayerInput.GetAxisRaw(Constants.HorizontalInputName);
            m_ForwardMovement = m_PlayerInput.GetAxisRaw(Constants.ForwardInputName);
            // Update the look rotation within update for top down and 2.5D movement types because the rotation depends on input rater than the camera.
            if (m_Controller.Movement == RigidbodyCharacterController.MovementType.TopDown || m_Controller.Movement == RigidbodyCharacterController.MovementType.Pseudo3D || m_Controller.IndependentLook()) {
                if (m_Controller.LookInMoveDirection) {
                    var direction = Vector3.zero;
                    direction.x = m_HorizontalMovement;
                    direction.z = m_ForwardMovement;
                    if (direction.sqrMagnitude > 0.01f) {
                        m_LookRotation = Quaternion.LookRotation(direction);
                    } else {
                        m_LookRotation = m_Transform.rotation;
                    }
                } else {
                    var mousePosition = (Vector3)m_PlayerInput.GetMousePosition();
                    if ((mousePosition - m_PrevMousePosition).sqrMagnitude > 0.1f && !m_Controller.IndependentLook()) {
                        var ray = m_Camera.ScreenPointToRay(mousePosition);
                        RaycastHit hit;
                        if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                            var hitPosition = hit.point;
                            hitPosition.y = m_Transform.position.y;
                            m_LookRotation = Quaternion.LookRotation(hitPosition - transform.position);
                        } else {
                            var direction = mousePosition - m_Camera.WorldToScreenPoint(m_Transform.position);
                            // Convert the XY direction to an XYZ direction with Y equal to 0.
                            direction.z = direction.y;
                            direction.y = 0;
                            m_LookRotation = Quaternion.LookRotation(direction);
                        }
                    } else {
                        var direction = Vector3.zero;
                        direction.x = m_PlayerInput.GetAxisRaw(Constants.YawInputName);
                        direction.z = m_PlayerInput.GetAxisRaw(Constants.PitchInputName);
                        if (direction.sqrMagnitude > 0.1f) {
                            m_LookRotation = Quaternion.LookRotation(direction);
                        } else {
                            m_LookRotation = m_Transform.rotation;
                        }
                    }
                    m_PrevMousePosition = mousePosition;
                }
            } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.RPG) {
                if (m_PlayerInput.GetButton(Constants.SecondaryDisableButtonName, true) && m_PlayerInput.GetButton(Constants.PrimaryDisableButtonName, true)) {
                    m_ForwardMovement = 1;
                } else if (!m_PlayerInput.GetButton(Constants.PrimaryDisableButtonName, true)) {
                    m_HorizontalMovement = 0;
                }
            } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.FourLegged) {
                // Four legged characters should turn reverse of the input when moving backwards.
                if (m_ForwardMovement < -0.01f) {
                    m_HorizontalMovement *= -1;
                }
            }
#if ENABLE_MULTIPLAYER
            CmdSetInputParameters(m_HorizontalMovement, m_ForwardMovement, m_LookRotation);
#endif

            // Should the controller aim?
            if (m_AimType == AimType.Down) {
                if (m_PlayerInput.GetButtonDown(Constants.AimInputName, true)) {
                    m_Controller.Aim = true;
                } else if (m_Controller.Aim && !m_PlayerInput.GetButton(Constants.AimInputName, true)) {
                    m_Controller.Aim = false;
                }
            } else if (m_AimType == AimType.Toggle) {
                if (m_PlayerInput.GetButtonDown(Constants.AimInputName)) {
                    m_Controller.Aim = !m_Controller.Aiming;
                }
            }
            
            // Abilities can have their own input.
            if (m_AbilityInputName != null) {
                for (int i = 0; i < m_AbilityInputName.Count; ++i) {
                    if (m_PlayerInput.GetButtonDown(m_AbilityInputName[i])) {
#if ENABLE_MULTIPLAYER
                        CmdExecuteAbilityEvent(m_AbilityInputEvent[i]);
                        // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                        // in which case the method will be called with the Rpc call.
                        if (!isClient) {
#endif
                            EventHandler.ExecuteEvent(m_GameObject, m_AbilityInputEvent[i]);
#if ENABLE_MULTIPLAYER
                        }
#endif
                    }
                }
            }

            // Start or stop the abilities.
            if (m_Controller.Abilities != null) {
                for (int i = 0; i < m_Controller.Abilities.Length; ++i) {
                    if ((m_Controller.Abilities[i].StartType == Ability.AbilityStartType.ButtonDown || m_Controller.Abilities[i].StopType == Ability.AbilityStopType.ButtonToggle) &&
                            m_PlayerInput.GetButtonDown(m_Controller.Abilities[i].InputName)) {
                        // Start the ability if it is not started and can be started when a button is down. Stop the ability if it is already started and
                        // the stop type is button toggle. A toggled button means the button has to be pressed and released before the ability can be stopped.
                        if (!m_Controller.Abilities[i].IsActive && m_Controller.Abilities[i].StartType == Ability.AbilityStartType.ButtonDown) {
#if ENABLE_MULTIPLAYER
                            CmdTryStartAbility(i);
                            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                            // in which case the method will be called with the Rpc call.
                            if (!isClient) {
#endif
                                m_Controller.TryStartAbility(m_Controller.Abilities[i]);
#if ENABLE_MULTIPLAYER
                            }
#endif
                        } else if (m_Controller.Abilities[i].StopType == Ability.AbilityStopType.ButtonToggle) {
#if ENABLE_MULTIPLAYER
                            CmdTryStopAbility(i);
                            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                            // in which case the method will be called with the Rpc call.
                            if (!isClient) {
#endif
                                m_Controller.TryStopAbility(m_Controller.Abilities[i]);
#if ENABLE_MULTIPLAYER
                            }
#endif
                        }
                    } else if (m_Controller.Abilities[i].StopType == Ability.AbilityStopType.ButtonUp && m_PlayerInput.GetButtonUp(m_Controller.Abilities[i].InputName)) {
                        // Stop the ability if the ability can be stopped when the button is up.
#if ENABLE_MULTIPLAYER
                        CmdTryStopAbility(i);
                        // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                        // in which case the method will be called with the Rpc call.
                        if (!isClient) {
#endif
                            m_Controller.TryStopAbility(m_Controller.Abilities[i]);
#if ENABLE_MULTIPLAYER
                        }
#endif
                    } else if (m_Controller.Abilities[i].StartType == Ability.AbilityStartType.DoublePress && m_PlayerInput.GetDoublePress(m_Controller.Abilities[i].InputName)) {
                        // Start the ability if the ability should be started with a double press.
                        if (!m_Controller.Abilities[i].IsActive) {
                            m_Controller.TryStartAbility(m_Controller.Abilities[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Call Move directly on the character. A similar approach could have been used as the CameraController/Handler where the RigidbodyCharacterController
        /// directly checks the input storage variable but this would not allow the RigidbodyCharacterController to act as an AI agent as easily. 
        /// </summary>
        private void FixedUpdate()
        {
#if ENABLE_MULTIPLAYER
            if (m_AllowGameplayInput && isLocalPlayer) {
#else
            if (m_AllowGameplayInput) {
#endif
                // Update the look rotation within FixedUpdate because the camera may change rotations multiple times within its FixedUpdate loop.
                if (!m_Controller.IndependentLook()) {
                    if (m_Controller.Movement == RigidbodyCharacterController.MovementType.Combat || m_Controller.Movement == RigidbodyCharacterController.MovementType.Adventure) {
                        m_LookRotation = m_CameraTransform.rotation;
                    } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.RPG) {
                        if (m_PlayerInput.GetButton(Constants.SecondaryDisableButtonName, true)) {
                            m_LookRotation = m_CameraTransform.rotation;
                        } else if (!m_PlayerInput.GetButton(Constants.PrimaryDisableButtonName, true)) {
                            if (m_ForwardMovement != 0 || m_HorizontalMovement != 0) {
                                m_LookRotation = m_CameraTransform.rotation;
                            }
                        }
                    } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.FourLegged) {
                        m_LookRotation = m_CameraTransform.rotation;
                    }
                }
            }

            // The PointClickControllerHandler will move the character.
            if (m_Controller.Movement == RigidbodyCharacterController.MovementType.PointClick) {
                return;
            }

            m_Controller.Move(m_HorizontalMovement, m_ForwardMovement, m_LookRotation);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Set the input parameters on the server.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        [Command(channel = (int)QosType.Unreliable)]
        private void CmdSetInputParameters(float horizontalMovement, float forwardMovement, Quaternion lookRotation)
        {
            m_HorizontalMovement = horizontalMovement;
            m_ForwardMovement = forwardMovement;
            m_LookRotation = lookRotation;

            RpcSetInputParameters(horizontalMovement, forwardMovement, lookRotation);
        }

        /// <summary>
        /// Set the input parameters on the client.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        [ClientRpc(channel = (int)QosType.Unreliable)]
        private void RpcSetInputParameters(float horizontalMovement, float forwardMovement, Quaternion lookRotation)
        {
            // The parameters would have already been set if a local player.
            if (isLocalPlayer) {
                return;
            }
            m_HorizontalMovement = horizontalMovement;
            m_ForwardMovement = forwardMovement;
            m_LookRotation = lookRotation;
        }

        /// <summary>
        /// Try to start an ability on the server.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [Command]
        private void CmdTryStartAbility(int abilityIndex)
        {
            m_Controller.TryStartAbility(m_Controller.Abilities[abilityIndex]);

            RpcTryStartAbility(abilityIndex);
        }

        /// <summary>
        /// Try to start an ability on the client.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [ClientRpc]
        private void RpcTryStartAbility(int abilityIndex)
        {
            m_Controller.TryStartAbility(m_Controller.Abilities[abilityIndex]);
        }

        /// <summary>
        /// Try to stop an ability on the server.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [Command]
        private void CmdTryStopAbility(int abilityIndex)
        {
            m_Controller.TryStopAbility(m_Controller.Abilities[abilityIndex]);

            RpcTryStopAbility(abilityIndex);
        }

        /// <summary>
        /// Try to start an ability on the client.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [ClientRpc]
        private void RpcTryStopAbility(int abilityIndex)
        {
            m_Controller.TryStopAbility(m_Controller.Abilities[abilityIndex]);
        }
        
        /// <summary>
        /// Execute an ability event on the server.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        [Command]
        private void CmdExecuteAbilityEvent(string eventName)
        {
            EventHandler.ExecuteEvent(eventName);
            
            RpcExecuteAbilityEvent(eventName);
        }
        
        /// <summary>
        /// Execute an ability event on the client.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        [ClientRpc]
        private void RpcExecuteAbilityEvent(string eventName)
        {
            EventHandler.ExecuteEvent(eventName);
        }
#endif

        /// <summary>
        /// The character has died. Disable the handler.
        /// </summary>
        private void OnDeath()
        {
            m_HorizontalMovement = m_ForwardMovement = 0;
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = false;
        }

        /// <summary>
        /// The character has respawned. Enable the handler.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            if (m_PlayerInput != null) {
                enabled = true;
                // Call update immediately to force the horizontal, forward, and look rotation values to update before FixedUpdate is called.
                Update();
            }
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            m_AllowGameplayInput = allow;
        }

        /// <summary>
        /// Adds a new input that the handler should listen for.
        /// </summary>
        /// <param name="inputName">The input name which will trigger the event.</param>
        /// <param name="eventName">The event to trigger when the button is down.</param>
        private void RegisterAbilityInput(string inputName, string eventName)
        {
            if (m_AbilityInputName == null) {
                m_AbilityInputName = new List<string>();
                m_AbilityInputEvent = new List<string>();
            }
            m_AbilityInputName.Add(inputName);
            m_AbilityInputEvent.Add(eventName);
        }

        /// <summary>
        /// Removes an input event that the handler should no longer for.
        /// </summary>
        /// <param name="inputName">The input name which will trigger the event.</param>
        /// <param name="eventName">The event to trigger when the button is down.</param>
        private void UnregisterAbilityInput(string inputName, string eventName)
        {
            // The input name and event list will always correspond to the same abilitie's input event.
            for (int i = m_AbilityInputName.Count - 1; i >= 0; --i) {
                if (inputName.Equals(m_AbilityInputName[i]) && eventName.Equals(m_AbilityInputEvent[i])) {
                    m_AbilityInputName.RemoveAt(i);
                    m_AbilityInputEvent.RemoveAt(i);
                    break;
                }
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has left the network game. Tell the camera not to follow the character anymore.
        /// </summary>
        public override void OnNetworkDestroy()
        {
            base.OnNetworkDestroy();

            if (isLocalPlayer && m_Camera != null) {
                m_Camera.GetComponent<CameraMonitor>().Character = null;
            }

            // The event will be registered again if the character joins the game again.
            EventHandler.UnregisterEvent("OnNetworkStopClient", OnNetworkDestroy);
        }
#endif
    }
}