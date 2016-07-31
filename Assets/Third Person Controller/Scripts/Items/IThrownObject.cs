using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an object that can be thrown.
    /// </summary>
    public interface IThrownObject
    {
        /// Applies the forces to thrown the object.
        /// </summary>
        /// <param name="force">The force to apply.</param>
        /// <param name="torque">The torque to apply.</param>
        /// <param name="originator">The originator of the grenade.</param>
        void ApplyThrowForce(GameObject originator, Vector3 force, Vector3 torque);
    }
}