namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an item that can be used (fired, swung, thrown, etc).
    /// </summary>
    public interface IUseableItem
    {
        /// <summary>
        /// Try to perform the use. Depending on the item this may not always succeed. For example, if the user is trying to shoot a weapon that was shot a half
        /// second ago cannot be used if the weapon can only be fired once per second.
        /// <returns>True if the item was used.</returns>
        /// </summary>
        bool TryUse();

        /// <summary>
        /// Can the item be used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        bool CanUse();

        /// <summary>
        /// Is the item currently in use?
        /// </summary>
        /// <returns>True if the item is in use.</returns>
        bool InUse();

        /// <summary>
        /// Stop the item from being used. This may not always succeed. For example, a melee weapon cannot be interrupted if it is already in the middle of its motion. 
        /// </summary>
        void TryStopUse();

        /// <summary>
        /// Callback for when an item has been used. An example of its use is by melee weapons - when the item is used the melee weapon should do its damage.
        /// </summary>
        void Used();
    }
}