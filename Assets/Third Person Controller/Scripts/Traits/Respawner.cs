using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// When the object is disabled, respawn after a specified number of seconds.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class Respawner : NetworkBehaviour
#else
    public class Respawner : MonoBehaviour
#endif
    {
        [Tooltip("Waits the specified amount of time before respawning. Set to -1 to prevent any respawning based on time")]
        [SerializeField] protected float m_RespawnTime = 1;

        // Internal variables
        private Vector3 m_StartPosition;
        private Quaternion m_StartRotation;
        protected ScheduledEvent m_RespawnEvent;

        // Component references
        private Transform m_Transform;
        protected GameObject m_GameObject;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected virtual void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;

            m_StartPosition = m_Transform.position;
            m_StartRotation = m_Transform.rotation;
        }

        /// <summary>
        /// Register for any events that the respawner should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnStartRespawn);
        }

        /// <summary>
        /// Unregister for any events that the respawner was registered for.
        /// </summary>
        protected void OnDisable()
        {
            if (m_RespawnEvent == null) {
                OnStartRespawn();
            }

            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnStartRespawn);
        }

        /// <summary>
        /// The object has been disabled. Respawn after a set amount of time.
        /// </summary>
        private void OnStartRespawn()
        {
            if (m_RespawnTime != -1) {
                m_RespawnEvent = Scheduler.Schedule(m_RespawnTime, Spawn);
            }
        }

        /// <summary>
        /// Respawn the object. Call the corresponding server or client method.
        /// </summary>
        public virtual void Spawn()
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcSpawn();
            } else {
#endif
                SpawnLocal();
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Respawn the object on the client.
        /// </summary>
        [ClientRpc]
        private void RpcSpawn()
        {
            SpawnLocal();
        }
#endif

        /// <summary>
        /// Do the spawn by setting the position and rotation back to their starting values. Enable the GameObject and let all of the listening objects know that
        /// we have been respawned.
        /// </summary>
        protected void SpawnLocal()
        {
            m_RespawnEvent = null;

            m_Transform.position = m_StartPosition;
            m_Transform.rotation = m_StartRotation;
            m_GameObject.SetActive(true);

            EventHandler.ExecuteEvent(m_GameObject, "OnRespawn");
        }
    }
}