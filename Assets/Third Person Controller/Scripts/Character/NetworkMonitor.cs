#if !(UNITY_4_6 || UNITY_4_7 || UNITY_5_0)
using UnityEngine;
using UnityEngine.Networking;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The NetworkMonitor acts as an intermediary component between the network and any object related to the character that is not spawned. These objects do not have
    /// the NetworkIdentifier component so they cannot issue standard RPC or Command calls. It also performs various other network functions such as contain the NetworkMessage identifiers.
    /// </summary>
    public class NetworkMonitor : NetworkBehaviour
    {
        // Internal variables
        private SharedMethod<int, GameObject> m_GameObjectWithItemID = null;
        private SharedProperty<Ray> m_TargetLookRay = null;
        private SharedProperty<float> m_Recoil = null;
        private SharedProperty<CameraMonitor.CameraViewMode> m_ViewMode = null;

        private Ray m_CameraTargetLookRay;
        private float m_CameraRecoil;
        private float m_SendInterval;
        private float m_LastSyncTime = -1;

        // Component references
        private NetworkTransform m_NetworkTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_NetworkTransform = GetComponent<NetworkTransform>();

            SharedManager.Register(this);
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            SharedManager.InitializeSharedFields(gameObject, this);

            // The NetworkMonitor only needs to update for the camera. There is no camera for a non-local player so disable the component if not local.
            if (!isLocalPlayer) {
                enabled = false;
            }
        }

        /// <summary>
        /// Update the look variables of the camera. This is sent to all of the other clients so the clients can accurately update the character's IK.
        /// </summary>
        private void Update()
        {
            var lookRay = m_TargetLookRay.Get();
            // The server only needs to know the look position when the character is aiming. Don't sync every frame because that would cause too much bandwidth.
            if (m_LastSyncTime + m_NetworkTransform.GetNetworkSendInterval() < Time.time) {
                CmdUpdateCameraVariables(lookRay.origin, lookRay.direction, m_Recoil.Get());
                m_LastSyncTime = Time.time;
            }
            // Update the camera variables immediately on the local machine. There is no reason to wait for the server to update these variables.
            UpdateCameraVariables(lookRay.origin, lookRay.direction, m_Recoil.Get());
        }

        /// <summary>
        /// Tell the server the camera variables of the local player.
        /// </summary>
        /// <param name="origin">The origin of the camera's look ray.</param>
        /// <param name="direction">The direction of hte camera's look ray.</param>
        /// <param name="recoil">Any recoil that should be added.</param>
        [Command(channel=(int)QosType.Unreliable)]
        private void CmdUpdateCameraVariables(Vector3 origin, Vector3 direction, float recoil)
        {
            RpcUpdateCameraVariables(origin, direction, recoil);
        }

        /// <summary>
        /// Send all of the camera variables to the clients.
        /// </summary>
        /// <param name="origin">The origin of the camera's look ray.</param>
        /// <param name="direction">The direction of hte camera's look ray.</param>
        /// <param name="recoil">Any recoil that should be added.</param>
        [ClientRpc]
        private void RpcUpdateCameraVariables(Vector3 origin, Vector3 direction, float recoil)
        {
            if (!isLocalPlayer) {
                UpdateCameraVariables(origin, direction, recoil);
            }
        }

        /// <summary>
        /// Update the camera variables for the attached character.
        /// </summary>
        /// <param name="origin">The origin of the camera's look ray.</param>
        /// <param name="direction">The direction of hte camera's look ray.</param>
        /// <param name="recoil">Any recoil that should be added.</param>
        private void UpdateCameraVariables(Vector3 origin, Vector3 direction, float recoil)
        {
            m_CameraTargetLookRay.origin = origin;
            m_CameraTargetLookRay.direction = direction;
            m_CameraRecoil = recoil;
        }

        /// <summary>
        /// Return the position that the camera is looking at. An example of where this is used include when a weapon needs to know at what point to fire. 
        /// </summary>
        /// <param name="applyRecoil">Should the target position take into account any recoil?</param>
        /// <returns>The position that the camera is looking at.</returns>
        private Vector3 SharedMethod_TargetLookPosition(bool applyRecoil)
        {
            // The SharedMethod may be called before Start is called.
            if (m_ViewMode == null) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }

            // ThirdPersonView is consistant across all of the cameras so it does not have to be sent from the client. 
            return CameraMonitor.TargetLookPosition(m_CameraTargetLookRay, (applyRecoil ? m_CameraRecoil : 0), -1, m_ViewMode.Get());
        }

        /// <summary>
        /// Return the position that the camera is looking at with a specified max distance. An example of where this is used include when a weapon needs to know at what point to fire. 
        /// </summary>
        /// <param name="applyRecoil">Should the target position take into account any recoil?</param>
        /// <param name="distance">How far away from the origin should the look position be? -1 to indicate no maximum.</param>
        /// <returns>The position that the camera is looking at.</returns>
        private Vector3 SharedMethod_TargetLookPositionMaxDistance(bool applyRecoil, float distance)
        {
            // The SharedMethod may be called before Start is called.
            if (m_ViewMode == null) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }

            // ThirdPersonView is consistant across all of the cameras so it does not have to be sent from the client. 
            return CameraMonitor.TargetLookPosition(m_CameraTargetLookRay, (applyRecoil ? m_CameraRecoil : 0), distance, m_ViewMode.Get());
        }

        /// <summary>
        /// Returns the direction that the camera is looking. An example of where this is used include when the GUI needs to determine if the crosshairs is looking at any enemies.
        /// </summary>
        /// <param name="applyRecoil">Should the target ray take into account any recoil?</param>
        /// <returns>A ray in the direction that the camera is looking.</returns>
        private Vector3 SharedMethod_TargetLookDirection(bool applyRecoil)
        {
            return CameraMonitor.TargetLookDirection(m_CameraTargetLookRay, applyRecoil ? m_CameraRecoil : 0);
        }

        /// <summary>
        /// Execute an event on all of the clients. Items will call this method because the items do not have a NetworkIdentifier and cannot call Rpc methods.
        /// </summary>
        /// <param name="itemID">The id of the item executing the event.</param>
        /// <param name="eventName">The name of the event to be executed.</param>
        public void ExecuteItemEvent(int itemID, string eventName)
        {
            RpcExecuteItemEvent(itemID, eventName);
        }

        /// <summary>
        /// Execute an event on the client. Items will call this method because the items do not have a NetworkIdentifier and cannot call Rpc methods.
        /// </summary>
        /// <param name="itemID">The id of the item executing the event.</param>
        /// <param name="eventName">The name of the event to be executed.</param>
        [ClientRpc]
        private void RpcExecuteItemEvent(int itemID, string eventName)
        {
            EventHandler.ExecuteEvent(m_GameObjectWithItemID.Invoke(itemID), eventName);
        }

        /// <summary>
        /// Execute an event on all of the clients with two arguments. Items will call this method because the items do not have a NetworkIdentifier and cannot call Rpc methods.
        /// </summary>
        /// <param name="itemID">The id of the item executing the event.</param>
        /// <param name="eventName">The name of the event to be executed.</param>
        public void ExecuteItemEvent(int itemID, string eventName, Vector3 arg1, Vector3 arg2)
        {
            RpcExecuteItemEventTwoVector3(itemID, eventName, arg1, arg2);
        }

        /// <summary>
        /// Execute an event on the client with two arguments. Items will call this method because the items do not have a NetworkIdentifier and cannot call Rpc methods.
        /// Note: A new method name was used because of a current Unity bug (697809).
        /// </summary>
        /// <param name="itemID">The id of the item executing the event.</param>
        /// <param name="eventName">The name of the event to be executed.</param>
        [ClientRpc]
        private void RpcExecuteItemEventTwoVector3(int itemID, string eventName, Vector3 arg1, Vector3 arg2)
        {
            EventHandler.ExecuteEvent(m_GameObjectWithItemID.Invoke(itemID), eventName, arg1, arg2);
        }

        /// <summary>
        /// Execute an event on all of the clients with three arguments. Items will call this method because the items do not have a NetworkIdentifier and cannot call Rpc methods.
        /// </summary>
        /// <param name="itemID">The id of the item executing the event.</param>
        /// <param name="eventName">The name of the event to be executed.</param>
        public void ExecuteItemEvent(int itemID, string eventName, GameObject arg1, Vector3 arg2, Vector3 arg3)
        {
            RpcExecuteItemEventGameObjectTwoVector3(itemID, eventName, arg1, arg2, arg3);
        }

        /// <summary>
        /// Execute an event on the client with three arguments. Items will call this method because the items do not have a NetworkIdentifier and cannot call Rpc methods.
        /// Note: A new method name was used because of a current Unity bug (697809).
        /// </summary>
        /// <param name="itemID">The id of the item executing the event.</param>
        /// <param name="eventName">The name of the event to be executed.</param>
        [ClientRpc]
        private void RpcExecuteItemEventGameObjectTwoVector3(int itemID, string eventName, GameObject arg1, Vector3 arg2, Vector3 arg3)
        {
            EventHandler.ExecuteEvent<Transform, Vector3, Vector3>(m_GameObjectWithItemID.Invoke(itemID), eventName, arg1.transform, arg2, arg3);
        }
    }
}
#endif