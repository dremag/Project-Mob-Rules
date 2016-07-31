using UnityEngine;
using UnityEngine.UI;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The ItemMonitor will keep the Item UI in sync with the rest of the game. This includes showing the current item and the amount of ammo that is remaining.
    /// </summary>
    public class ItemMonitor : MonoBehaviour
    {
        protected enum MonitorType { Primary, Secondary, DualWield, ItemExtension, RightHand, LeftHand }
        [Tooltip("Specifies how the UI monitors the item")]
        [SerializeField] protected MonitorType m_MonitorType;
        [Tooltip("A reference to the UI Text component for the item count")]
        [SerializeField] protected Text m_LoadedCountText;
        [Tooltip("A reference to the UI Text component for the primary item's unloaded count")]
        [SerializeField] protected Text m_UnloadedCountText;
        [Tooltip("A reference to the GameObject that should be disabled when the item is empty")]
        [SerializeField] protected GameObject m_DisableObjectOnEmpty;

        // Internal variables
        private bool m_IsPrimaryItem;

        // SharedFields
        private SharedProperty<int> m_UnloadedCount = null;
        private SharedProperty<int> m_PrimaryLoadedCount = null;
        private SharedProperty<int> m_DualWieldLoadedCount = null;
        private SharedProperty<int> m_SecondaryItemCount = null;
        private SharedProperty<int> m_FirstExtensionItemCount = null;
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;

        // Component references
        private Image m_Image;
        private RectTransform m_RectTransform;
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();

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
                if (m_MonitorType != MonitorType.Secondary) {
                    if (m_MonitorType == MonitorType.LeftHand || m_MonitorType == MonitorType.RightHand) {
                        EventHandler.UnregisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                        EventHandler.UnregisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", DualWieldItemChange);
                    } else if (m_MonitorType == MonitorType.Primary || m_MonitorType == MonitorType.ItemExtension) {
                        EventHandler.UnregisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                    } else {
                        EventHandler.UnregisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", DualWieldItemChange);
                    }
                } else {
                    EventHandler.UnregisterEvent(m_Character, "OnInventorySecondaryItemCountChange", SecondaryItemCountChange);
                }
            }

            m_Character = character;

            if (character == null) {
                // The object may be destroyed when Unity is ending.
                if (this != null) {
                    gameObject.SetActive(false);
                }
                return;
            }

            SharedManager.InitializeSharedFields(m_Character, this);

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            if (m_MonitorType != MonitorType.Secondary) {
                if (m_MonitorType == MonitorType.LeftHand || m_MonitorType == MonitorType.RightHand) {
                    EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                    EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", DualWieldItemChange);
                    PrimaryItemChange(m_CurrentPrimaryItem.Get());
                    DualWieldItemChange(m_CurrentDualWieldItem.Get());
                } else if (m_MonitorType == MonitorType.Primary || m_MonitorType == MonitorType.ItemExtension) {
                    EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                    PrimaryItemChange(m_CurrentPrimaryItem.Get());
                } else {
                    EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", DualWieldItemChange);
                    DualWieldItemChange(m_CurrentDualWieldItem.Get());
                }
                EventHandler.RegisterEvent<Item, bool, bool>(m_Character, "OnInventoryConsumableItemCountChange", ConsumableItemCountChange);

            } else {
                EventHandler.RegisterEvent(m_Character, "OnInventorySecondaryItemCountChange", SecondaryItemCountChange);

                // Initialize the secondary values.
                SecondaryItemCountChange();
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// The primary item has changed. Update the UI.
        /// </summary>
        /// <param name="item">The item that was equipped. Can be null.</param>
        private void PrimaryItemChange(Item item)
        {
            ItemChange();
        }

        /// <summary>
        /// The dual wield item has changed. Update the UI.
        /// </summary>
        /// <param name="item">The item that was equipped. Can be null.</param>
        private void DualWieldItemChange(Item item)
        {
            ItemChange();
        }

        /// <summary>
        /// The item has changed. Update the UI.
        /// </summary>
        private void ItemChange()
        {
            if (m_Image == null) {
                return;
            }

            Item item = null;
            var currentItem = m_CurrentPrimaryItem.Get();
            if (currentItem != null && ((currentItem.RightItemSprite == (m_MonitorType == MonitorType.RightHand) || m_MonitorType == MonitorType.Primary) || 
                                        (m_MonitorType == MonitorType.ItemExtension && m_FirstExtensionItemCount.Get() > -1))) {
                item = currentItem;
                m_IsPrimaryItem = true;
            } else if ((currentItem = m_CurrentDualWieldItem.Get()) != null && (currentItem.RightItemSprite == (m_MonitorType == MonitorType.RightHand) || m_MonitorType == MonitorType.DualWield)) {
                item = currentItem;
                m_IsPrimaryItem = false;
            }

            if (item == null) {
                if (m_MonitorType != MonitorType.ItemExtension) {
                    // Disable the UI if there is no item or sprite.
                    m_Image.sprite = null;
                }
                m_LoadedCountText.enabled = m_Image.enabled = false;
                if (m_UnloadedCountText != null) {
                    m_UnloadedCountText.enabled = false;
                }
                if (m_DisableObjectOnEmpty != null) {
                    m_DisableObjectOnEmpty.SetActive(false);
                }
            } else {
                if (m_MonitorType != MonitorType.ItemExtension) {
                    // There is an item so ensure the UI is enabled.
                    if (item.ItemSprite == null) {
                        m_Image.sprite = null;
                        m_Image.enabled = false;
                    } else {
                        m_Image.sprite = item.ItemSprite;
                        m_Image.enabled = true;
                    }

                    // Position the sprite in the center.
                    if (m_Image.sprite != null) {
                        var sizeDelta = m_RectTransform.sizeDelta;
                        sizeDelta.x = m_Image.sprite.textureRect.width;
                        sizeDelta.y = m_Image.sprite.textureRect.height;
                        m_RectTransform.sizeDelta = sizeDelta;
                    }
                } else {
                    m_Image.enabled = true;
                }

                m_LoadedCountText.enabled = true;
                if (m_UnloadedCountText != null) {
                    m_UnloadedCountText.enabled = true;
                }
                if (m_DisableObjectOnEmpty != null) {
                    m_DisableObjectOnEmpty.SetActive(true);
                }

                // Update the loaded and unloaded count.
                ConsumableItemCountChange(item, false, false);
            }
        }

        /// <summary>
        /// The amount of consumable ammo has changed. Update the loaded and unloaded count.
        /// </summary>
        /// <param name="item">The item whose consumable ammo has changed.</param>
        /// <param name="added">True if the consumable items were added.</param>
        /// <param name="immediateChange">True if the consumable item count should be changed immediately. This is not used by the ItemMonitor.</param>
        private void ConsumableItemCountChange(Item item, bool added, bool immediateChange)
        {
            var loadedCount = m_IsPrimaryItem ? (m_MonitorType == MonitorType.ItemExtension ? m_FirstExtensionItemCount.Get() : m_PrimaryLoadedCount.Get()) : m_DualWieldLoadedCount.Get();
            // Don't update if monitoring a hand and the hand doesn't match.
            if ((item.RightItemSprite && m_MonitorType == MonitorType.LeftHand) || (!item.RightItemSprite && m_MonitorType == MonitorType.RightHand)) {
                if (m_UnloadedCountText != null) {
                    if (loadedCount != int.MaxValue && loadedCount != -1) {
                        m_UnloadedCountText.text = m_UnloadedCount.Get().ToString();
                    } else {
                        m_UnloadedCountText.text = string.Empty;
                    }
                }
                return;
            }

            if (loadedCount != int.MaxValue && loadedCount != -1) {
                if (m_MonitorType == MonitorType.ItemExtension) {
                    m_LoadedCountText.text = loadedCount.ToString();
                } else {
                    // If ItemUnloadedCountText is null then the ItemCount should show the loaded and unloaded count.
                    if (m_UnloadedCountText == null) {
                        m_LoadedCountText.text = (loadedCount + m_UnloadedCount.Get()).ToString();
                    } else {
                        m_LoadedCountText.text = loadedCount.ToString();
                        m_UnloadedCountText.text = m_UnloadedCount.Get().ToString();
                    }
                }
            } else {
                // If the amount is unlimited then show an empty string.
                m_LoadedCountText.text = string.Empty;
                if (m_UnloadedCountText != null) {
                    m_UnloadedCountText.text = string.Empty;
                }
            }
        }

        /// <summary>
        /// The amount of secondary ammo has changed. Update the count.
        /// </summary>
        private void SecondaryItemCountChange()
        {
            var itemCount = m_SecondaryItemCount.Get();
            if (itemCount != int.MaxValue && itemCount != -1) {
                m_LoadedCountText.text = itemCount.ToString();
            } else {
                m_LoadedCountText.text = string.Empty;
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