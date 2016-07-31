using UnityEngine;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Allows the player to click to move the character to a position. Will translate the NavMeshAgent desired velocity into values that the RigidbodyCharacterController can understand.
    /// </summary>
    public class PointClickControllerHandler : NavMeshAgentBridge
    {
        // Internal variables
        private Vector3 m_Velocity;
        private Quaternion m_LookRotation;

        // Component references
        private PlayerInput m_PlayerInput;
        private Camera m_Camera;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_PlayerInput = GetComponent<PlayerInput>();
            m_Camera = Utility.FindCamera();

            SharedManager.Register(this);
        }

        /// <summary>
        /// Ensure the controller is set to the correct movement type.
        /// </summary>
        private void Start()
        {
#if UNITY_EDITOR
            // The controller must use the PointClick movement type with this component.
            if (GetComponent<RigidbodyCharacterController>().Movement != RigidbodyCharacterController.MovementType.PointClick) {
                Debug.LogWarning("Warning: The PointClickControllerHandler component has been started but the RigidbodyCharacterController is not using the PointClick movement type.");
            }
#endif
        }

        /// <summary>
        /// Move towards the mouse position if the MoveInput has been pressed. Translates the NavMeshAgent desired velocity into values that the RigidbodyCharacterController can understand.
        /// </summary>
        protected override void FixedUpdate()
        {
            if (m_PlayerInput.GetButton(Constants.MoveInputName, true)) {
                RaycastHit hit;
                // Fire a raycast in the direction that the camera is looking. Move to the hit point if the raycast hits the ground.
                if (Physics.Raycast(m_Camera.ScreenPointToRay(UnityEngine.Input.mousePosition), out hit, Mathf.Infinity, LayerManager.Mask.Ground)) {
                    if (hit.transform.gameObject.layer != LayerManager.Enemy) {
                        m_NavMeshAgent.SetDestination(hit.point);
                    }
                }
            }

            base.FixedUpdate();
        }
    }
}