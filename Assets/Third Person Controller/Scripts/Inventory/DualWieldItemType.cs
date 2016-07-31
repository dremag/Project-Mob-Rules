using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A dual wield item is any item that can be equipped in conjunction with another primary item of the same type. Examples include dual pistols. If a pistol and a shield can be equipped
    /// at the same time, they each will have their own primary type. The shield does not need to use the dual wield item type because two shields are not equipped.
    /// </summary>
    public class DualWieldItemType : ItemType
    {
        [Tooltip("The PrimaryItem that this ItemType can be equipped with")]
        [SerializeField] protected PrimaryItemType m_PrimaryItem;

        // Exposed properties
        public PrimaryItemType PrimaryItem { get { return m_PrimaryItem; } }

        /// <summary>
        /// Overridden from the ItemType class, will return int.MaxValue to specify that there is not a limit to the number of primary items that can be held. In most cases
        /// there will only be one primary item for a particular type, but you may have more than one if the primary item is a grenade.
        /// </summary>
        /// <returns>Returns int.MaxValue specifying that primary items have no capacity limit.</returns>
        public override int GetCapacity()
        {
            return m_PrimaryItem.GetCapacity();
        }
    }
}