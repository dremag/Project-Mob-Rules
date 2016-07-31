using UnityEngine;
using UnityEngine.UI;
using System;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The HealthMonitor will keep the Health UI in sync with the rest of the game. This includes keeping the slider/images up to date when the the health or shield values change.
    /// </summary>
    public class HealthMonitor : MonoBehaviour
    {
        [Tooltip("Should the health be monitored? If false the shield will be monitored")]
        [SerializeField] protected bool m_MonitorHealth = true;
        [Tooltip("Optionally specify a set of images used to represent the health")]
        [SerializeField] protected Image[] m_Images;
        [Tooltip("Optionally specify a image to indicate that the current health image is half full")]
        [SerializeField] protected Sprite m_HalfImageSprite;

        // SharedFields
        private SharedProperty<float> m_MaxHealth = null;
        private SharedProperty<float> m_MaxShield = null;
        private SharedProperty<float> m_CurrentHealth = null;
        private SharedProperty<float> m_CurrentShield = null;

        // Component references
        private Slider m_Slider;
        private Sprite m_FullImageSprite;
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_Slider = GetComponent<Slider>();
            // If the slider is null then the discrete images will be used.
            if (m_Slider == null) {
                m_FullImageSprite = m_Images[0].sprite;
                for (int i = 0; i < m_Images.Length; ++i) {
                    m_Images[i].enabled = true;
                }
            }

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
                if (m_MonitorHealth) {
                    EventHandler.RegisterEvent<float>(m_Character, "OnHealthAmountChange", HealthAmountChange);
                } else {
                    EventHandler.RegisterEvent<float>(m_Character, "OnHealthShieldAmountChange", HealthAmountChange);
                }
            }

            m_Character = character;

            if (character == null) {
                // The object may be destroyed when Unity is ending.
                if (this != null) {
                    gameObject.SetActive(false);
                    enabled = false;
                }
                return;
            }

            SharedManager.InitializeSharedFields(character, this);

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            if (m_MonitorHealth) {
                EventHandler.RegisterEvent<float>(character, "OnHealthAmountChange", HealthAmountChange);
                HealthAmountChange(m_CurrentHealth.Get());
                if (m_Slider != null) {
                    m_Slider.maxValue = m_MaxHealth.Get();
                }
            } else {
                EventHandler.RegisterEvent<float>(character, "OnHealthShieldAmountChange", HealthAmountChange);
                HealthAmountChange(m_CurrentShield.Get());
                if (m_Slider != null) {
                    m_Slider.maxValue = m_MaxShield.Get();
                }
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// The amount of health or shield has changed. Update the slider.
        /// </summary>
        /// <param name="amount">The new amount.</param>
        private void HealthAmountChange(float amount)
        {
            if (m_Slider != null) {
                m_Slider.value = amount;
            } else { // Discrete images.
                // Determine the image count based on the amount of the current health/shield.
                var imageCount = (amount / (m_MonitorHealth ? m_MaxHealth.Get() : m_MaxShield.Get())) * m_Images.Length;
                // If a half image sprite is specified then allow half images.
                if (m_HalfImageSprite != null) {
                    imageCount = (float)Math.Round((imageCount * 2), MidpointRounding.AwayFromZero) / 2f;
                } else {
                    imageCount = Mathf.Floor(imageCount);
                }
                // Prevent the image from not displaying at all if there is some health left.
                if (imageCount == 0) {
                    if (m_HalfImageSprite != null) {
                        imageCount = 0.5f;
                    } else {
                        imageCount = 1;
                    }
                }
                // Show all of the images.
                for (int i = 0; i < m_Images.Length; ++i) {
                    if (i < imageCount) {
                        // The image should be enabled. Show a half image if the remainder is 0.5f.
                        if (imageCount - i == 0.5f) {
                            m_Images[i].sprite = m_HalfImageSprite;
                        } else {
                            m_Images[i].sprite = m_FullImageSprite;
                        }
                        m_Images[i].enabled = true;
                    } else {
                        m_Images[i].enabled = false;
                    }
                }
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