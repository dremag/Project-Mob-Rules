using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The PushableObject is any object which can be pushed by the character.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
#if ENABLE_MULTIPLAYER
    public class PushableObject : NetworkBehaviour
#else
    public class PushableObject : MonoBehaviour
#endif
    {
        // The amount of dampening force to apply while moving
        public float m_Dampening = 0.15f;

        // Internal variables
        private Vector3 m_PushDirection;
        private Vector3 m_BottomOffset;
        private Vector3 m_PushForce;
        private float m_Size;

        // Component references
        private Transform m_Transform;
        private Rigidbody m_Rigidbody;
        private BoxCollider m_BoxCollider;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_Rigidbody = GetComponent<Rigidbody>();
            m_BoxCollider = GetComponent<BoxCollider>();

            // The bottom offset is needed so CanPush is able to determine if the object is about to hit another object.
            m_BottomOffset = -m_Transform.up * ((m_BoxCollider.size.y / 2) - 0.01f);

            // The component will be enabled when StartPush is called.
            m_Rigidbody.isKinematic = true;
            enabled = false;
        }

        /// <summary>
        /// Can the object be pushed?
        /// </summary>
        /// <returns>True as long as another character isn't currently pushing the object.</returns>
        public bool CanStartPush()
        {
            return enabled == false;
        }

        /// <summary>
        /// The object is going to be starting to push.
        /// </summary>
        /// <param name="characterTransform">The character that is going to be pushing the object.</param>
        public void StartPush(Transform characterTransform)
        {
            // The y position should not contribute to the push direction.
            m_PushDirection = m_Transform.position - characterTransform.position;
            m_PushDirection.y = 0;
            m_PushDirection.Normalize();

            // Determine the closest point on the opposite side of the object. This point will be used to determine how large the object is so CanPush
            // is able to determine if the object is about to hit another object.
            var oppositePoint = m_BoxCollider.ClosestPointOnBounds(m_Transform.position + m_PushDirection * m_BoxCollider.size.magnitude);
            m_Size = (oppositePoint - m_Transform.position).magnitude + 0.1f; // Add a small buffer.

            // Start pushing.
            m_Rigidbody.isKinematic = false;
            enabled = true;
        }

        /// <summary>
        /// Add the push force to the Rigidbody's velocity.
        /// </summary>
        public void FixedUpdate()
        {
#if ENABLE_MULTIPLAYER
            // Don't add any forces if not on the server. The server will move the object.
            if (!isServer) {
                return;
            }
#endif
            // Add the push force.
            var velocity = m_Rigidbody.velocity;
            velocity += m_PushForce;

            // Apply the dampening force to prevent the object from forever increasing in speed.
            velocity.x /= (1 + m_Dampening);
            velocity.z /= (1 + m_Dampening);

            // Set the velocity. The push force has been applied so can be set to zero.
            m_Rigidbody.velocity = velocity;
            m_PushForce = Vector3.zero;
        }

        /// <summary>
        /// Push the object with the desired force.
        /// </summary>
        /// <param name="force">The force used to push the object.</param>
        /// <returns>Was the object pushed?</returns>
        public bool Push(Vector3 force)
        {
            if (CanPush(force)) {
                m_PushForce = force;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Can the object be pushed?
        /// </summary>
        /// <param name="force">The force used to push the object.</param>
        /// <returns>Was the object pushed?</returns>
        private bool CanPush(Vector3 force)
        {
            // The object cannot be pushed if something is blocking its path.
            return !Physics.Raycast(m_Transform.position + m_BottomOffset, m_PushDirection, m_Size, LayerManager.Mask.IgnoreInvisibleLayersPlayer);
        }

        /// <summary>
        /// The character is no longer pushing the object. Disable the component.
        /// </summary>
        public void StopPush()
        {
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.isKinematic = true;
            enabled = false;
        }
    }
}