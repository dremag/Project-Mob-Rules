using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Plays a ParticleSystem when the object is enabled and removes itself after the ParticleSystem is done playing.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleRemover : MonoBehaviour
    {
        // Component references
        private GameObject m_GameObject;
        private ParticleSystem m_ParticleSystem;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_ParticleSystem = GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// Start the ParticleSystem and schedule itself to be destroyed after the lifetime of the ParticleSystem.
        /// </summary>
        private void OnEnable()
        {
            m_ParticleSystem.Stop(true);
            m_ParticleSystem.Play();
            Scheduler.Schedule(m_ParticleSystem.startLifetime, DestroySelf);
        }

        /// <summary>
        /// Place itself back in the ObjectPool.
        /// </summary>
        public void DestroySelf()
        {
            ObjectPool.Destroy(m_GameObject);
        }
    }
}