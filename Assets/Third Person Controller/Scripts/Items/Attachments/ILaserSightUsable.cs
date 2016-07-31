using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an item that can use the laser sight.
    /// </summary>
    public interface ILaserSightUseable
    {
        /// <summary>
        /// Toggles the activate state of the laser sight.
        /// </summary>
        void ToggleLaserSight();

        /// <summary>
        /// Activates or deactivates the laser sight when the item is aimed.
        /// </summary>
        /// <param name="activate">Should the laser sight be active?</param>
        void ActivateLaserSightOnAim(bool activate);
    }
}