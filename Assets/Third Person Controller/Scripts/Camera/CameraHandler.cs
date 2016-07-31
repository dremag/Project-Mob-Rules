using UnityEngine;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the camera. 
    /// </summary>
    public class CameraHandler : MonoBehaviour
    {
        [Tooltip("The camera zoom state. Set to null if the camera should not be able to zoom")]
        [SerializeField] protected string m_ZoomStateName = "Zoom";

        // Internal variables
        private CameraState m_ZoomState;
        private float m_Yaw;
        private float m_Pitch;
        private float m_StepZoom;
        private bool m_RotateBehindCharacter;
        private bool m_IsZoomed;
        private bool m_CanZoom;

        // Exposed properties
        public GameObject Character { set { InitializeCharacter(value); } }
        public float Yaw { get { return m_Yaw; } }
        public float Pitch { get { return m_Pitch; } }
        public float StepZoom { get { return m_StepZoom; } }
        public bool RotateBehindCharacter { get { return m_RotateBehindCharacter; } }
        public string ZoomStateName { set { m_ZoomStateName = value; m_ZoomState = !string.IsNullOrEmpty(m_ZoomStateName) ? m_CameraController.StateWithName(m_ZoomStateName) : null; } }

        // Component references
        private GameObject m_Character;
        private CameraController m_CameraController;
        private PlayerInput m_PlayerInput;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_CameraController = GetComponent<CameraController>();
        }

        /// <summary>
        /// Initializes the state values.
        /// </summary>
        private void Start()
        {
            if (!string.IsNullOrEmpty(m_ZoomStateName)) {
                m_ZoomState = m_CameraController.StateWithName(m_ZoomStateName);
                m_CanZoom = true;
            }
        }

        /// <summary>
        /// Initialize the handler to the specified character.
        /// </summary>
        /// <param name="character">The character to initialize. Can be null.</param>
        private void InitializeCharacter(GameObject character)
        {
            if (character == null) {
                if (m_Character != null) {
                    EventHandler.UnregisterEvent<bool>(m_Character, "OnAllowGameplayInput", AllowGameplayInput);
                    EventHandler.UnregisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", OnDualWieldItemChange);
                    m_Character = null;
                    m_PlayerInput = null;
                }
                return;
            }

            m_Character = character;
            m_PlayerInput = character.GetComponent<PlayerInput>();
            EventHandler.RegisterEvent<bool>(m_Character, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", OnDualWieldItemChange);
        }

        /// <summary>
        /// Sets the input storage variables to allow the camera to move according to the user input.
        /// </summary>
        private void Update()
        {
            var zoom = m_CanZoom && m_ZoomState != null && m_PlayerInput.GetButton(Constants.ZoomInputName, true);
            if (zoom != m_IsZoomed) {
                m_CameraController.ChangeState(m_ZoomState, zoom);
                m_IsZoomed = zoom;
            }

            if (m_CameraController.ActiveState.ViewMode != CameraMonitor.CameraViewMode.RPG) {
                m_Pitch += m_PlayerInput.GetAxisRaw(Constants.PitchInputName);
                m_Yaw += m_PlayerInput.GetAxisRaw(Constants.YawInputName);
            } else {
                if (m_PlayerInput.GetButton(Constants.PrimaryDisableButtonName, true) || m_PlayerInput.GetButton(Constants.SecondaryDisableButtonName, true)) {
                    m_Pitch += m_PlayerInput.GetAxisRaw(Constants.PitchInputName);
                    m_Yaw += m_PlayerInput.GetAxisRaw(Constants.YawInputName);
                    m_RotateBehindCharacter = !m_PlayerInput.GetButton(Constants.PrimaryDisableButtonName, true);
                } else {
                    m_Pitch = 0;
                    m_Yaw += m_PlayerInput.GetAxisRaw(Constants.SecondaryYawInputName);
                    m_RotateBehindCharacter = false;
                }
            }
            m_StepZoom = m_CameraController.ActiveState.StepZoomSensitivity > 0 ? m_PlayerInput.GetAxisRaw(Constants.StepZoomName) : 0;
        }

        /// <summary>
        /// The Camera Controller has used the input - reset the pitch and yaw.
        /// </summary>
        public void InputConsumed()
        {
            m_Pitch = m_Yaw = 0;
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            enabled = allow;

            // Stop the camera from moving if gameplay input is disallowed.
            if (!allow) {
                m_Yaw = 0;
                m_Pitch = 0;
            }
        }

        /// <summary>
        /// The inventory has added or removed a dual wielded item. Prevent the camera from zooming if the item was added.
        /// </summary>
        /// <param name="prevent">The dual wielded item added. Can be null.</param>
        private void OnDualWieldItemChange(Item item)
        {
            m_CanZoom = item == null;
        }
    }
}