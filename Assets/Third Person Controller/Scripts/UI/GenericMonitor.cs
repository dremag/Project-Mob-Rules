using UnityEngine;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// A generic UI class which will listen for the OnShowUI event. Add this component to any UI objects that should be disabled when the event is fired.
    /// </summary>
    public class GenericMonitor : MonoBehaviour
    {
        // Internal variables
        private bool m_PrevActive;

        // Component references
        private GameObject m_GameObject;

        /// <summary>
        /// Cache the component references and register for any events.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;

            EventHandler.RegisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);
        }

        /// <summary>
        /// Shows or hides the UI.
        /// </summary>
        /// <param name="show">Should the UI be shown?</param>
        private void ShowUI(bool show)
        {
            // Only show the GameObject if it was previously active.
            if (!show) {
                m_PrevActive = m_GameObject.activeSelf;
                m_GameObject.SetActive(show);
            } else if (m_PrevActive) {
                m_GameObject.SetActive(show);
            }
        }

        /// <summary>
        /// The EventHandler was cleared. This will happen when a new scene is loaded. Unregister the registered events to prevent old events from being fired.
        /// </summary>
        private void EventHandlerClear()
        {
            EventHandler.UnregisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.UnregisterEvent("OnEventHandlerClear", EventHandlerClear);
        }
    }
}