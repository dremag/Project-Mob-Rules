using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Allows an object with the Invetory component to pickup items when that object enters the trigger
    /// </summary>
    public class ItemPickup : PickupObject
    {
        [Tooltip("A list of items that will be picked up by the object")]
        [SerializeField] protected List<Inventory.ItemAmount> m_ItemList;

        // Exposed properties
        public List<Inventory.ItemAmount> ItemList { get { return m_ItemList; } set { m_ItemList = value; } }

        /// <summary>
        /// Give the object the items specified in the item list.
        /// </summary>
        /// <param name="other">The object which may pick up the items if it has an Inventory.</param>
        public virtual void OnTriggerEnter(Collider other)
        {
#if ENABLE_MULTIPLAYER
            // The server should pick up the item and persist it to the clients.
            if (!isServer) {
                return;
            }
#endif
            // Do not pickup the item if it cannot be picked up on trigger enter.
            if (!PickupOnTriggerEnter) {
                return;
            }

            Inventory inventory;
            if ((inventory = other.GetComponent<Inventory>()) != null) {
                PickUpItems(inventory);
            }
        }

        public void PickUpItems(Inventory inventory)
        {
            // Do not pick up the item if it is already depleted.
            if (IsDepleted) {
                return;
            }

            for (int i = 0; i < m_ItemList.Count; ++i) {
                inventory.PickupItem(m_ItemList[i].ItemType.ID, m_ItemList[i].Amount, m_ItemList[i].Equip, false);
            }

            ObjectPickup();
        }
    }
}