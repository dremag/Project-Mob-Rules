using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The PickupItem ability will play an animation when the character picks up an item. As soon as the OnAnimatorPickupItem is triggered it will add the item to the inventory.
    /// </summary>
    public class PickupItem : Ability
    {
        [Tooltip("Should the character unquip the current items before trying to pickup a new item?")]
        [SerializeField] protected bool m_UnequipCurrentItem = true;

        // Internal variables
        private HashSet<Collider> m_IgnoreTriggers = new HashSet<Collider>();
        private bool m_InTrigger;

        // SharedFields
        private SharedProperty<bool> ItemEquipped = null;
        private SharedMethod<bool> m_IsSwitchingItems = null;

        // Component references
        private Collider m_ActiveCollider;
        private ItemPickup m_ActiveItemPickup;
        private Inventory m_Inventory;
        private HeightChange m_HeightChange;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Inventory = GetComponent<Inventory>();
            m_HeightChange = GetComponent<HeightChange>();
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            if (m_ActiveItemPickup == null) {
                return false;
            }

            // Don't allow pickup unless there is room within the inventory.
            var itemList = m_ActiveItemPickup.ItemList;
            var canPickup = false;
            for (int i = 0; i < itemList.Count; ++i) {
                var inventoryCount = m_Inventory.GetItemCount(itemList[i].ItemType);
                if (inventoryCount < itemList[i].ItemType.GetCapacity() || itemList[i].ItemType.GetCapacity() == int.MaxValue) {
                    canPickup = true;
                    break;
                }
            }

            return canPickup;
        }

        /// <summary>
        /// The ability has been started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            EventHandler.RegisterEvent(gameObject, "OnAnimatorPickupItem", DoItemPickup);

            // Unequip the current item if it exists.
            if (m_UnequipCurrentItem && ItemEquipped.Get()) {
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (layer != m_AnimatorMonitor.BaseLayerIndex && layer != m_AnimatorMonitor.UpperLayerIndex && !m_AnimatorMonitor.ItemUsesAbilityLayer(this, layer)) {
                return string.Empty;
            }

            // If the height change ability is active then continue to play its base animation - the character is already low to the ground.
            if (m_HeightChange != null && m_HeightChange.IsActive && layer == m_AnimatorMonitor.BaseLayerIndex) {
                return m_HeightChange.GetDestinationState(layer);
            }

            return "Pickup Item.Pickup";
        }

        /// <summary>
        /// Add the item to the inventory.
        /// </summary>
        private void DoItemPickup()
        {
            if (!IsActive || m_ActiveItemPickup == null) {
                return;
            }
            m_ActiveItemPickup.PickUpItems(m_Inventory);
            m_ActiveCollider = null;
            m_ActiveItemPickup = null;

            // Toggle the equipped item again only if the inventory is not switching. The inventory may switch if a new primary item was picked up.
            if (m_UnequipCurrentItem && !m_IsSwitchingItems.Invoke()) {
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
            }

            // The ability is done as soon as the item has been picked up.
            StopAbility();
        }

        /// <summary>
        /// The character has entered a trigger. Check to see if the collider is one that contains an ItemPickup.
        /// </summary>
        /// <param name="other">The trigger collider.</param>
        private void OnTriggerEnter(Collider other)
        {
            // Keep a set to remember the triggers that should be ignored. This will prevent GetComponent from being called everytime the footstep trigger intersects
            // with the character's CapsuleCollider.
            if (m_IgnoreTriggers.Contains(other)) {
                return;
            }

            ItemPickup itemPickup;
            if ((itemPickup = other.GetComponent<ItemPickup>()) && !itemPickup.PickupOnTriggerEnter) {
                m_ActiveItemPickup = itemPickup;
                m_ActiveCollider = other;
            } else {
                m_IgnoreTriggers.Add(other);
            }
        }

        /// <summary>
        /// The character has exited a trigger. Check to see if the collider is currently active ItemPickup collider.
        /// </summary>
        /// <param name="other">The trigger collider.</param>
        private void OnTriggerExit(Collider other)
        {
            if (other.Equals(m_ActiveCollider)) {
                m_ActiveCollider = null;
                m_ActiveItemPickup = null;
            }
        }
    }
}