using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A primary item is any item that can be equipped by the character. This includes a standard weapon such as a pistol or shotgun but it can also include throwable items
    /// (such as a grenade) or melee weapons (such as a knife).
    /// </summary>
    public class PrimaryItemType : ItemType
    {
        /// <summary>
        /// Primary items can use consumable items. Setup a link between the primary item and what consumable item it can use.
        /// </summary>
        [System.Serializable]
        public class UseableConsumableItem
        {
            [Tooltip("The consumable item that can be used by the primary item")]
            [SerializeField] protected ConsumableItemType m_ItemType;
            [Tooltip("The number of consumable item units the primary item can hold. For example, if the primary item is a pistol then it may be able to hold up to 12 bullets. In this example " +
                     "the capacity is 12")]
            [SerializeField] protected int m_Capacity = int.MaxValue;

            // Exposed properties
            public ConsumableItemType ItemType { get { return m_ItemType; } set { m_ItemType = value; } }
            public int Capacity { get { return m_Capacity; } set { m_Capacity = value; } }

            /// <summary>
            /// Constructor for the ItemTypeBuilder.
            /// </summary>
            public UseableConsumableItem(ConsumableItemType itemType, int capacity)
            {
                m_ItemType = itemType;
                m_Capacity = capacity;
            }
        }

        /// <summary>
        /// Primary items can dual wield other items. Setup a link between the primary item and the items that can be dual wielded with the current item.
        /// </summary>
        [System.Serializable]
        public class DualWieldItem
        {
            [Tooltip("The ItemType of the dual wielded item")]
            [SerializeField] protected ItemType m_ItemType;
            [Tooltip("Should the name of this item come first when dual wielding with another item? This is used to reduce the number of substates needed. " +
                     "For example, if the primary item is a Pistol and the dual wielded item is a Shield, the name will be \"Pistol Shield\". The name will still be " +
                     "\"Pistol Shield\" even if the primary item is a Shield and the dual wielded item is a Pistol")]
            [SerializeField] protected bool m_PrimaryName;

            // Exposed properties
            public ItemType ItemType { get { return m_ItemType; } }
            public bool PrimaryName { get { return m_PrimaryName; } }
        }

        [Tooltip("The consumable item that can be used by the primary item")]
        [SerializeField] protected UseableConsumableItem m_ConsumableItem;
        [Tooltip("Any other consumable items that can be used by the primary item")]
        [SerializeField] protected UseableConsumableItem[] m_AdditionalConsumableItems;
        [Tooltip("The items that can dual wield with the current item")]
        [SerializeField] protected DualWieldItem[] m_DualWieldItems;

        // Exposed properties
        public UseableConsumableItem ConsumableItem { get { return m_ConsumableItem; } set { m_ConsumableItem = value; } }
        public UseableConsumableItem[] AdditionalConsumableItems { get { return m_AdditionalConsumableItems; } set { m_AdditionalConsumableItems = value; } }
        public DualWieldItem[] DualWieldItems { get { return m_DualWieldItems; } }

        /// <summary>
        /// Overridden from the ItemType class, will return int.MaxValue to specify that there is not a limit to the number of primary items that can be held. In most cases
        /// there will only be one primary item for a particular type, but you may have more than one if the primary item is a grenade.
        /// </summary>
        /// <returns>Returns int.MaxValue specifying that primary items have no capacity limit.</returns>
        public override int GetCapacity()
        {
            return int.MaxValue;
        }
    }
}