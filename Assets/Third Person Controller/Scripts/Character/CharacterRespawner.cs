using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// When the character respawns it should respawn in the location determined by SpawnSelection.
    /// </summary>
    public class CharacterRespawner : Respawner
    {
        // Component references
        private RigidbodyCharacterController m_Controller;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Controller = GetComponent<RigidbodyCharacterController>();
        }
        
        /// <summary>
        /// The character should spawn. Override Spawn to allow the SpawnSelection component determine the location that the character should spawn.
        /// Call the corresponding server or client method.
        /// </summary>
        public override void Spawn()
        {
            var location = SpawnSelection.GetSpawnLocation();

#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcSpawn(location.position, location.rotation);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
            SpawnLocal(location.position, location.rotation);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The character should spawn on the client.
        /// </summary>
        [ClientRpc]
        private void RpcSpawn(Vector3 position, Quaternion rotation)
        {
            SpawnLocal(position, rotation);
        }
#endif

        /// <summary>
        /// The character should spawn with the specified position and rotation.
        /// </summary>
        private void SpawnLocal(Vector3 position, Quaternion rotation)
        {
            m_RespawnEvent = null;

            m_Controller.SetPosition(position);
            m_Controller.SetRotation(rotation);

            EventHandler.ExecuteEvent(m_GameObject, "OnRespawn");
        }
    }
}