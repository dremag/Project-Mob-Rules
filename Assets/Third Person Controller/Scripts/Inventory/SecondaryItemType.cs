using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Secondary items are items that are used in the non-dominant hand. These items do not need to be equipped. This includes grenades, a health pack, a power up, etc.
    /// </summary>
    public class SecondaryItemType : ItemType
    {
        [Tooltip("The maximum number of secondary items the inventory can hold")]
        [SerializeField] protected int m_Capacity = int.MaxValue;

        // Exposed properties
        public int Capacity { set { m_Capacity = value; } }

        /// <summary>
        /// Overridden from the ItemType class, will return the maximum number of secondary items that the inventory can hold.
        /// </summary>
        /// <returns>The maximum number of secondary items the invetory can hold.</returns>
        public override int GetCapacity()
        {
            return m_Capacity;
        }
    }
}