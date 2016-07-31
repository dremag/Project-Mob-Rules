using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an object that performs the interaction on an IInteractableTarget (such as buttons or switches).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Returns the ID of the interactable object.
        /// </summary>
        /// <returns>The ID of the interactable object.</returns>
        int GetInteractableID();

        /// <summary>
        /// Can the interactor interact with the IInteractable?
        /// </summary>
        /// <returns>True if the object can be interacted with.</returns>
        bool CanInteract();

        /// <summary>
        /// Does the interactable object require the interactor to be in a target position?
        /// </summary>
        /// <returns>True if the interactor is required to be in a target position.</returns>
        bool RequiresTargetInteractorPosition();

        /// <summary>
        /// Returns the rotation that the interactor should face before interacting with the object.
        /// </summary>
        /// <returns>The target interactor rotation.</returns>
        Quaternion GetTargetInteractorRotation();

        /// <summary>
        /// The position that the interactor should move to before interacting with the object.
        /// </summary>
        /// <param name="interactorTransform">The transform of the interactor.</param>
        /// <returns>The target interactor position.</returns>
        Vector3 GetTargetInteractorPosition(Transform interactorTransform);

        /// <summary>
        /// Interact with the target.
        /// </summary>
        void Interact();
    }
}