using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the inventory. 
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class InventoryHandler : NetworkBehaviour
#else
    public class InventoryHandler : MonoBehaviour
#endif
    {
        [Tooltip("Can the items be switched through a button map?")]
        [SerializeField] protected bool m_CanSwitchItems = true;
        [Tooltip("Can the item be toggled between equipped or unequipped through a button map?")]
        [SerializeField] protected bool m_CanToggleEquippedItem = true;
        [Tooltip("Can the input scroll through the items?")]
        [SerializeField] protected bool m_CanScrollItems;
        [Tooltip("If Can Scroll Items is enabled, the sensitivity for scrolling between items")]
        [SerializeField] protected float m_ScrollSensitivity;
        [Tooltip("Can items be equipped via a specified button map?")]
        [SerializeField] protected bool m_CanEquipSpecifiedItems = true;

        // SharedFields
        private SharedMethod<bool> m_CanInteractItem = null;

        // Component references
        private GameObject m_GameObject;
        private Inventory m_Inventory;
        private PlayerInput m_PlayerInput;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Inventory = GetComponent<Inventory>();
            m_PlayerInput = GetComponent<PlayerInput>();
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowInventoryInput", AllowGameplayInput);

            // An AI Agent does not use PlayerInput so Update does not need to run.
            if (GetComponent<PlayerInput>() == null) {
                enabled = false;
            }
        }

        /// <summary>
        /// Notify the inventory that the user wants to perform an action.
        /// </summary>
        private void Update()
        {

#if ENABLE_MULTIPLAYER
            if (!isLocalPlayer) {
                return;
            }
#endif

            if (m_CanSwitchItems) {
                if (m_PlayerInput.GetButtonDown(Constants.NextItemInputName)) {
#if ENABLE_MULTIPLAYER
                    CmdTrySwitchItem(true, false);
#else
                    TrySwitchItem(true, false);
#endif
                }

                if (m_PlayerInput.GetButtonDown(Constants.PrevItemInputName)) {
#if ENABLE_MULTIPLAYER
                    CmdTrySwitchItem(true, true);
#else
                    TrySwitchItem(true, true);
#endif
                }
            }

            if (m_CanToggleEquippedItem && m_PlayerInput.GetButtonDown(Constants.EquipItemToggleInputName) && m_CanInteractItem.Invoke()) {
#if ENABLE_MULTIPLAYER
                CmdTryToggleEquippedItem();
#else
                m_Inventory.ToggleEquippedItem();
#endif
            }

            if (m_CanScrollItems) {
                float scrollInput;
                if (Mathf.Abs(scrollInput = m_PlayerInput.GetAxis(Constants.ItemScrollName)) > m_ScrollSensitivity && !m_Inventory.IsSwitchingItems) {
#if ENABLE_MULTIPLAYER
                    CmdTrySwitchItem(true, scrollInput > 0);
#else
                    TrySwitchItem(true, scrollInput > 0);
#endif
                }
            }

            if (m_CanEquipSpecifiedItems) {
                for (int i = 0; i < Constants.EquipSpecifiedItem.Length; ++i) {
                    if (m_PlayerInput.GetButtonDown(Constants.EquipSpecifiedItem[i])) {
#if ENABLE_MULTIPLAYER
                        CmdTryEquipItem(i);
#else
                        TryEquipItem(i);
#endif
                    }
                }
            }

            Item dualWieldItem;
            if ((dualWieldItem = m_Inventory.GetCurrentItem(typeof(DualWieldItemType))) != null && m_PlayerInput.GetButtonDown(Constants.DropDualWieldItemInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryDropItem(dualWieldItem.ItemType.ID);
#else
                TryDropItem(dualWieldItem.ItemType.ID);
#endif
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Tries to switch the item to the next item in the inventory list on the server.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        [Command]
        private void CmdTrySwitchItem(bool primaryItem, bool next)
        {
            TrySwitchItem(primaryItem, next);
        }

        /// <summary>
        /// If an item is equipped then unequip it on the server. If an item is unequipped or equal to the unequipped type then equip the previous item.
        /// </summary>
        [Command]
        private void CmdTryToggleEquippedItem()
        {
            TryToggleEquippedItem();
        }

        /// <summary>
        /// Tires to equip the primary item in the specified index on the server.
        /// </summary>
        /// <param name="itemIndex">The inventory index to equip</param>
        [Command]
        private void CmdTryEquipItem(int itemIndex)
        {
            TryEquipItem(itemIndex);
        }
        
        /// <summary>
        /// Tries to drop the item on the server.
        /// </summary>
        /// <param name="itemID">The item id to drop.</param>
        [Command]
        private void CmdTryDropItem(int itemID)
        {
            TryDropItem(itemID);
        }
#endif

        /// <summary>
        /// Tries to switch the item to the next item in the inventory list.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        public void TrySwitchItem(bool primaryItem, bool next)
        {
            // Return early if the item cannot be interacted with.
            if (!m_CanInteractItem.Invoke()) {
                return;
            }

            m_Inventory.SwitchItem(primaryItem, next);
        }

        /// <summary>
        /// If an item is equipped then unequip it. If an item is unequipped or equal to the unequipped type then equip the previous item.
        /// </summary>
        public void TryToggleEquippedItem()
        {
            // Return early if the item cannot be interacted with.
            if (!m_CanInteractItem.Invoke()) {
                return;
            }

            m_Inventory.ToggleEquippedItem();
        }

        /// <summary>
        /// Equips the primary item in the specified index on the server.
        /// </summary>
        /// <param name="itemIndex">The inventory index to equip</param>
        public void TryEquipItem(int itemIndex)
        {
            // Return early if the item cannot be interacted with.
            if (!m_CanInteractItem.Invoke()) {
                return;
            }

            m_Inventory.EquipItem(itemIndex);
        }

        /// <summary>
        /// Tries to drop the item.
        /// </summary>
        /// <param name="itemID">The item id to drop.</param>
        public void TryDropItem(int itemID)
        {
            // Return early if the item cannot be interacted with.
            if (!m_CanInteractItem.Invoke()) {
                return;
            }

            m_Inventory.DropItem(itemID);
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            enabled = allow;
        }
    }
}