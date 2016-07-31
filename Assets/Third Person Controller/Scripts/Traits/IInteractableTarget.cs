namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an object that can be interacted with (such as a platform or door).
    /// </summary>
    public interface IInteractableTarget
    {
        /// <summary>
        /// Is the target ready for an interaction?
        /// </summary>
        /// <returns>True if the target can be interacted with.</returns>
        bool IsInteractionReady();
        
        /// <summary>
        /// Interact with the target.
        /// </summary>
        void Interact();
    }
}