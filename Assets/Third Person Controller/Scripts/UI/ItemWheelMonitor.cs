using UnityEngine;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The ItemMonitor keeps the Item UI up to date, including showing the current item and the amount of ammo that is remaining.
    /// </summary>
    public class ItemWheelMonitor : MonoBehaviour
    {
        [Tooltip("The mapping to the Item Wheel input")]
        [SerializeField] protected string m_ToggleItemWheel = "Toggle Item Wheel";

        // Component references
        private GameObject m_GameObject;
        private GameObject m_Character;
        private Inventory m_Inventory;
        private PlayerInput m_PlayerInput;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the component.
            enabled = false;
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            if (m_Character != null) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnItemShowScope", CanShowWheel);
            }

            m_Character = character;

            if (character == null) {
                // The object may be destroyed when Unity is ending.
                if (this != null) {
                    enabled = false;
                }
                return;
            }

            EventHandler.RegisterEvent<bool>(m_Character, "OnItemShowScope", CanShowWheel);

            m_Inventory = m_Character.GetComponent<Inventory>();
            m_PlayerInput = m_Character.GetComponent<PlayerInput>();
            enabled = true;
        }

        /// <summary>
        /// Respond to changes when the item wheel button is pressed.
        /// </summary>
        private void Update()
        {
            if (m_PlayerInput.GetButtonDown(m_ToggleItemWheel)) {
                ToggleVisiblity(true);
            } else if (m_PlayerInput.GetButtonUp(m_ToggleItemWheel)) {
                ToggleVisiblity(false);
            }
        }

        /// <summary>
        /// A wheel slice has been selected. Equip the selected item and close the wheel.
        /// </summary>
        /// <param name="primaryItemType">The selected item.</param>
        public void ItemSelected(PrimaryItemType primaryItemType)
        {
            m_Inventory.EquipItem(primaryItemType);

            ToggleVisiblity(false);
        }

        /// <summary>
        /// Show or hide the item wheel.
        /// </summary>
        /// <param name="visible">Should the wheel be visible?</param>
        private void ToggleVisiblity(bool visible)
        {
            // Let the slices and other objects know that the wheel has been shown. When the wheel is visible regular gameplay input should stop.
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnItemWheelToggleVisibility", visible);
            EventHandler.ExecuteEvent<bool>(m_Character, "OnAllowGameplayInput", !visible);
        }

        /// <summary>
        /// Can the ItemWheel be shown? It may not be able to be shown if the scope is active.
        /// </summary>
        /// <param name="disableWheel">True if the wheel should be disabled.</param>
        private void CanShowWheel(bool disableWheel)
        {
            if (this != null) {
                enabled = !disableWheel;
            }
        }

        /// <summary>
        /// Shows or hides the UI.
        /// </summary>
        /// <param name="show">Should the UI be shown?</param>
        private void ShowUI(bool show)
        {
            gameObject.SetActive(show);
        }

        /// <summary>
        /// The EventHandler was cleared. This will happen when a new scene is loaded. Unregister the registered events to prevent old events from being fired.
        /// </summary>
        private void EventHandlerClear()
        {
            EventHandler.UnregisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.UnregisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.UnregisterEvent("OnEventHandlerClear", EventHandlerClear);
        }
    }
}