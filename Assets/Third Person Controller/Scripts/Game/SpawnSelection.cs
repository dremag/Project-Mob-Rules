using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Singleton object which randomly selects a new spawn location out of the spawn location list.
    /// </summary>
    public class SpawnSelection : MonoBehaviour
    {
        // Static variables
        private static SpawnSelection s_Instance;
        private static SpawnSelection Instance
        {
            get
            {
#if UNITY_EDITOR
                if (s_Instance == null) {
                    Debug.LogError("Error: No spawn points setup. Please create a new GameObject with the SpawnSelection component. " +
                                   "Any child GameObjects indicate where the character should spawn.");
                }
#endif
                return s_Instance;
            }
        }

        [Tooltip("The locations that the object can spawn")]
        [SerializeField] protected Transform[] m_SpawnLocations;

        /// <summary>
        /// Assign the static variables.
        /// </summary>
        protected virtual void Awake()
        {
            s_Instance = this;
        }

        /// <summary>
        /// Static method for returning a random spawn location from the spawn location list.
        /// </summary>
        /// <returns>The Transform of a random spawn location.</returns>
        public static Transform GetSpawnLocation()
        {
            return Instance.GetSpawnLocationInternal();
        }

        /// <summary>
        /// Internal method for returning a random spawn location from the spawn location list.
        /// </summary>
        /// <returns>The Transform of a random spawn location.</returns>
        protected virtual Transform GetSpawnLocationInternal()
        {
            if (m_SpawnLocations.Length == 0) {
                Debug.LogError("SpawnSelection Error: No spawn positions have been added.");
                return null;
            }

            return m_SpawnLocations[Random.Range(0, m_SpawnLocations.Length)];
        }

        /// <summary>
        /// Static method for returning all of the spawn locations.
        /// </summary>
        /// <returns>All of the spawn locations.</returns>
        public static Transform[] GetAllSpawnLocations()
        {
            return Instance.GetAllSpawnLocationsInternal();
        }

        /// <summary>
        /// Internal method for returning all of the spawn locations.
        /// </summary>
        /// <returns>All of the spawn locations.</returns>
        protected virtual Transform[] GetAllSpawnLocationsInternal()
        {
            return m_SpawnLocations;
        }
    }
}