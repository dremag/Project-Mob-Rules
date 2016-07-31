using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The SniperScopeMonitor will monitor the visiblity of the scope UI.
    /// </summary>
    public class SniperScopeMonitor : MonoBehaviour
    {
        // Component references
        private GameObject m_GameObject;
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the GameObject.
            ShowScope(false);
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            // The object may be destroyed when Unity is ending.
            if (this != null) {
                ShowScope(false);
            }

            if (m_Character != null) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnItemShowScope", ShowScope);
            }

            m_Character = character;

            if (character == null) {
                return;
            }

            EventHandler.RegisterEvent<bool>(character, "OnItemShowScope", ShowScope);
        }

        /// <summary>
        /// Shows or hides the scope.
        /// </summary>
        /// <param name="show">Should the scope be shown?</param>
        private void ShowScope(bool show)
        {
            m_GameObject.SetActive(show);
        }

        /// <summary>
        /// The EventHandler was cleared. This will happen when a new scene is loaded. Unregister the registered events to prevent old events from being fired.
        /// </summary>
        private void EventHandlerClear()
        {
            EventHandler.UnregisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.UnregisterEvent("OnEventHandlerClear", EventHandlerClear);
        }
    }
}