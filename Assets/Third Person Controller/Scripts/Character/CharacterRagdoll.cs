using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// This component has been deprecated and replaced by the RagdollDeath ability.
    /// </summary>
    public class CharacterRagdoll : MonoBehaviour
    {
        // Internal variables
        private List<Collider> m_Colliders = new List<Collider>();
        private List<Rigidbody> m_Rigidbodies = new List<Rigidbody>();

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            Debug.LogWarning("Warning: The CharacterRagdoll component has been deprecated. Please add the Die ability to the character with a manual start and stop type.");

            // Cache the components for quick access.
            var colliders = GetComponentsInChildren<Collider>();
            var characterCollider = GetComponent<RigidbodyCharacterController>().CapsuleCollider;
            for (int i = 0; i < colliders.Length; ++i) {
                // Don't add the collider to the list if the collider is the main character's collider, a trigger, or on an item. The item can have its collider enabled.
                if (colliders[i].gameObject == gameObject || colliders[i] == characterCollider || colliders[i].isTrigger || colliders[i].GetComponent<Item>() != null) {
                    continue;
                }
                m_Colliders.Add(colliders[i]);
                var colliderRigidbody = colliders[i].GetComponent<Rigidbody>();
                if (colliderRigidbody != null) {
                    m_Rigidbodies.Add(colliderRigidbody);
                }
            }
        }

        private void Start()
        {
            EnableRagdoll(false);
        }

        private void EnableRagdoll(bool enable)
        {
            for (int i = 0; i < m_Colliders.Count; ++i) {
                m_Colliders[i].enabled = enable;
            }
            for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                m_Rigidbodies[i].useGravity = enable;
                m_Rigidbodies[i].isKinematic = !enable;
                m_Rigidbodies[i].constraints = (enable ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeAll);
                m_Rigidbodies[i].detectCollisions = true;
            }
            enabled = enable;
        }
    }
}