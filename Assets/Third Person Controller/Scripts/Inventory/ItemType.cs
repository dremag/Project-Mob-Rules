using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// An Item is the base class of any object can be picked up by the character. The ItemType binds an Item to an object type that the Inventory can use to update 
    /// properties on that item such as if the item is equipped or how many bullets are left. 
    /// </summary>
    public abstract class ItemType : ScriptableObject
    {
        [Tooltip("The unique ID of the ItemType")]
        [SerializeField] protected int m_ID = -1;

        // Exposed properties
        public int ID { get { return m_ID; } set { m_ID = value; } }

        /// <summary>
        /// Returns the maximum capacity of an item. In most cases this will return the max int, but in some cases you want to limit the capacity (such as only allowing a max of
        /// 5 grenades to be carried at one time if the grenade is a PrimaryItemType. Note that this does not specify a maximum value on the number of consumable items a particular
        /// primary item can hold. That is defined within PrimaryItemType.IncludedConsumableItem.
        /// </summary>
        /// <returns>The maximum capacity of an item.</returns>
        public abstract int GetCapacity();
    }
}