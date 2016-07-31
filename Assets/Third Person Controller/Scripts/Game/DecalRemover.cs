using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Remove a decal from parent Transform when the parent GameObject is disabled.
    /// </summary>
    public class DecalRemover : MonoBehaviour
    {
        // Internal variables
        private bool m_CanRemove;

        // Component references
        private Renderer m_Renderer;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
        }

        /// <summary>
        /// The decal can be removed as soon as it is enabled.
        /// </summary>
        private void OnEnable()
        {
            m_CanRemove = true;
        }

        /// <summary>
        /// Schedule a decal removal if it hasn't already been removed. This is done within OnDisable to remove the decal when the parent GameObject is
        /// deactivated. If that GameObject respawns the decal shouldn't still be on the object.
        /// </summary>
        private void OnDisable()
        {
            if (m_CanRemove) {
                m_CanRemove = false;
                Scheduler.Schedule(0.01f, RemoveDecal);
            }
        }

        /// <summary>
        /// Remove the decal from the DecalManager.
        /// </summary>
        private void RemoveDecal()
        {
            DecalManager.Remove(m_Renderer);
        }
    }
}