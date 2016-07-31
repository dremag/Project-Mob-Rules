using UnityEngine;
using UnityEngine.UI;
using Opsive.ThirdPersonController.Abilities;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The AbilityIndicatorMonitor will show and hide the ability indicator UI sprite.
    /// </summary>
    public class AbilityIndicatorMonitor : MonoBehaviour
    {
        // Component references
        private RectTransform m_ImageRectTransform;
        private Image m_Image;
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_ImageRectTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();
            m_Image.enabled = false;

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the GameObject.
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            if (m_Character != null) {
                EventHandler.UnregisterEvent<Sprite>(m_Character, "OnControllerAbilityChange", AbilityChange);
                EventHandler.UnregisterEvent<Sprite>(m_Character, "OnControllerAbilityChange", AbilityChange);
                EventHandler.UnregisterEvent(m_Character, "OnDeath", OnDeath);
            }

            m_Character = character;

            if (character == null) {
                // The object may be destroyed when Unity is ending.
                if (this != null) {
                    gameObject.SetActive(false);
                }
                return;
            }

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            EventHandler.RegisterEvent<Sprite>(m_Character, "OnControllerAbilityChange", AbilityChange);
            EventHandler.RegisterEvent(m_Character, "OnDeath", OnDeath);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// The ability status has changed. Show the sprite if an ability can be activated, otherwise hide the sprite.
        /// </summary>
        /// <param name="abilitySprite">The sprite of the active ability. Can be null.</param>
        private void AbilityChange(Sprite abilitySprite)
        {
            // The object may be destroyed when the event is triggered.
            if (this == null) {
                return;
            }

            m_Image.enabled = abilitySprite != null;
            m_Image.sprite = abilitySprite;

            // Size the RectTransform according to the Sprite size.
            if (abilitySprite != null) {
                var sizeDelta = m_ImageRectTransform.sizeDelta;
                sizeDelta.x = abilitySprite.textureRect.width;
                sizeDelta.y = abilitySprite.textureRect.height;
                m_ImageRectTransform.sizeDelta = sizeDelta;
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
        /// The character has died. Hide the indicator.
        /// </summary>
        private void OnDeath()
        {
            AbilityChange(null);
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