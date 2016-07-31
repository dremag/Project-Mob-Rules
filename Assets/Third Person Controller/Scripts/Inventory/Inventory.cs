using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The Inventory manager all of the Items. It allows items to be used, reloaded, dropped, etc. It also communicates with the Animator to trigger animations when switching between
    /// items.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class Inventory : NetworkBehaviour
#else
    public class Inventory : MonoBehaviour
#endif
    {
        /// <summary>
        /// Specifies the amount of each ItemType that the character can pickup or is loaded with the default inventory.
        /// </summary>
        [System.Serializable]
        public class ItemAmount
        {
            [Tooltip("The type of item")]
            [SerializeField] protected ItemType m_ItemType;
            [Tooltip("The number of ItemType units to pickup")]
            [SerializeField] protected int m_Amount = 1;
            [Tooltip("Should the item be equipped when picked up?")]
            [SerializeField] protected bool m_Equip = true;

            // Exposed properties
            public ItemType ItemType { get { return m_ItemType; } }
            public int Amount { get { return m_Amount; } }
            public bool Equip { get { return m_Equip; } }

            /// <summary>
            /// ItemAmount constructor with no parameters.
            /// </summary>
            public ItemAmount() { }

            /// <summary>
            /// ItemAmount constructor with two parameters.
            /// </summary>
            public ItemAmount(ItemType itemType, int amount)
            {
                Initialize(itemType, amount);
            }

            /// <summary>
            /// Initializes the ItemAmount to the specified values.
            /// </summary>
            /// <param name="itemType">The ItemType to set.</param>
            /// <param name="amount">The amount of ItemType.</param>
            public void Initialize(ItemType itemType, int amount)
            {
                m_ItemType = itemType;
                m_Amount = amount;
            }
        }

        /// <summary>
        /// An ItemInstance is the actual object that is added to the Inventory. The ItemInstance can represent a PrimaryItemType or a SecondaryItemType. The ItemInstance keeps
        /// track of the variables for that particular item, such as the item count or a reference to the GameObject for the Item.
        /// </summary>
        private class ItemInstance
        {
            // Exposed properties
            public ItemType ItemType { get { return m_ItemType; } }
            public GameObject GameObject { get { return m_GameObject; } }
            public Item Item { get { return m_Item; } }
            public int ItemCount { 
                get
                { 
                    return m_ItemCount;
                } 
                set 
                {
                    m_ItemCount = Mathf.Max(Mathf.Min(value, m_ItemType.GetCapacity()), 0); 
                } 
            }
            public ConsumableItemInstance ConsumableItem { get { return m_ConsumableItem; } set { m_ConsumableItem = value; } }

            // Internal variables
            private ItemType m_ItemType;
            private GameObject m_GameObject;
            private Item m_Item;
            private int m_ItemCount;
            private ConsumableItemInstance m_ConsumableItem;

            /// <summary>
            /// Activates or deactivates the Item.
            /// </summary>
            /// <param name="active">True if the Item should activate.</param>
            public void SetActive(bool active)
            {
                m_Item.SetActive(active, m_ItemCount);
            }

            /// <summary>
            /// Constructor for the ItemInstance. Will set the internal variables.
            /// </summary>
            /// <param name="item">A reference to the Item that is being added.</param>
            public ItemInstance(Item item)
            {
                m_Item = item;
                m_ItemType = item.ItemType;
                m_GameObject = item.gameObject;
                if (m_ItemType is PrimaryItemType || m_ItemType is DualWieldItemType) {
                    // On the network Awake isn't always immediately called so do it manually.
                    m_Item.Awake();
                    SetActive(false);
                }
            }
        }

        /// <summary>
        /// A ConsumableItemInstance is the actual object that is added to the Inventory. The ConsumableItemInstance keeps track of the variables for that particular
        /// item, such as the number of items that are loaded in a primary item or the number of items that are unloaded.
        /// </summary>
        private class ConsumableItemInstance
        {
            // Exposed properties
            public ItemType ConsumableItemType { get { return m_ConsumableItemType; } }
            public List<ItemType> ParentItemType { get { return m_ParentItemType; } }
            public int Capacity { get { return m_Capacity; } }
            public int UnloadedCount
            {
                get
                {
                    return m_UnloadedCount;
                }
                set
                {
                    // Take the min of the value and capacity minus the loaded counts so the unloaded plus loaded is never more than the max capacity.
                    // Take the max of that value and zero to prevent the count from going below zero.
                    m_UnloadedCount = Mathf.Max(Mathf.Min(value, m_Capacity - m_TotalPrimaryItemCount), 0);
                }
            }

            // Internal variables
            private int m_UnloadedCount;
            private Dictionary<ItemType, int> m_PrimaryLoadedCount = new Dictionary<ItemType, int>();
            private ItemType m_ConsumableItemType;
            private List<ItemType> m_ParentItemType = new List<ItemType>();
            private int m_Capacity;
            private int m_TotalPrimaryItemCount;

            /// <summary>
            /// Constructor for ConsumableItemInstance. Will set the internal variables.
            /// </summary>
            /// <param name="itemType">The ItemType that the ConsumableItemInstance represents.</param>
            /// <param name="capacity">The maximum number of consumable items the primary item can hold.</param>
            /// <param name="capacity">The parent of the consumable ItemType.</param>
            public ConsumableItemInstance(ItemType itemType, int capacity, ItemType parentItemType)
            {
                m_Capacity = capacity;
                m_ConsumableItemType = itemType;
                m_ParentItemType.Add(parentItemType);
                m_PrimaryLoadedCount.Add(parentItemType, 0);
            }

            /// <summary>
            /// Adds a new ItemType to the PrimaryLoadedCount dictionary.
            /// </summary>
            /// <param name="capacity">The parent of the consumable ItemType.</param>
            public void AddItemType(ItemType parentItemType)
            {
                m_ParentItemType.Add(parentItemType);
#if UNITY_EDITOR
                if (m_PrimaryLoadedCount.ContainsKey(parentItemType)) {
                    Debug.LogError("Error: Unable to add " + parentItemType + " to the Inventory because the ItemType has already been added.");
                    return;
                }
#endif
                m_PrimaryLoadedCount.Add(parentItemType, 0);
            }

            /// <summary>
            /// Sets a new PrimaryItemCount value.
            /// </summary>
            /// <param name="itemType">The item type to change.</param>
            /// <param name="amount">The value to change.</param>
            /// <param name="assign">Should the value be assigned (true) or added (false)?</param>
            public void SetPrimaryItemCount(ItemType itemType, int amount, bool assign)
            {
                int count;
                if (!m_PrimaryLoadedCount.TryGetValue(itemType, out count)) {
                    // If the ItemType isn't found then it could be a PrimaryItem or ConsumableItem extension. In this case return the first instance - it allows for easy reference when the
                    // parent item type isn't immediately known.
                    count = m_PrimaryLoadedCount[m_ParentItemType[0]];
                }

                var startCount = count;
                if (assign) {
                    count = amount;
                } else { // Add the amount.
                    count += amount;
                }
                count = Mathf.Max(count, 0);
                // Keep a running count of the primary item count across all items for quick access.
                m_TotalPrimaryItemCount += (count - startCount);
                m_PrimaryLoadedCount[itemType] = count;
            }

            /// <summary>
            /// Returns the PrimaryItemCount based on the ItemType.
            /// </summary>
            /// <param name="itemType">The ItemType to retrieve the count of.</param>
            /// <returns>The PrimaryItemCount based on the ItemType.</returns>
            public int GetPrimaryItemCount(ItemType itemType)
            {
                int count;
                if (!m_PrimaryLoadedCount.TryGetValue(itemType, out count)) {
                    // If the ItemType isn't found then it could be a PrimaryItem or ConsumableItem extension. In this case return the first instance - it allows for easy reference when the
                    // parent item type isn't immediately known.
                    count = m_PrimaryLoadedCount[m_ParentItemType[0]];
                }

                return count;
            }

            /// <summary>
            /// Resets all of the PrimaryLoadedCounts back to 0.
            /// </summary>
            public void ResetPrimaryLoadedCount()
            {
                for (int i = 0; i < m_ParentItemType.Count; ++i) {
                    m_PrimaryLoadedCount[m_ParentItemType[i]] = 0;
                }
                m_TotalPrimaryItemCount = 0;
            }
        }

        [Tooltip("Items to load when the Inventory is initially created or on a character respawn")]
        [SerializeField] protected ItemAmount[] m_DefaultLoadout;
        [Tooltip("The order to load the primary items within the inventory")]
        [SerializeField] protected ItemType[] m_ItemOrder;
        [Tooltip("an the character use unlimited items?")]
        [SerializeField] protected bool m_UnlimitedAmmo;
        [Tooltip("The item that should be used when unequipped. This can be used for the character's fist so they can punch when unarmed")]
        [SerializeField] protected ItemType m_UnequippedItemType;
        [Tooltip("Can the unequipped item type be switched to?")]
        [SerializeField] protected bool m_CanSwitchToUnequippedItemType;
        [Tooltip("When the character dies or drops a dual wielded item, should they drop the item?")]
        [SerializeField] protected bool m_DropItems;
        [Tooltip("The Transform to parent the dropped items to")]
        [SerializeField] protected Transform m_DroppedItemsParent;

        // Internal variables
        private List<ItemInstance> m_PrimaryInventory = new List<ItemInstance>();
        private List<ConsumableItemInstance> m_ConsumableInventory = new List<ConsumableItemInstance>();
        private List<ItemInstance> m_SecondaryInventory = new List<ItemInstance>();
        private List<ItemInstance> m_DualWieldInventory = new List<ItemInstance>();
        private Dictionary<int, ItemType> m_IDItemTypeMap = new Dictionary<int, ItemType>();
        private Dictionary<ItemType, int> m_ItemIndexMap = new Dictionary<ItemType, int>();
        private Dictionary<ItemType, ItemType> m_DualWieldPrimaryItemMap = new Dictionary<ItemType, ItemType>();
        private Dictionary<ItemType, ItemType> m_PrimaryDualWieldItemMap = new Dictionary<ItemType, ItemType>();
        private Dictionary<ItemType, int> m_PrimaryDualWieldItemIndexMap = new Dictionary<ItemType, int>();

        private int m_CurrentPrimaryIndex = -1;
        private int m_CurrentSecondaryIndex = -1;
        private int m_CurrentDualWieldIndex = -1;
        private int m_ToggleEquippedIndex = -1;
        private int m_ToggleDualWieldEquippedIndex = -1;
        private int m_EquipIndex = -1;
        private int m_UnequipIndex = -1;
        private int m_DualWieldEquipIndex = -1;
        private int m_DualWieldUnequipIndex = -1;
        private int m_DualWieldSwitchIndex = -1;
        private bool m_InventoryLoaded = false;
#if ENABLE_MULTIPLAYER
        private int m_StartItemID = -1;
        private int m_StartDualWieldItemID = -1;
#endif

        // SharedFields
        private SharedMethod<bool> m_CanInteractItem = null;
        private bool SharedProperty_ItemEquipped { get { return m_CurrentPrimaryIndex != -1 && m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType != m_UnequippedItemType; } }
        private Item SharedProperty_CurrentPrimaryItem { get { return GetCurrentItem(typeof(PrimaryItemType)); } }
        private Item SharedProperty_CurrentSecondaryItem { get { return GetCurrentItem(typeof(SecondaryItemType)); } }
        private Item SharedProperty_CurrentDualWieldItem { get { return GetCurrentItem(typeof(DualWieldItemType)); } }
        private int SharedProperty_UnloadedCount { get { return GetCurrentItemCount(typeof(PrimaryItemType), false); } }
        private int SharedProperty_PrimaryLoadedCount { get { return GetCurrentItemCount(typeof(PrimaryItemType), true); } }
        private int SharedProperty_DualWieldLoadedCount { get { return GetCurrentItemCount(typeof(DualWieldItemType), true); } }
        private int SharedProperty_SecondaryItemCount { get { return GetCurrentItemCount(typeof(SecondaryItemType), false); } }
        private int SharedProperty_FirstExtensionItemCount { get { return GetCurrentItemCount(typeof(PrimaryItemType), true, 0); } }

        // Exposed properties
        public ItemAmount[] DefaultLoadout { get { return m_DefaultLoadout; } set { m_DefaultLoadout = value; } }
        private int CurrentPrimaryIndex
        {
            set
            {
                m_CurrentPrimaryIndex = value;
                EventHandler.ExecuteEvent<Item>(m_GameObject, "OnInventoryPrimaryItemChange", GetCurrentItem(typeof(PrimaryItemType)));
            }
        }
        private int CurrentDualWieldIndex
        {
            set
            {
                m_CurrentDualWieldIndex = value;
                EventHandler.ExecuteEvent<Item>(m_GameObject, "OnInventoryDualWieldItemChange", GetCurrentItem(typeof(DualWieldItemType)));
            }
        }
        public bool IsSwitchingItems
        {
            get
            {
                if (m_UnequippedItemType != null) {
                    var unequipItemIndex = m_ItemIndexMap[m_UnequippedItemType];
                    if (m_EquipIndex == unequipItemIndex && m_UnequipIndex == -1) {
                        return false;
                    }
                }
                return m_EquipIndex != -1 || m_UnequipIndex != -1 || m_DualWieldEquipIndex != -1 || m_DualWieldUnequipIndex != -1;
            }
        }
        public ItemType UnequippedItemType { get { return m_UnequippedItemType; } }

        // Component references
        private GameObject m_GameObject;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            EquipUnequipItem(false, -1, true);

            SharedManager.Register(this);
        }

        /// <summary>
        /// Register for any events that the inventory should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipped", OnItemEquipped);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipped", OnItemUnequipped);
            EventHandler.RegisterEvent(m_GameObject, "OnAbilityToggleEquippedItem", ToggleEquippedItem);
#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
#endif
        }

        /// <summary>
        /// Unregister for any events that the inventory was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipped", OnItemEquipped);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipped", OnItemUnequipped);
            EventHandler.UnregisterEvent(m_GameObject, "OnAbilityToggleEquippedItem", ToggleEquippedItem);
#if ENABLE_MULTIPLAYER
            EventHandler.UnregisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
#endif
            RemoveAllItems(false);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Register for the inventory callbacks so the inventory can be synchronized with the current game state.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer) {
                // Initialize the inventory immediately so it can act on the received messages.
                Initialize();
            }

            NetworkClient.allClients[0].RegisterHandler(NetworkEventManager.NetworkMessages.MSG_ACTIVE_ITEM, NetworkSetActiveItem);
            NetworkClient.allClients[0].RegisterHandler(NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, NetworkPickupItem);
        }

        /// <summary>
        /// Message which indicates which primary item is currently active.
        /// </summary>
        private class ActiveItemMessage : MessageBase
        {
            // Internal variables
            private int m_ItemID;
            private int m_DualWieldItemID;

            // Exposed properties
            public int ItemID { get { return m_ItemID; } set { m_ItemID = value; } }
            public int DualWieldItemID { get { return m_DualWieldItemID; } set { m_DualWieldItemID = value; } }

            /// <summary>
            /// Populate a message object from a NetworkReader stream.
            /// </summary>
            /// <param name="reader">Stream to read from.</param>
            public override void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);

                m_ItemID = reader.ReadInt32();
                m_DualWieldItemID = reader.ReadInt32();
            }

            /// <summary>
            /// Populate a NetworkWriter stream from a message object.
            /// </summary>
            /// <param name="writer">Stream to write to.</param>
            public override void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);

                writer.Write(m_ItemID);
                writer.Write(m_DualWieldItemID);
            }
        }

        /// <summary>
        /// Message which indicates which item the character currently has.
        /// </summary>
        private class PickupItemMessage : MessageBase
        {
            // Internal variables
            private int m_ItemID;
            private int m_ItemCount;
            private int m_LoadedCount;
            private int m_UnloadedCount;

            // Exposed properties
            public int ItemID { get { return m_ItemID; } set { m_ItemID = value; } }
            public int ItemCount { get { return m_ItemCount; } set { m_ItemCount = value; } }
            public int LoadedCount { get { return m_LoadedCount; } set { m_LoadedCount = value; } }
            public int UnloadedCount { get { return m_UnloadedCount; } set { m_UnloadedCount = value; } }

            /// <summary>
            /// Populate a message object from a NetworkReader stream.
            /// </summary>
            /// <param name="reader">Stream to read from.</param>
            public override void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);

                m_ItemID = reader.ReadInt32();
                m_ItemCount = reader.ReadInt32();
                m_LoadedCount = reader.ReadInt32();
                m_UnloadedCount = reader.ReadInt32();
            }

            /// <summary>
            /// Populate a NetworkWriter stream from a message object.
            /// </summary>
            /// <param name="writer">Stream to write to</param>
            public override void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);

                writer.Write(m_ItemID);
                writer.Write(m_ItemCount);
                writer.Write(m_LoadedCount);
                writer.Write(m_UnloadedCount);
            }
        }

        /// <summary>
        /// A new client has just joined the server. Send that client the active primary item as well as all of the items that the character is carrying.
        /// </summary>
        /// <param name="netConn">The client connection.</param>
        private void OnServerReady(NetworkConnection netConn)
        {
            var activeItemMessage = ObjectPool.Get<ActiveItemMessage>();
            if (m_CurrentPrimaryIndex != -1) {
                var item = GetCurrentItem(typeof(PrimaryItemType));
                activeItemMessage.ItemID = item.ItemType.ID;
            } else {
                // Send the client a -1 to indicate that it should have all of the items unequipped.
                activeItemMessage.ItemID = -1;
            }
            if (m_CurrentDualWieldIndex != -1) {
                var item = GetCurrentItem(typeof(DualWieldItemType));
                activeItemMessage.DualWieldItemID = item.ItemType.ID;
            } else {
                // Sent the client a -1 to indicate that there is no dual wield item.
                activeItemMessage.DualWieldItemID = -1;
            }
            NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_ACTIVE_ITEM, activeItemMessage);
            ObjectPool.Return(activeItemMessage);

            var pickupItemMessage = ObjectPool.Get<PickupItemMessage>();
            // Send the new client all of the primary items and the number of consumable items that the character is carrying.
            for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                var item = m_PrimaryInventory[i].Item;
                // No reason to send the item if the character doesn't have the item.
                if (m_PrimaryInventory[i].ItemCount > 0) {
                    pickupItemMessage.ItemID = item.ItemType.ID;
                    pickupItemMessage.ItemCount = m_PrimaryInventory[i].ItemCount;
                    pickupItemMessage.LoadedCount = GetItemCount(item.ItemType, true);
                    pickupItemMessage.UnloadedCount = GetItemCount(item.ItemType, false);
                    NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, pickupItemMessage);

                    // Send any additional ItemExtension ConsumableItemTypes.
                    for (int j = 0; j < item.ItemExtensions.Length; ++j) {
                        var extensionItemType = item.ItemExtensions[j].ConsumableItemType;
                        pickupItemMessage.ItemID = extensionItemType.ID;
                        pickupItemMessage.ItemCount = pickupItemMessage.LoadedCount = GetItemCount(extensionItemType, true);
                        pickupItemMessage.UnloadedCount = -1;
                        NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, pickupItemMessage);
                    }
                }
            }

            // Send the new client all of the secondary items and the number of items that the character is carrying.
            for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                var item = m_SecondaryInventory[i].Item;
                // No reason to send the item if the character doesn't have the item.
                if (m_SecondaryInventory[i].ItemCount > 0) {
                    pickupItemMessage.ItemID = item.ItemType.ID;
                    pickupItemMessage.LoadedCount = GetItemCount(item.ItemType, true);
                    pickupItemMessage.UnloadedCount = 0;
                    NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, pickupItemMessage);
                }
            }

            ObjectPool.Return(pickupItemMessage);
        }

        /// <summary>
        /// Load the correct item for each character.
        /// </summary>
        /// <param name="netMsg">The message being sent.</param>
        private void NetworkSetActiveItem(NetworkMessage netMsg)
        {
            // The server is going to be sending a list of items that the character has. Remove all items to start fresh.
            RemoveAllItems(false);

            var activeItemMessage = netMsg.ReadMessage<ActiveItemMessage>();
            m_StartItemID = activeItemMessage.ItemID;
            m_StartDualWieldItemID = activeItemMessage.DualWieldItemID;
            m_InventoryLoaded = true;
        }

        /// <summary>
        /// The server has sent the client an item that the character is carrying. Update the inventory.
        /// </summary>
        /// <param name="netMsg">The message being sent.</param>
        private void NetworkPickupItem(NetworkMessage netMsg)
        {
            var pickupItemMessage = netMsg.ReadMessage<PickupItemMessage>();
            // ActiveItemMessage is received before PickupItemMessage so it is safe to use StartItemID.
            PickupItemLocal(pickupItemMessage.ItemID, pickupItemMessage.ItemCount, m_StartItemID == pickupItemMessage.ItemID || m_StartDualWieldItemID == pickupItemMessage.ItemID, true);
            // The item has been added to the inventory. PickupItem assumes the item was just picked up so the loaded and unloaded counts need to be updated.
            SetItemCount(m_IDItemTypeMap[pickupItemMessage.ItemID], pickupItemMessage.LoadedCount, pickupItemMessage.UnloadedCount);
            EventHandler.ExecuteEvent(m_GameObject, "OnInventoryNetworkMessageAdd");
        }

        /// <summary>
        /// The client has left the server. Reset to the default values.
        /// </summary>
        public override void OnNetworkDestroy()
        {
            base.OnNetworkDestroy();

            m_InventoryLoaded = false;
            m_StartItemID = -1;
            m_StartDualWieldItemID = -1;
        }
#endif

        /// <summary>
        /// Initialize the inventory.
        /// </summary>
        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes the inventory.
        /// </summary>
        private void Initialize()
        {
            // Do not initialize if the inventory has already been initialized.
            if (m_IDItemTypeMap.Count > 0) {
                return;
            }

            SharedManager.InitializeSharedFields(m_GameObject, this);
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);

            InitItems(GetComponentsInChildren<Item>(true));

            // Do not load the default loadout if the client is joining an existing game. The inventory has already been loaded at this point.
            if (!m_InventoryLoaded) {
                LoadDefaultLoadout();
            }

            EventHandler.ExecuteEvent(m_GameObject, "OnInventoryInitialized");
        }

        /// <summary>
        /// Add all of the items that the inventory can carry to the internal lists. Note that this doesn't mean that the character now has these items and can use them,
        /// rather it means that the character has the potential to use these items. The character still needs to pick the actual item up or have it in their default loadout.
        /// </summary>
        /// <param name="items">An array of items that the Inventory can use.</param>
        private void InitItems(Item[] items)
        {
            if (m_ItemOrder != null && m_ItemOrder.Length > 0) {
                // Create a dictionary which maps the ItemType to the element index.
                var orderMap = new Dictionary<ItemType, int>();
                for (int i = 0; i < m_ItemOrder.Length; ++i) {
                    orderMap.Add(m_ItemOrder[i], i);
                }

                // Set the index for any items which have a specified ordering.
                int index;
                var order = new int[items.Length];
                for (int i = 0; i < items.Length; ++i) {
                    if (orderMap.TryGetValue(items[i].ItemType, out index)) {
                        order[i] = index;
                    } else {
                        // Indicate no specified ordering with -1.
                        order[i] = -1;
                    }
                }

                // Set the index for all of the items which do not have a specified ordering.
                int insertCount = 0;
                for (int i = 0; i < items.Length; ++i) {
                    if (order[i] == -1) {
                        order[i] = m_ItemOrder.Length + insertCount;
                        insertCount++;
                    }
                }

                // Arrange the items based on the order.
                Array.Sort(order, items);
            }

            for (int i = 0; i < items.Length; ++i) {
                AddInventoryItem(items[i]);

                items[i].Init(this);
            }
        }

        /// <summary>
        /// Adds a particular item type to the inventory. This will convert the item type to an actual item instance.
        /// </summary>
        /// <param name="item">The item that is being added.</param>
        private void AddInventoryItem(Item item)
        {
#if UNITY_EDITOR
            if (m_ItemIndexMap.ContainsKey(item.ItemType)) {
                Debug.LogError("Error: Unable to add " + item.ItemType + " to the Inventory because the ItemType has already been added.");
                return;
            }
#endif
            // Add the item to the correct inventory list.
            if (item.ItemType is PrimaryItemType) {
                var primaryItemType = item.ItemType as PrimaryItemType;
                // If the item is a primary item then the consumable item should also be added.
                m_ItemIndexMap.Add(primaryItemType, m_PrimaryInventory.Count);
                var itemInstance = new ItemInstance(item);
                m_PrimaryInventory.Add(itemInstance);
                if (primaryItemType.ConsumableItem != null) {
                    if (primaryItemType.ConsumableItem.ItemType != null) {
                        int index;
                        ConsumableItemInstance consumableItem;
                        if (m_ItemIndexMap.TryGetValue(primaryItemType.ConsumableItem.ItemType, out index)) {
                            consumableItem = m_ConsumableInventory[index];
                            consumableItem.AddItemType(primaryItemType);
                        } else {
                            consumableItem = new ConsumableItemInstance(primaryItemType.ConsumableItem.ItemType, primaryItemType.ConsumableItem.Capacity, primaryItemType);
                            m_ItemIndexMap.Add(primaryItemType.ConsumableItem.ItemType, m_ConsumableInventory.Count);
                            m_ConsumableInventory.Add(consumableItem);
#if UNITY_EDITOR
                            if (m_IDItemTypeMap.ContainsKey(primaryItemType.ConsumableItem.ItemType.ID)) {
                                Debug.LogError("Error: Unable to add " + primaryItemType.ConsumableItem.ItemType + " to the Inventory because the ItemType ID has already been added.");
                                return;
                            }
#endif
                            m_IDItemTypeMap.Add(primaryItemType.ConsumableItem.ItemType.ID, primaryItemType.ConsumableItem.ItemType);
                        }
                        itemInstance.ConsumableItem = consumableItem;

                        for (int i = 0; i < primaryItemType.DualWieldItems.Length; ++i) {
                            consumableItem.AddItemType(primaryItemType.DualWieldItems[i].ItemType);
                        }
                    }
                    if (primaryItemType.AdditionalConsumableItems != null) {
                        for (int i = 0; i < primaryItemType.AdditionalConsumableItems.Length; ++i) {
                            if (primaryItemType.AdditionalConsumableItems[i].ItemType != null) {
                                int index;
                                ConsumableItemInstance consumableItem;
                                if (m_ItemIndexMap.TryGetValue(primaryItemType.AdditionalConsumableItems[i].ItemType, out index)) {
                                    consumableItem = m_ConsumableInventory[index];
                                    consumableItem.AddItemType(primaryItemType.AdditionalConsumableItems[i].ItemType);
                                } else {
                                    consumableItem = new ConsumableItemInstance(primaryItemType.AdditionalConsumableItems[i].ItemType, 
                                                            primaryItemType.AdditionalConsumableItems[i].Capacity, primaryItemType);
                                    m_ItemIndexMap.Add(primaryItemType.AdditionalConsumableItems[i].ItemType, m_ConsumableInventory.Count);
                                    m_ConsumableInventory.Add(consumableItem);
#if UNITY_EDITOR
                                    if (m_IDItemTypeMap.ContainsKey(primaryItemType.AdditionalConsumableItems[i].ItemType.ID)) {
                                        Debug.LogError("Error: Unable to add " + primaryItemType.AdditionalConsumableItems[i].ItemType + " to the Inventory because the ItemType ID has already been added.");
                                        return;
                                    }
#endif
                                    m_IDItemTypeMap.Add(primaryItemType.AdditionalConsumableItems[i].ItemType.ID, primaryItemType.AdditionalConsumableItems[i].ItemType);
                                }
                            }
                        }
                    }
                }
                // Add the ItemInstance to the DualWieldInventory if it can be dual wielded.
                if ((item.ItemType as PrimaryItemType).DualWieldItems.Length > 0) {
                    m_PrimaryDualWieldItemIndexMap.Add(primaryItemType, m_DualWieldInventory.Count);
                    m_DualWieldInventory.Add(itemInstance);
                }
            } else if (item.ItemType is SecondaryItemType) {
                m_ItemIndexMap.Add(item.ItemType, m_SecondaryInventory.Count);
                m_SecondaryInventory.Add(new ItemInstance(item));
            } else if (item.ItemType is DualWieldItemType) {
                m_ItemIndexMap.Add(item.ItemType, m_DualWieldInventory.Count);
                m_DualWieldInventory.Add(new ItemInstance(item));

                var dualWieldItemType = item.ItemType as DualWieldItemType;
#if UNITY_EDITOR
                if (m_DualWieldPrimaryItemMap.ContainsKey(item.ItemType)) {
                    Debug.LogError("Error: Unable to add " + item.ItemType + " to the Dual Wield Inventory because the ItemType has already been added.");
                    return;
                }
                if (m_PrimaryDualWieldItemMap.ContainsKey(dualWieldItemType.PrimaryItem)) {
                    Debug.LogError("Error: Unable to add " + dualWieldItemType.PrimaryItem + " to the Dual Wield Primary Inventory because the Item has already been added.");
                    return;
                }
#endif
                m_DualWieldPrimaryItemMap.Add(item.ItemType, dualWieldItemType.PrimaryItem);
                m_PrimaryDualWieldItemMap.Add(dualWieldItemType.PrimaryItem, item.ItemType);
            }
#if UNITY_EDITOR
            if (m_IDItemTypeMap.ContainsKey(item.ItemType.ID)) {
                Debug.LogError("Error: Unable to add " + item.ItemType + " to the Inventory because the ItemType ID has already been added.");
                return;
            }
#endif
            m_IDItemTypeMap.Add(item.ItemType.ID, item.ItemType);
        }

        /// <summary>
        /// Loop through the default loadout list picking up each item.
        /// </summary>
        public void LoadDefaultLoadout()
        {
            if (m_DefaultLoadout != null) {
                m_InventoryLoaded = true;
                var loadUnequipItemType = m_UnequippedItemType != null;
                for (int i = 0; i < m_DefaultLoadout.Length; ++i) {
                    PickupItemLocal(m_DefaultLoadout[i].ItemType.ID, m_DefaultLoadout[i].Amount, m_DefaultLoadout[i].Equip, true);
                    if (loadUnequipItemType && m_DefaultLoadout[i].ItemType == m_UnequippedItemType) {
                        loadUnequipItemType = false;
                    }
                }
                // The unequipped item type is an extension of the default loadout.
                if (loadUnequipItemType) {
                    PickupItemLocal(m_UnequippedItemType.ID, 1, false, true);
                }
                EventHandler.ExecuteEvent(m_GameObject, "OnInventoryLoadDefaultLoadout");
            }
        }

        /// <summary>
        /// Pickup an item. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemType">The type of item to pickup.</param>
        /// <param name="amount">The number of items to pickup.</param>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="immediateActivation">Should the item be shown immediately? This only applies to the PrimaryItemType. If false the item will be added with an animation.</param>
        public void PickupItem(int itemID, int amount, bool equip, bool immediateActivation)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcPickupItem(itemID, amount, equip, immediateActivation);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                PickupItemLocal(itemID, amount, equip, immediateActivation);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Pickup an item on the client.
        /// </summary>
        /// <param name="itemType">The type of item to pickup.</param>
        /// <param name="amount">The number of items to pickup.</param>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="immediateActivation">Should the item be shown immediately? This only applies to the PrimaryItemType. If false the item will be added with an animation.</param>
        [ClientRpc]
        private void RpcPickupItem(int itemID, int amount, bool equip, bool immediateActivation)
        {
            PickupItemLocal(itemID, amount, equip, immediateActivation);
        }
#endif

        /// <summary>
        /// Pickup an item. Picking up an item will allow the item to actually be used. Note that if the item is a primary item that it will still need to be equipped.
        /// </summary>
        /// <param name="itemType">The type of item to pickup.</param>
        /// <param name="amount">The number of items to pickup.</param>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="immediateActivation">Should the item be shown immediately? This only applies to the PrimaryItemType. If false the item will be added with an animation.</param>
        private void PickupItemLocal(int itemID, int amount, bool equip, bool immediateActivation)
        {
            ItemType itemType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemType)) {
                Debug.LogError("Unable to pickup item with id " + itemID + ": has it been added to an item object?");
                return;
            }

            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                Debug.LogError("Unable to pickup " + itemType + ": has it been added to an item object?");
                return;
            }

            // If an item is being picked up it must at least have a count of one, even if only consumable items are used.
            if (amount == 0) {
                amount = 1;
            }

            // Immediately activate the item if the item cannot be interacted with. The roll ability will prevent animations since the character cannot play the 
            // equip/unequip animations while rolling. CanInteractItem will be null if the item is being initialized over the network - InventoryItem is called before start. 
            if (m_CanInteractItem != null && !m_CanInteractItem.Invoke()) {
                immediateActivation = true;
            }
            if (itemType is PrimaryItemType) {
                // Only add the item if it hasn't already been added.
                if (m_PrimaryInventory[itemIndex].ItemCount == 0) {
                    m_PrimaryInventory[itemIndex].ItemCount = 1;
                    amount -= 1;
                    if (equip) {
                        // Deactivate the previous item. If the item should not be immediately activated then the animation needs to deactivate the old item
                        // and activate the new item as soon as the old item has been removed.
                        var waitForUnequip = false;
                        var dualWieldItem = false;
                        if (m_CurrentPrimaryIndex != -1) {
                            if (m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType != m_UnequippedItemType) {
                                if (!(dualWieldItem = CanDualWield(m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType, itemType as PrimaryItemType))) {
                                    if (!immediateActivation) {
                                        m_EquipIndex = itemIndex;
                                        waitForUnequip = true;
                                    }
                                    if (m_CurrentDualWieldIndex != -1) {
                                        EquipUnequipDualWieldItem(false, m_CurrentDualWieldIndex, immediateActivation);
                                    }
                                    EquipUnequipItem(false, m_CurrentPrimaryIndex, immediateActivation);
                                } else {
                                    if (m_CurrentDualWieldIndex != -1) {
                                        if (!immediateActivation) {
                                            m_DualWieldEquipIndex = itemIndex;
                                            waitForUnequip = true;
                                        }
                                        EquipUnequipDualWieldItem(false, m_CurrentDualWieldIndex, immediateActivation);
                                    }
                                }
                            }
                        }
                        if (!waitForUnequip) {
                            if (!dualWieldItem) {
                                EquipUnequipItem(true, itemIndex, immediateActivation);
                            } else {
                                PickupDualWieldItem(itemType, itemIndex, immediateActivation);
                            }
                        }
                    } else {
                        m_PrimaryInventory[itemIndex].SetActive(false);
                    }

                    // Let the item know that it already has a consumable item if the consumable item has already been added.
                    var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                    if (consumableItem != null && consumableItem.UnloadedCount > 0) {
                        EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_PrimaryInventory[itemIndex].Item, true, consumableItem.GetPrimaryItemCount(itemType) == 0);
                    }
                }

                if (m_PrimaryInventory[itemIndex].ItemCount == 1 && amount > 0) {
                    // If the inventory already has the item and a different primary item is equipped then determine if the newly acquired item can be dualwielded. 
                    // Do not pick up the item if it can't. If the EquipIndex is not -1 then that item is about to be equipped.
                    var index = m_EquipIndex != -1 ? m_EquipIndex : m_CurrentPrimaryIndex;
                    if (index != -1 && index != itemIndex) {
                        if (m_UnequippedItemType != null && index == m_ItemIndexMap[m_UnequippedItemType]) {
                            EquipItem(m_ToggleEquippedIndex);
                            index = m_ToggleEquippedIndex;
                        }
                        if (CanDualWield(m_PrimaryInventory[index].ItemType as PrimaryItemType, itemType as PrimaryItemType)) {
                            PickupDualWieldItem(itemType, itemIndex, immediateActivation);
                        }
                    }
                    ItemType dualWieldItemType;
                    if (m_PrimaryDualWieldItemMap.TryGetValue(itemType, out dualWieldItemType)) {
                        var primaryItemIndex = itemIndex;
                        // Get the ConsumableItem of the PrimaryItemType before switching to the DualWieldItemType. There are no ConsumableItems with the DualWieldItemType.
                        var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                        if (!m_ItemIndexMap.TryGetValue(dualWieldItemType, out itemIndex)) {
                            Debug.LogError("Unable to pickup " + dualWieldItemType + ": has it been added to an item object?");
                            return;
                        }
                        m_PrimaryInventory[primaryItemIndex].ItemCount = 2;
                        
                        // Do not equip the dual wield item if a primary item is currently being equipped.
                        if (m_EquipIndex == -1) {
                            if (equip && primaryItemIndex == m_CurrentPrimaryIndex) {
                                EquipUnequipDualWieldItem(true, itemIndex, immediateActivation);
                            }
                        } else {
                            m_DualWieldEquipIndex = itemIndex;
                        }
                        // Let the item know that it can reload.
                        if (consumableItem != null && consumableItem.UnloadedCount > 0) {
                            EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_DualWieldInventory[itemIndex].Item, true, true);
                        }
                    }
                }
            } else if (itemType is ConsumableItemType) {
                m_ConsumableInventory[itemIndex].UnloadedCount += amount;
                // The item should be reloaded immediately if immediateActivation is true (coming from a default loadout) or if the item currently is not equipped. If the item
                // is equipped then the reload animation should play.
                for (int i = 0; i < m_ConsumableInventory[itemIndex].ParentItemType.Count; ++i) {
                    var parentItemType = m_ConsumableInventory[itemIndex].ParentItemType[i];
                    int primaryIndex;
                    if (parentItemType is PrimaryItemType && m_ItemIndexMap.TryGetValue(parentItemType, out primaryIndex)) {
                        var primaryEquipped = primaryIndex == m_CurrentPrimaryIndex && m_EquipIndex == -1;
                        // Let any interested objects know that a consumable item has been added.
                        EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_PrimaryInventory[primaryIndex].Item, true, immediateActivation || !primaryEquipped);
                    }
                }
            } else if (itemType is SecondaryItemType) {
                m_SecondaryInventory[itemIndex].ItemCount += amount;
                // Equip the secondary item if it has just been added.
                if (m_SecondaryInventory[itemIndex].ItemCount <= amount) {
                    m_CurrentSecondaryIndex = itemIndex;
                }

                EventHandler.ExecuteEvent(m_GameObject, "OnInventorySecondaryItemCountChange");
            }
        }

        /// <summary>
        /// Can the current ItemType be dual wielded with the new ItemType?
        /// </summary>
        /// <param name="currentItemType">The Item currently equipped.</param>
        /// <param name="newItemType">The Item that is being equipped.</param>
        /// <returns>True if the new ItemType can be dual wielded with the currentItemType.</returns>
        private bool CanDualWield(PrimaryItemType currentItemType, PrimaryItemType newItemType)
        {
            for (int i = 0; i < currentItemType.DualWieldItems.Length; ++i) {
                if (currentItemType.DualWieldItems[i].ItemType.Equals(newItemType)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Pickup the new dual wield Item. 
        /// </summary>
        /// <param name="itemType">The ItemType of the dual wield item being picked up.</param>
        /// <param name="primaryItemIndex">The index of the item within the PrimaryItemInventory list.</param>
        /// <param name="immediateActivation">Should the dual wield item be activated immediately?</param>
        private void PickupDualWieldItem(ItemType itemType, int primaryItemIndex, bool immediateActivation)
        {
            int itemIndex;
            // A dual wield item of a different PrimaryItemType has been picked up. Enable that item.
            if (!m_PrimaryDualWieldItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return;
            }
            // Determine which item should be the primary item.
            var currentPrimaryItemType = m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType;
            var newPrimary = false;
            for (int i = 0; i < currentPrimaryItemType.DualWieldItems.Length; ++i) {
                if (currentPrimaryItemType.DualWieldItems[i].ItemType.Equals(itemType)) {
                    newPrimary = currentPrimaryItemType.DualWieldItems[i].PrimaryName;
                    break;
                }
            }
            // The new dual wield item should be the primary item.
            if (newPrimary) {
                m_CurrentDualWieldIndex = m_PrimaryDualWieldItemIndexMap[m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType];
                EquipUnequipItem(true, primaryItemIndex, immediateActivation);
            } else {
                EquipUnequipDualWieldItem(true, itemIndex, immediateActivation);
            }
        }

        /// <summary>
        /// Returns the DualWieldItemType for the specified PrimaryItemType.
        /// </summary>
        /// <param name="itemType">The PrimaryItemType to get the DualWieldItemType of.</param>
        /// <returns>The DualWieldItemType mapped to the specified PrimaryItemType. Can be null.</returns>
        public ItemType DualWieldItemForPrimaryItem(ItemType itemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return null;
            }

            // The character doesn't have the dual wielded item if the count isn't greater then 1.
            if (m_PrimaryInventory[itemIndex].ItemCount != 2) {
                return null;
            }

            ItemType dualWieldItemType;
            if (m_PrimaryDualWieldItemMap.TryGetValue(itemType, out dualWieldItemType)) {
                return dualWieldItemType;
            }
            return null;
        }

        /// <summary>
        /// Returns a list of items which are currently available in the inventory.
        /// </summary>
        /// <param name="itemTypes">The list of items which are currently available.</param>
        public void GetAvailableItems(ref List<ItemType> itemTypes)
        {
            itemTypes.Clear();

            // Return all of the primary, secondary, and unequipped items that have a count greater than 0.
            for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                if (m_PrimaryInventory[i].ItemCount > 0) {
                    itemTypes.Add(m_PrimaryInventory[i].ItemType);
                }
            }
            for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                if (m_SecondaryInventory[i].ItemCount > 0) {
                    itemTypes.Add(m_SecondaryInventory[i].ItemType);
                }
            }
        }

        /// <summary>
        /// Returns the current primary, secondary, or dual wield item.
        /// </summary>
        /// <param name="itemType">The type of item that should be retrieved.</param>
        /// <returns>The current primary, secondary, or dual wield item.</returns>
        public Item GetCurrentItem(Type itemType)
        {
            if (typeof(PrimaryItemType).IsAssignableFrom(itemType)) {
                if (m_CurrentPrimaryIndex == -1) {
                    if (m_UnequippedItemType != null) {
                        int itemIndex;
                        if (m_ItemIndexMap.TryGetValue(m_UnequippedItemType, out itemIndex)) {
                            return m_PrimaryInventory[itemIndex].Item;
                        }
                    }
                    return null;
                }
                return m_PrimaryInventory[m_CurrentPrimaryIndex].Item;
            } else if (typeof(SecondaryItemType).IsAssignableFrom(itemType)) {
                if (m_CurrentSecondaryIndex == -1) {
                    return null;
                }
                return m_SecondaryInventory[m_CurrentSecondaryIndex].Item;
            } else {
                if (m_CurrentDualWieldIndex == -1) {
                    return null;
                }
                return m_DualWieldInventory[m_CurrentDualWieldIndex].Item;
            }
        }

        /// <summary>
        /// Returns the Item that is used by the ItemType.
        /// </summary>
        /// <param name="itemType">The corresponding ItemType.</param>
        /// <returns>The Item that is used by the ItemType.</returns>
        public Item GetItem(ItemType itemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return null;
            }

            if (itemType is PrimaryItemType) {
                return m_PrimaryInventory[itemIndex].Item;
            } else if (itemType is ConsumableItemType) {
                var consumableItem = m_ConsumableInventory[itemIndex];
                return m_PrimaryInventory[m_ItemIndexMap[consumableItem.ParentItemType[0]]].Item;
            } else if (itemType is SecondaryItemType) {
                return m_SecondaryInventory[itemIndex].Item;
            } else { // DualWieldItemType.
                return m_DualWieldInventory[itemIndex].Item;
            }
        }

        /// <summary>
        /// Returns the count for the current primary or secondary item. If primary item is specified then loadedCount specifies if the loaded or unloaded count should be returned.
        /// </summary>
        /// <param name="type">The type of item that should be retrieved.</param>
        /// <param name="loadedCount">If the primary item count is requested, should the loaded count be returned? If false the unloaded count will be returned.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetCurrentItemCount(Type type, bool loadedCount)
        {
            return GetCurrentItemCount(type, loadedCount, -1);
        }

        /// <summary>
        /// Returns the count for the current primary or secondary item. If primary item is specified then loadedCount specifies if the loaded or unloaded count should be returned.
        /// </summary>
        /// <param name="type">The type of item that should be retrieved.</param>
        /// <param name="loadedCount">If the primary item count is requested, should the loaded count be returned? If false the unloaded count will be returned.</param>
        /// <param name="extensionIndex">The item extension to get the count of. -1 equals no extension.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetCurrentItemCount(Type type, bool loadedCount, int extensionIndex)
        {
            var item = GetCurrentItem(type);
            if (item == null) {
                return 0;
            }
            ItemType itemType;
            if (extensionIndex != -1) {
                if (extensionIndex < item.ItemExtensions.Length) {
                    itemType = item.ItemExtensions[extensionIndex].ConsumableItemType;
                } else { // No ExtensionItem matches the extensionIndex, return -1.
                    return -1;
                }
            } else {
                itemType = item.ItemType;
            }

            return GetItemCount(itemType, loadedCount, false);
        }

        /// <summary>
        /// Returns the item count for the specified item type.
        /// </summary>
        /// <param name="itemType">The interested item type.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetItemCount(ItemType itemType)
        {
            return GetItemCount(itemType, true, false);
        }

        /// <summary>
        /// Returns the item count for the specified item type.
        /// </summary>
        /// <param name="itemType">The interested item type.</param>
        /// <param name="loadedCount">If the primary item count is requested, should the loaded count be returned? If false the unloaded count will be returned.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetItemCount(ItemType itemType, bool loadedCount)
        {
            return GetItemCount(itemType, loadedCount, false);
        }

        /// <summary>
        /// Returns the item count for the specified item type and if the loaded or unloaded count should be returned. The loaded parameter is only used for primary items.
        /// </summary>
        /// <param name="itemType">The interested item type.</param>
        /// <param name="loadedCount">If the primary item count is requested, should the loaded count be returned? If false the unloaded count will be returned.</param>
        /// <param name="primaryItemCount">Should the primary item count be returned?</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetItemCount(ItemType itemType, bool loadedCount, bool primaryItemCount)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return -1;
            }

            // A DualWieldItemType Item uses the mapped ItemType for all of its properties.
            var origItemType = itemType;
            if (itemType is DualWieldItemType) {
                if (!m_DualWieldPrimaryItemMap.TryGetValue(itemType, out itemType)) {
                    return -1;
                }
                // The item index for the DualWieldItemType has been found. However, the item index for the PrimaryItemType needs to be used. At this point
                // itemType has been changed to a PrimaryItemType so get the index one more time.
                if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                    return -1;
                }
            }

            // Always return the max value if the item exists and has unlimited ammo.
            if (m_UnlimitedAmmo) {
                return int.MaxValue;
            }

            // Return the actual item count if the item is a secondary item. If the item is a primary item or consumable item then
            // return the loaded or unloaded count of the consumable item.
            if (itemType is PrimaryItemType || itemType is ConsumableItemType) {
                ConsumableItemInstance consumableItem;
                if (itemType is PrimaryItemType) {
                    if (primaryItemCount) {
                        return m_PrimaryInventory[itemIndex].ItemCount;
                    }
                    consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                } else {
                    consumableItem = m_ConsumableInventory[itemIndex];
                }
                if (consumableItem != null) {
                    if (loadedCount) {
                        return consumableItem.GetPrimaryItemCount(origItemType);
                    } else {
                        return consumableItem.UnloadedCount;
                    }
                }
                // If there is no ConsumableItem then the item has unlimited ammo.
                return int.MaxValue;
            } else { // SecondaryItemType.
                return m_SecondaryInventory[itemIndex].ItemCount;
            }
        }

        /// <summary>
        /// Sets the loaded and unloaded count for the specified item type.
        /// </summary>
        /// <param name="itemType">The interested item type.</param>
        /// <param name="loadedCount">The item's loaded count.</param>
        /// <param name="unloadedCount">The item's unloaded count.</param>
        public void SetItemCount(ItemType itemType, int loadedCount, int unloadedCount)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return;
            }

            if (m_UnlimitedAmmo) {
                return;
            }

            if (itemType is PrimaryItemType || itemType is ConsumableItemType) {
                ConsumableItemInstance consumableItem;
                Item item = null;
                if (itemType is PrimaryItemType) {
                    item = m_PrimaryInventory[itemIndex].Item;
                    consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                } else {
                    consumableItem = m_ConsumableInventory[itemIndex];
                }
                if (consumableItem != null) {
                    consumableItem.SetPrimaryItemCount(itemType, loadedCount, true);
                    // unloadedCount will be -1 in the case of a network sync of multiple PrimaryItemTypes for a single ConsumableItem.
                    if (unloadedCount != -1) {
                        consumableItem.UnloadedCount = unloadedCount;
                    }
                    if (item != null) {
                        EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, false, true);
                    }
                }
            } else { // SecondaryItemType
                m_SecondaryInventory[itemIndex].ItemCount = loadedCount;
            }
        }

        /// <summary>
        /// An item has been used. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemType">The type of item used.</param>
        /// <param name="amount">The number of items used.</param>
        public void UseItem(ItemType itemType, int amount)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcUseItem(itemType.ID, amount);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                UseItem(itemType.ID, amount);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// An item has been used on the client.
        /// </summary>
        /// <param name="itemID">The id of the item.</param>
        /// <param name="amount">The number of items used.</param>
        [ClientRpc]
        private void RpcUseItem(int itemID, int amount)
        {
            UseItem(itemID, amount);
        }
#endif

        /// <summary>
        /// An item has been used. Decrement the used amount from the inventory.
        /// </summary>
        /// <param name="primaryItem">Is the item a primary item?</param>
        /// <param name="itemID">The ID of the item within the inventory list.</param>
        /// <param name="amount">The number of items used.</param>
        private int UseItem(int itemID, int amount)
        {
            ItemType itemType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemType)) {
                return 0;
            }

            // A DualWieldItemType Item uses the mapped ItemType for all of its properties.
            var isDualWieldItem = false;
            var origItemType = itemType;
            if ((isDualWieldItem = itemType is DualWieldItemType)) {
                if (!m_DualWieldPrimaryItemMap.TryGetValue(itemType, out itemType)) {
                    Debug.LogError("Error: The DualWieldItemType of " + itemType + " is not mapped to a PrimaryItemType");
                    return 0;
                }
            }

            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return 0;
            }

            if (m_UnlimitedAmmo) {
                amount = 0;
            }

            if (itemType is PrimaryItemType || itemType is ConsumableItemType) {
                ConsumableItemInstance consumableItem;
                if (itemType is PrimaryItemType) {
                    // Do not subtract from the primary item type as we are interested in the consumable item count.
                    consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                } else {
                    consumableItem = m_ConsumableInventory[itemIndex];
                }
                // int.MaxValue indicates unlimited ammo.
                if (consumableItem.GetPrimaryItemCount(itemType) == int.MaxValue) {
                    amount = 0;
                }
                consumableItem.SetPrimaryItemCount(origItemType, -amount, false);
                Item item = null;
                if (isDualWieldItem) {
                    // Convert back to the dual wield item for the consumable item count change event.
                    item = m_DualWieldInventory[m_ItemIndexMap[origItemType]].Item;
                } else {
                    item = m_PrimaryInventory[itemIndex].Item;
                }
#if ENABLE_MULTIPLAYER
                EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, isServer, false);
#else
                EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, false, false);
#endif

                return consumableItem.GetPrimaryItemCount(origItemType);
            } else { // SecondaryItemType.
                // int.MaxValue indicates unlimited ammo.
                if (m_SecondaryInventory[itemIndex].ItemCount == int.MaxValue) {
                    amount = 0;
                }
                m_SecondaryInventory[itemIndex].ItemCount -= amount;
                EventHandler.ExecuteEvent(m_GameObject, "OnInventorySecondaryItemCountChange");

                return m_SecondaryInventory[itemIndex].ItemCount;
            }
        }
        
        /// <summary>
        /// Reload the item with the specified amount. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemType">The type of item that should be reloaded.</param>
        /// <param name="amount">The amount of consumable items to reload the item with.</param>
        public void ReloadItem(ItemType itemType, int amount)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcReloadItem(itemType.ID, amount);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ReloadItemLocal(itemType.ID, amount);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Reload the item with the specified amount on the client.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        /// <param name="amount">The amount of consumable items to reload the item with.</param>
        [ClientRpc]
        private void RpcReloadItem(int itemIndex, int amount)
        {
            ReloadItemLocal(itemIndex, amount);
        }
#endif

        /// <summary>
        /// Reload the item with the specified amount. Only primary items can be reloaded.
        /// </summary>
        /// <param name="itemID">The ID of the item within the inventory list.</param>
        /// <param name="amount">The amount of consumable items to reload the item with.</param>
        private void ReloadItemLocal(int itemID, int amount)
        {
            ItemType itemType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemType)) {
                return;
            }

            // A DualWieldItemType Item uses the mapped ItemType for all of its properties.
            var dualWieldItem = false;
            var origItemType = itemType;
            if (dualWieldItem = itemType is DualWieldItemType) {
                if (!m_DualWieldPrimaryItemMap.TryGetValue(itemType, out itemType)) {
                    return;
                }
            }

            int itemIndex;
            if ((!(itemType is PrimaryItemType) && !(itemType is ConsumableItemType)) || !m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return;
            }

            ConsumableItemInstance consumableItem;
            if (itemType is PrimaryItemType) {
                consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
            } else {
                consumableItem = m_ConsumableInventory[itemIndex];
            }

            // The item can't load more than what is in unloaded.
            if (amount > consumableItem.UnloadedCount) {
                amount = consumableItem.UnloadedCount;
            }
            consumableItem.SetPrimaryItemCount(origItemType, amount, false);

            // int.MaxValue indicates unlimited ammo.
            if (consumableItem.UnloadedCount != int.MaxValue) {
                consumableItem.UnloadedCount -= amount;
            }

            // Notify the ConsumableItemChange with the original item.
            Item item;
            if (dualWieldItem) {
                item = m_DualWieldInventory[m_ItemIndexMap[origItemType]].Item;
            } else {
                item = m_PrimaryInventory[itemIndex].Item;
            }
            EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, false, false);
        }

        /// <summary>
        /// Switch the item to the next item in the inventory list. Call the corresponding client or server method.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        public void SwitchItem(bool primaryItem, bool next)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcSwitchItem(primaryItem, next);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                SwitchItemLocal(primaryItem, next);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Switch the item to the next item in the inventory list on the client.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        [ClientRpc]
        private void RpcSwitchItem(bool primaryItem, bool next)
        {
            SwitchItemLocal(primaryItem, next);
        }
#endif

        /// <summary>
        /// Switch the item to the next item in the inventory list.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        private void SwitchItemLocal(bool primaryItem, bool next)
        {
            var currentIndex = (primaryItem ? m_CurrentPrimaryIndex : m_CurrentSecondaryIndex);
            var itemIndex = SwitchItem(primaryItem, next, currentIndex);
            if (itemIndex == currentIndex || itemIndex == -1) {
                return;
            }

            // A new item index has been retrieved and now the current variables need to be updated.
            if (primaryItem) {
                if (itemIndex != -1) {
                    EquipItemLocal(itemIndex);
                } else {
                    // The current primary item is null. Animate the removal of the item.
                    EquipUnequipItem(false, -1, false);
                }
            } else { // SecondaryItemType
                m_CurrentSecondaryIndex = itemIndex;
            }
        }

        /// <summary>
        /// Determine the index of the next/previous item in the inventory. A valid index number will always be returned, meaning an item which does not have any ammo will not be returned.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item will be used.</param>
        /// <param name="next">Should the inventory switch to the next item? If false then the inventory will switch to the previous item.</param>
        /// <param name="currentItemIndex">The index of the current item activated.</param>
        /// <returns>The index of the item that the inventory should switch to.</returns>
        private int SwitchItem(bool primaryItem, bool next, int currentItemIndex)
        {
            if (currentItemIndex != -1) {
                // Do not switch items if the unequipped item type is equipped.
                if (primaryItem && (!m_CanSwitchToUnequippedItemType && m_PrimaryInventory[currentItemIndex].ItemType == m_UnequippedItemType)) {
                    return -1;
                }
                var inventory = (primaryItem ? m_PrimaryInventory : m_SecondaryInventory);
                var itemIndex = (currentItemIndex + (next ? 1 : -1)) % inventory.Count;
                if (itemIndex < 0) itemIndex = inventory.Count - 1;
                // Loop through the inventory list until an item is found or the entire inventory has been searched..
                var i = 0;
                while (i < inventory.Count) {
                    if (inventory[itemIndex].ItemCount > 0 && 
                        (!primaryItem || m_CurrentDualWieldIndex == -1 || m_DualWieldInventory[m_CurrentDualWieldIndex].Item != inventory[itemIndex].Item) &&
                        (m_CanSwitchToUnequippedItemType || inventory[itemIndex].ItemType != m_UnequippedItemType)) {
                        return itemIndex;
                    }
                    itemIndex = (itemIndex + (next ? 1 : -1)) % inventory.Count;
                    if (itemIndex < 0) itemIndex = inventory.Count - 1;
                    i++;
                }
            }
            return -1;
        }

        /// <summary>
        /// Does the character have the specified item?
        /// </summary>
        /// <param name="itemType">The item to check against.</param>
        public bool HasItem(ItemType itemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return false;
            }

            if (itemType is PrimaryItemType) {
                var item = m_PrimaryInventory[itemIndex];
                return item.ItemCount > 0;
            } else {
                var item = m_SecondaryInventory[itemIndex];
                return item.ItemCount > 0;
            }
        }

        /// <summary>
        /// Does the character have the specified item?
        /// </summary>
        /// <param name="itemType">The item to check against.</param>
        private bool SharedMethod_HasItem(ItemType itemType)
        {
            return HasItem(itemType);
        }
        /// <summary>
        /// Returns the GameObject which has the given item id.
        /// </summary>
        /// <param name="id">The item id of the GameObject.</param>
        /// <returns>The GameObject corresponding to the item id.</returns>
        public GameObject SharedMethod_GameObjectWithItemID(int id)
        {
            ItemType itemType = null;
            if (m_IDItemTypeMap.TryGetValue(id, out itemType)) {
                int itemIndex;
                if (m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                    if (itemType is PrimaryItemType) {
                        return m_PrimaryInventory[itemIndex].Item.gameObject;
                    } else if (itemType is ConsumableItemType) {
                        var primaryItemType = m_ConsumableInventory[itemIndex].ParentItemType[0];
                        return m_PrimaryInventory[m_ItemIndexMap[primaryItemType]].Item.gameObject;
                    } else if (itemType is DualWieldItemType) {
                        return m_DualWieldInventory[itemIndex].Item.gameObject;
                    }
                    return m_SecondaryInventory[itemIndex].Item.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the name of the item.
        /// </summary>
        /// <param name="primaryItem">Is the item a primary item?</param>
        /// <returns>The name of the item.</returns>
        private string SharedMethod_ItemName(bool primaryItem)
        {
            var item = GetCurrentItem(primaryItem ? typeof(PrimaryItemType) : typeof(SecondaryItemType));
            if (item != null) {
                return item.ItemName;
            }
            return "No Item";
        }

        /// <summary>
        /// If an item is equipped then unequip it. If an item is unequipped or equal to the unequipped type then equip the previous item.
        /// </summary>
        public void ToggleEquippedItem()
        {
            // If an item is already being equipped or unequipped then piggy back off of the resulting callback.
            if (m_UnequipIndex != -1 && (m_UnequippedItemType == null || m_PrimaryInventory[m_UnequipIndex].ItemType != m_UnequippedItemType)) {
                m_EquipIndex = m_ToggleEquippedIndex;
                m_DualWieldEquipIndex = m_ToggleDualWieldEquippedIndex;
                return;
            } else if (m_EquipIndex != -1 && (m_UnequippedItemType == null || m_PrimaryInventory[m_EquipIndex].ItemType != m_UnequippedItemType)) {
                m_UnequipIndex = m_ToggleEquippedIndex;
                m_DualWieldUnequipIndex = m_ToggleDualWieldEquippedIndex;
                return;
            }

            if (m_ToggleEquippedIndex != -1 && (m_CurrentPrimaryIndex == -1 || m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType == m_UnequippedItemType)) {
                EquipItem(m_ToggleEquippedIndex);
                if (m_ToggleDualWieldEquippedIndex != -1) {
                    // If the unequip index is -1 then the item was replaced unequipped and the dual wield item was not unequipped.
                    if (m_EquipIndex == -1) {
                        EquipUnequipDualWieldItem(true, m_ToggleDualWieldEquippedIndex, true);
                    } else {
                        m_DualWieldEquipIndex = m_ToggleDualWieldEquippedIndex;
                    }
                }
            } else {
                m_ToggleEquippedIndex = m_CurrentPrimaryIndex;
                m_ToggleDualWieldEquippedIndex = m_CurrentDualWieldIndex;
                m_DualWieldUnequipIndex = m_ToggleDualWieldEquippedIndex;
                UnequipCurrentItem();
                // If the unequip index is -1 then the item was replaced unequipped and the dual wield item was not unequipped.
                if (m_UnequipIndex == -1) {
                    EquipUnequipDualWieldItem(false, m_ToggleDualWieldEquippedIndex, true);
                    m_DualWieldUnequipIndex = -1;
                }
            }
        }

        /// <summary>
        /// Equip the specified primary item.
        /// </summary>
        /// <param name="primaryItemType">The primary item type.</param>
        public void EquipItem(PrimaryItemType primaryItemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(primaryItemType, out itemIndex)) {
                return;
            }

            EquipItem(itemIndex);
        }

        /// <summary>
        /// Equips the primary item in the specified index. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        public void EquipItem(int itemIndex)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcEquipItemIndex(itemIndex);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                EquipItemLocal(itemIndex);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Equips the primary item in the specified index on the client.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        [ClientRpc]
        private void RpcEquipItemIndex(int itemIndex)
        {
            EquipItemLocal(itemIndex);
        }
#endif

        /// <summary>
        /// Equips the primary item in the specified index.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        private void EquipItemLocal(int itemIndex)
        {
            // Cannot equip the item if it hasn't been picked up yet or the current item is already equipped.
            if (itemIndex >= m_PrimaryInventory.Count || m_PrimaryInventory[itemIndex].ItemCount == 0 || m_CurrentPrimaryIndex == itemIndex) {
                return;
            }

            if (m_CurrentPrimaryIndex != -1) {
                var canDualWield = CanDualWield(m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType, m_PrimaryInventory[itemIndex].ItemType as PrimaryItemType);
                // Don't switch to the dual wielded items immediately. Let each item be on its own and then switch to both of them being equipped at the same time. For example,
                // lets say that the inventory contains a sword, shield, and fist. The sword and shield can be dual wielded. When switching items it should go:
                // sword, shield, sword shield, fist, sword, ...
                if (canDualWield) {
                    m_DualWieldSwitchIndex = m_CurrentPrimaryIndex;
                    canDualWield = false;
                } else if (!canDualWield && m_DualWieldSwitchIndex != -1) {
                    itemIndex = m_DualWieldSwitchIndex;
                    m_DualWieldSwitchIndex = -1;
                    canDualWield = true;
                }

                // The item that should be equipped can dual wield with the current item. Determine which item is the primary item, and equip the new item.
                if (canDualWield) {
                    var newPrimary = false;
                    var currentItemPrimaryItemType = m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType;
                    for (int i = 0; i < currentItemPrimaryItemType.DualWieldItems.Length; ++i) {
                        if (currentItemPrimaryItemType.DualWieldItems[i].ItemType.Equals(m_PrimaryInventory[itemIndex].ItemType)) {
                            newPrimary = currentItemPrimaryItemType.DualWieldItems[i].PrimaryName;
                            break;
                        }
                    }

                    if (newPrimary) {
                        // The current item should not be the primary item. Set the current item to the dual wield item.
                        m_CurrentDualWieldIndex = m_PrimaryDualWieldItemIndexMap[currentItemPrimaryItemType];
                        EquipUnequipItem(true, itemIndex, false);
                    } else {
                        if (m_CurrentDualWieldIndex != -1) {
                            // The current dual wield index may equal the equip index if moving between items quickly.
                            EquipUnequipDualWieldItem(false, m_CurrentDualWieldIndex, m_CurrentDualWieldIndex == m_DualWieldEquipIndex);
                        }

                        // DualWieldUnequip may still be -1 if the items are quickly being changed and the animator doesn't need to wait for a callback.
                        if (m_DualWieldUnequipIndex != -1) {
                            m_DualWieldEquipIndex = itemIndex;
                        } else {
                            EquipUnequipDualWieldItem(true, itemIndex, false);
                        }
                    }
                } else {
                    // If an item is equipped then it first needs to be unequipped before the new item can ben equipped. Run AnimateEquipUnequip to unequip the item
                    // and within OnItemUnequipped start equipping the new item.
                    if (m_CurrentDualWieldIndex != -1) {
                        EquipUnequipDualWieldItem(false, m_CurrentDualWieldIndex, m_CurrentDualWieldIndex == m_DualWieldEquipIndex);
                    }
                    // The current primary index may equal the equip index if moving between items quickly.
                    EquipUnequipItem(false, m_CurrentPrimaryIndex, m_CurrentPrimaryIndex == m_EquipIndex);

                    // Unequip may still be -1 if the items are quickly being changed and the animator doesn't need to wait for a callback.
                    if (m_UnequipIndex != -1) {
                        m_EquipIndex = itemIndex;
                    } else {
                        EquipUnequipItem(true, itemIndex, false);
                    }

                    // Equip the dual wield item if it exists.
                    var dualWieldItemType = DualWieldItemForPrimaryItem(m_PrimaryInventory[itemIndex].ItemType);
                    if (dualWieldItemType != null) {
                        if (!m_ItemIndexMap.TryGetValue(dualWieldItemType, out itemIndex)) {
                            return;
                        }
                        if (m_UnequipIndex != -1 || m_DualWieldUnequipIndex != -1) {
                            m_DualWieldEquipIndex = itemIndex;
                        } else {
                            EquipUnequipDualWieldItem(true, itemIndex, false);
                        }
                    }
                }
            } else {
                EquipUnequipItem(true, itemIndex, false);

                // Equip the dual wield item if it exists.
                var dualWieldItemType = DualWieldItemForPrimaryItem(m_PrimaryInventory[itemIndex].ItemType);
                if (dualWieldItemType != null) {
                    if (!m_ItemIndexMap.TryGetValue(dualWieldItemType, out itemIndex)) {
                        return;
                    }
                    EquipUnequipDualWieldItem(true, itemIndex, false);
                }
            }
        }

        /// <summary>
        /// Unequip the current item. Call the corresponding server or client method.
        /// </summary>
        public void UnequipCurrentItem()
        {
            // Cannot unequip an item if there isn't an item or the animator is currently aiming.
            if (m_CurrentPrimaryIndex == -1) {
                return;
            }

#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcUnequipCurrentItem();
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                UnequipCurrentItemLocal();
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Unequip the current item on the client.
        /// </summary>
        [ClientRpc]
        private void RpcUnequipCurrentItem()
        {
            UnequipCurrentItemLocal();
        }
#endif

        /// <summary>
        /// Unequip the current item.
        /// </summary>
        private void UnequipCurrentItemLocal()
        {
            if (m_CurrentDualWieldIndex != -1) {
                EquipUnequipDualWieldItem(false, m_CurrentDualWieldIndex, false);
            }
            if (m_UnequippedItemType != null) {
                m_EquipIndex = m_ItemIndexMap[m_UnequippedItemType];
            }
            EquipUnequipItem(false, m_CurrentPrimaryIndex, false);
        }

        /// <summary>
        /// Drops the specified item.
        /// </summary>
        /// <param name="itemID">The ID of the item to drop.</param>
        public void DropItem(int itemID)
        {
            ItemType itemType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemType)) {
                return;
            }

            if (m_DropItems) {
                int itemIndex;
                if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                    return;
                }

                ItemInstance itemInstance;
                if (itemType is PrimaryItemType) {
                    itemInstance = m_PrimaryInventory[itemIndex];
                } else if (itemType is SecondaryItemType) {
                    itemInstance = m_SecondaryInventory[itemIndex];
                } else { // DualWieldItemType.
                    itemInstance = m_DualWieldInventory[itemIndex];
                }

                DropItem(itemInstance);
            }
            RemoveItem(itemType, true, false);
        }

        /// <summary>
        /// Drops the specified item.
        /// </summary>
        /// <param name="itemInstance">The instance of the item to drop.</param>
        private void DropItem(ItemInstance itemInstance)
        {
            if (itemInstance.Item.ItemPickup == null) {
                return;
            }
            var itemPickup = ObjectPool.Spawn(itemInstance.Item.ItemPickup, itemInstance.Item.transform.position,
                                                itemInstance.Item.transform.rotation, m_DroppedItemsParent).GetComponent<ItemPickup>();
            var itemAmountList = itemPickup.ItemList;
            ConsumableItemInstance consumableItemInstance = null;
            ItemType itemType = null;
            if (itemInstance.ItemType is DualWieldItemType) {
                // The DualWieldItemType uses the PrimaryItemType's ConsumableItem.
                ItemType primaryItemType;
                if (m_DualWieldPrimaryItemMap.TryGetValue(itemInstance.ItemType, out primaryItemType)) {
                    int primaryItemIndex;
                    itemType = primaryItemType;
                    if (m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                        consumableItemInstance = m_PrimaryInventory[primaryItemIndex].ConsumableItem;
                    }
                }
            } else {
                itemType = itemInstance.ItemType;
                consumableItemInstance = itemInstance.ConsumableItem;
            }

            for (int i = 0; i < itemAmountList.Count; ++i) {
                ObjectPool.Return(itemAmountList[i]);
            }
            itemAmountList.Clear();

            // The main ItemType, ConsumableItemType, and ItemExtension ConsumableItemType can be dropped.
            var itemAmount = ObjectPool.Get<ItemAmount>();
            itemAmount.Initialize(itemType, 1);
            itemAmountList.Add(itemAmount);
            if (consumableItemInstance != null) {
                for (int i = 0; i < consumableItemInstance.ParentItemType.Count; ++i) {
                    var parentItemType = consumableItemInstance.ParentItemType[i];
                    itemAmount = ObjectPool.Get<ItemAmount>();
                    if (parentItemType is DualWieldItemType) {
                        itemAmount.Initialize(consumableItemInstance.ConsumableItemType, GetItemCount(itemInstance.ItemType, true));
                    } else {
                        // Do not add the unloaded counts multiple times.
                        itemAmount.Initialize(consumableItemInstance.ConsumableItemType, GetItemCount(itemInstance.ItemType, true) + (i == 0 ? GetItemCount(itemInstance.ItemType, false) : 0));
                    }
                    itemAmountList.Add(itemAmount);
                }
            }
            // Drop any ItemExtension ConsumableItemTypes as well.
            if (itemInstance.Item.ItemExtensions != null) {
                for (int i = 0; i < itemInstance.Item.ItemExtensions.Length; ++i) {
                    itemAmount = ObjectPool.Get<ItemAmount>();
                    itemAmount.Initialize(itemInstance.Item.ItemExtensions[i].ConsumableItemType, GetItemCount(itemInstance.Item.ItemExtensions[i].ConsumableItemType, true));
                    itemAmountList.Add(itemAmount);
                }
            }
        }

        /// <summary>
        /// Remove the count of all items from the inventory. This will happen will the character dies.
        /// </summary>
        public void RemoveAllItems()
        {
            RemoveAllItems(true);
        }

        /// <summary>
        /// Remove the count of all items from the inventory. This will happen will the character dies.
        /// </summary>
        /// <param name="canDropItems">Can the character drop the items?</param>
        public void RemoveAllItems(bool canDropItems)
        {
            // Drop any of the remaining items.
            if (m_DropItems && canDropItems) {
                // Drop any primary items.
                for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                    var itemInstance = m_PrimaryInventory[i];
                    if (itemInstance.Item.ItemPickup != null && m_PrimaryInventory[i].ItemCount > 0) {
                        DropItem(itemInstance);
                    }
                }

                // Drop any secondary items.
                for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                    var itemInstance = m_SecondaryInventory[i];
                    if (itemInstance.Item.ItemPickup != null && GetItemCount(itemInstance.ItemType) > 0) {
                        DropItem(itemInstance);
                    }
                }

                // Drop any dual wield items.
                for (int i = 0; i < m_DualWieldInventory.Count; ++i) {
                    var itemInstance = m_DualWieldInventory[i];
                    ItemType primaryItemType;
                    if (!m_DualWieldPrimaryItemMap.TryGetValue(itemInstance.ItemType, out primaryItemType)) {
                        continue;
                    }
                    int primaryItemIndex;
                    if (!m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                        continue;
                    }
                    if (itemInstance.Item.ItemPickup != null && m_PrimaryInventory[primaryItemIndex].ItemCount > 1) {
                        DropItem(itemInstance);
                    }
                }
            }

            // Remove the items from the inventory.
            for (int i = 0; i < m_DualWieldInventory.Count; ++i) {
                ItemType primaryItemType;
                if (!m_DualWieldPrimaryItemMap.TryGetValue(m_DualWieldInventory[i].ItemType, out primaryItemType)) {
                    continue;
                }

                int primaryItemIndex;
                if (!m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                    continue;
                }

                if (m_PrimaryInventory[primaryItemIndex].ItemCount > 1) {
                    RemoveItem(m_DualWieldInventory[i].ItemType, true, true);
                }
            }
            for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                if (m_PrimaryInventory[i].ItemCount > 0) {
                    RemoveItem(m_PrimaryInventory[i].ItemType, true, true);
                }
            }
            for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                if (m_SecondaryInventory[i].ItemCount > 0) {
                    RemoveItem(m_SecondaryInventory[i].ItemType, true, true);
                }
            }
            CurrentPrimaryIndex = CurrentDualWieldIndex = m_CurrentSecondaryIndex = -1;
            m_DualWieldSwitchIndex = m_ToggleEquippedIndex = m_ToggleDualWieldEquippedIndex = -1;
            m_EquipIndex = m_UnequipIndex = m_DualWieldEquipIndex = m_DualWieldUnequipIndex = -1;
        }

        /// <summary>
        /// Remove the count of the specified item. This in effect removes it from the inventory.
        /// </summary>
        /// <param name="itemType">The item type to remove.</param>
        /// <param name="removeConsumableItems">Should the consumable items also be removed?</param>
        /// <param name="immediateRemoval">Should the item be removed immediately? This only applies to the PrimaryItemType. If false the animation will remove the item.</param>
        public void RemoveItem(ItemType itemType, bool removeConsumableItems, bool immediateRemoval)
        {
            int itemIndex;
            if (m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                if (itemIndex == m_DualWieldSwitchIndex) {
                    m_DualWieldSwitchIndex = -1;
                }
                if (itemType is PrimaryItemType) {
                    m_PrimaryInventory[itemIndex].ItemCount -= 1;
                    if (m_PrimaryInventory[itemIndex].ItemCount == 0) {
                        EquipUnequipItem(false, itemIndex, immediateRemoval);

                        // Remove all of the consumable items.
                        if (removeConsumableItems) {
                            var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                            if (consumableItem != null) {
                                consumableItem.ResetPrimaryLoadedCount();
                            }
                        }
                    } else {
                        var dualWieldItemType = m_PrimaryDualWieldItemMap[itemType];
                        var dualWieldIndex = m_ItemIndexMap[dualWieldItemType];
                        EquipUnequipDualWieldItem(false, dualWieldIndex, immediateRemoval);

                        var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                        if (consumableItem != null) {
                            consumableItem.UnloadedCount += consumableItem.GetPrimaryItemCount(dualWieldItemType);
                            consumableItem.SetPrimaryItemCount(dualWieldItemType, 0, true);
                            EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_PrimaryInventory[itemIndex].Item, true, false);
                        }
                    }
                } else if (itemType is SecondaryItemType) { // SecondaryItemType
                    m_SecondaryInventory[itemIndex].ItemCount = 0;
                } else { // DualWieldItemType
                    ItemType primaryItemType;
                    if (!m_DualWieldPrimaryItemMap.TryGetValue(itemType, out primaryItemType)) {
                        return;
                    }

                    int primaryItemIndex;
                    if (!m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                        return;
                    }

                    m_PrimaryInventory[primaryItemIndex].ItemCount -= 1;
                    if (removeConsumableItems) {
                        var consumableItem = m_PrimaryInventory[primaryItemIndex].ConsumableItem;
                        if (consumableItem != null) {
                            consumableItem.SetPrimaryItemCount(itemType, 0, true);
                        }
                    }

                    m_DualWieldInventory[itemIndex].SetActive(false);
                    CurrentDualWieldIndex = -1;
                }
            }
        }

        /// <summary>
        /// Equip or unequip an item. Smoothly animate the transition unless immediate is called (in which case just activate/deactivate the GameObject).
        /// The animator knows what type of weapon is active by the ItemID parameter.
        /// </summary>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="itemIndex">The index of the item in the inventory.</param>
        /// <param name="immediate">Should the item be equipped immediately and not animated?</param>
        private void EquipUnequipItem(bool equip, int itemIndex, bool immediate)
        {
            // Immediately equip or unequip the item if the item cannot be interacted with. The roll ability will 
            // prevent animations since the character cannot play the equip/unequip animations while rolling.
            if (m_CanInteractItem != null && !m_CanInteractItem.Invoke()) {
                immediate = true;
            }

            if (equip) {
                if (immediate) {
                    m_PrimaryInventory[itemIndex].SetActive(true);
                    m_EquipIndex = -1;
                } else {
                    m_EquipIndex = itemIndex;
                }
                CurrentPrimaryIndex = itemIndex;
                if (m_UnequippedItemType != null && m_ItemIndexMap[m_UnequippedItemType] != itemIndex) {
                    m_ToggleEquippedIndex = itemIndex;
                } 
            } else {
                if (immediate) {
                    if (itemIndex != -1) {
                        m_PrimaryInventory[itemIndex].SetActive(false);
                        CurrentPrimaryIndex = -1;
                    }
                } else {
                    m_UnequipIndex = itemIndex;
                }
            }
            if (!immediate) {
                if (equip) {
                    EventHandler.ExecuteEvent(m_PrimaryInventory[itemIndex].GameObject, "OnInventoryItemEquipping");
                } else {
                    EventHandler.ExecuteEvent(m_PrimaryInventory[itemIndex].GameObject, "OnInventoryItemUnequipping");
                }
                EventHandler.ExecuteEvent(m_GameObject, "OnUpdateAnimator");
            } else {
                EventHandler.ExecuteEvent(m_GameObject, "OnUpdateAnimator");
            }
        }

        /// <summary>
        /// Equip or unequip a dual wield item. Smoothly animate the transition unless immediate is called (in which case just activate/deactivate the GameObject).
        /// The animator knows what type of weapon is active by the ItemID parameter.
        /// </summary>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="dualWieldItemIndex">The index of the item in the inventory.</param>
        /// <param name="immediate">Should the item be equipped immediately and not animated?</param>
        private void EquipUnequipDualWieldItem(bool equip, int dualWieldItemIndex, bool immediate)
        {
            // Immediately equip or unequip the item if the item cannot be interacted with. The roll ability will 
            // prevent animations since the character cannot play the equip/unequip animations while rolling.
            if (m_CanInteractItem != null && !m_CanInteractItem.Invoke()) {
                immediate = true;
            }
            if (equip) {
                if (m_DualWieldUnequipIndex == dualWieldItemIndex) {
                    m_DualWieldUnequipIndex = -1;
                }
                if (immediate) {
                    m_DualWieldInventory[dualWieldItemIndex].SetActive(true);
                    m_DualWieldEquipIndex = -1;
                } else {
                    m_DualWieldEquipIndex = dualWieldItemIndex;
                }
                CurrentDualWieldIndex = dualWieldItemIndex;
                m_ToggleDualWieldEquippedIndex = dualWieldItemIndex;
            } else {
                if (m_DualWieldEquipIndex == dualWieldItemIndex) {
                    m_DualWieldEquipIndex = -1;
                }
                if (immediate) {
                    if (dualWieldItemIndex != -1) {
                        m_DualWieldInventory[dualWieldItemIndex].SetActive(false);
                        m_DualWieldUnequipIndex = -1;
                        CurrentDualWieldIndex = -1;
                    }
                } else {
                    m_DualWieldUnequipIndex = dualWieldItemIndex;
                }
            }
            if (!immediate) {
                if (equip) {
                    EventHandler.ExecuteEvent(m_DualWieldInventory[dualWieldItemIndex].GameObject, "OnInventoryItemEquipping");
                } else {
                    EventHandler.ExecuteEvent(m_DualWieldInventory[dualWieldItemIndex].GameObject, "OnInventoryItemUnequipping");
                }
                EventHandler.ExecuteEvent(m_GameObject, "OnUpdateAnimator");
            } else {
                EventHandler.ExecuteEvent(m_GameObject, "OnUpdateAnimator");
            }
        }

        /// <summary>
        /// Is the inventory currently switching items?
        /// </summary>
        /// <returns>True if the inventory is switching items.</returns>
        public bool SharedMethod_IsSwitchingItems()
        {
            return IsSwitchingItems;
        }

        /// <summary>
        /// The character has died. Remove all of the items from the inventory.
        /// </summary>
        private void OnDeath()
        {
            EventHandler.UnregisterEvent(gameObject, "OnDeath", OnDeath);
            EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);

            RemoveAllItems();
        }

        /// <summary>
        /// The character has respawned. Load the default loadout.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
            EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);

            LoadDefaultLoadout();
        }

        /// <summary>
        /// The Animator says an item has been equipped so the GameObject should now activate.
        /// </summary>
        private void OnItemEquipped()
        {
            if (m_EquipIndex != -1) {
                if (m_UnequipIndex != -1) {
                    m_PrimaryInventory[m_UnequipIndex].SetActive(false);
                    // Notify the item that it has been unequipped.
                    EventHandler.ExecuteEvent(m_PrimaryInventory[m_UnequipIndex].GameObject, "OnInventoryItemUnequipped");
                    m_UnequipIndex = -1;
                }
                m_PrimaryInventory[m_EquipIndex].SetActive(true);
                // Let the item know that it has been equipped.
                EventHandler.ExecuteEvent(m_PrimaryInventory[m_EquipIndex].GameObject, "OnInventoryItemEquipped");
                m_EquipIndex = -1;
            }
            if (m_DualWieldEquipIndex != -1) {
                if (m_DualWieldUnequipIndex != -1) {
                    m_DualWieldInventory[m_DualWieldUnequipIndex].SetActive(false);
                    // Notify the item that it has been unequipped.
                    EventHandler.ExecuteEvent(m_DualWieldInventory[m_DualWieldUnequipIndex].GameObject, "OnInventoryItemUnequipped");
                    m_DualWieldUnequipIndex = -1;
                }
                m_DualWieldInventory[m_DualWieldEquipIndex].SetActive(true);
                CurrentDualWieldIndex = m_DualWieldEquipIndex;
                // Let the item know that it has been equipped.
                EventHandler.ExecuteEvent(m_DualWieldInventory[m_DualWieldEquipIndex].GameObject, "OnInventoryItemEquipped");
                m_DualWieldEquipIndex = -1;
            }

            EventHandler.ExecuteEvent(m_GameObject, "OnUpdateAnimator");
        }

        /// <summary>
        /// The Animator says an item has been unequipped so the GameObject should now deactivate. If an item is waiting to be equipped then start the equip animation.
        /// </summary>
        private void OnItemUnequipped()
        {
            if (m_UnequipIndex != -1) {
                m_PrimaryInventory[m_UnequipIndex].SetActive(false);
                CurrentPrimaryIndex = -1;
                // Notify the item that it has been unequipped.
                EventHandler.ExecuteEvent(m_PrimaryInventory[m_UnequipIndex].GameObject, "OnInventoryItemUnequipped");
                m_UnequipIndex = -1;
            }

            if (m_DualWieldUnequipIndex != -1) {
                m_DualWieldInventory[m_DualWieldUnequipIndex].SetActive(false);
                CurrentDualWieldIndex = -1;
                // Notify the item that it has been unequipped.
                EventHandler.ExecuteEvent(m_DualWieldInventory[m_DualWieldUnequipIndex].GameObject, "OnInventoryItemUnequipped");
                m_DualWieldUnequipIndex = -1;
            }

            EventHandler.ExecuteEvent(m_GameObject, "OnUpdateAnimator");

            if (m_EquipIndex != -1) {
                EquipUnequipItem(true, m_EquipIndex, false);
            }
            if (m_DualWieldEquipIndex != -1) {
                EquipUnequipDualWieldItem(true, m_DualWieldEquipIndex, false);
            }
        }
    }
}