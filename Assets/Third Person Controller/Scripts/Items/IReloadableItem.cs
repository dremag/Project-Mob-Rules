namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an item that can be reloaded.
    /// </summary>
    public interface IReloadableItem
    {
        /// <summary>
        /// Starts to reload the item.
        /// </summary>
        void StartReload();

        /// <summary>
        /// Is the item reloading?
        /// </summary>
        /// <returns>True if the item is reloading.</returns>
        bool IsReloading();

        /// <summary>
        /// Tries to stop the item reload.
        /// </summary>
        void TryStopReload();
    }
}