namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any items that can be consumed by the PrimaryItemType. These items cannot be equipped or used independently. Examples include bullets, flame from a flame thrower, 
    /// number of melee attacks, etc.
    /// </summary>
    public class ConsumableItemType : ItemType
    {
        /// <summary>
        /// Returns the maximum capacity of an item. Note that this does not specify a maximum value on the number of consumable items a particular
        /// primary item can hold. That is defined within PrimaryItemType.IncludedConsumableItem.
        /// </summary>
        /// <returns>The maximum capacity of an item.</returns>
        public override int GetCapacity()
        {
            return int.MaxValue;
        }
    }
}