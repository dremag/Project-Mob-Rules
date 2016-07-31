using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an item that can receive hitbox collisions.
    /// </summary>
    public interface IHitboxItem
    {
        /// <summary>
        /// The hitbox collided with another object.
        /// </summary>
        /// <param name="other">The object that collided with the hitbox.</param>
        void HitboxCollision(Collision other);
    }
}