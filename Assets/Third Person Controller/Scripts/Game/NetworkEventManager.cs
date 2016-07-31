#if !(UNITY_4_6 || UNITY_4_7 || UNITY_5_0)
using UnityEngine;
using UnityEngine.Networking;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Subclass of NetworkManager. Will send events when a network callback occurs.
    /// </summary>
    public class NetworkEventManager : NetworkManager
    {
        /// <summary>
        /// A list of integers for the messages being sent to all of the clients.
        /// </summary>
        public static class NetworkMessages
        {
            public static short MSG_PICKUP_ITEM = 3001;
            public static short MSG_ACTIVE_ITEM = 3002;
        }

        /// <summary>
        /// Called on the server when a client adds a new player.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        /// <param name="playerControllerId">Id of the new player.</param>
        public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
        { 	 
            base.OnServerAddPlayer(conn, playerControllerId);

            // The new connection isn't active yet so check against 0 connections to determine if the server just started and objects can spawn.
            if (NetworkServer.connections.Count == 0) {
                EventHandler.ExecuteEvent("OnNetworkAddFirstPlayer");
            }
        }

        /// <summary>
        /// Called on the server when a client is ready.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerReady(NetworkConnection conn)
        {
            base.OnServerReady(conn);

            EventHandler.ExecuteEvent("OnNetworkServerReady", conn);
        }

        /// <summary>
        /// Called when a client is stopped.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();

            EventHandler.ExecuteEvent("OnNetworkStopClient");
        }
    }
}
#endif