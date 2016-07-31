using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Allows multiple hitboxes to be used on a single MeleeWeapon or MeleeWeaponExtension.
    /// </summary>
    public class MeleeWeaponHitbox : MonoBehaviour
    {
        // Internal fields
        private IHitboxItem m_Owner;

        // Component references
        private Rigidbody m_Rigidbody;

        // Exposed properties
        public IHitboxItem Owner { set { m_Owner = value; } }

        /// <summary>
        /// Initialize the default values and cache the component references.
        /// </summary>
        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody == null) {
                Debug.LogError("Error: The MeleeWeaponHitbox must have a Rigidbody attached.");
            }
            m_Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        /// <summary>
        /// Activates or deactivates the hitbox.
        /// </summary>
        /// <param name="active">Should the hitbox be activated?</param>
        public void SetActive(bool active)
        {
            m_Rigidbody.isKinematic = !active;
        }

        /// <summary>
        /// The collider has collided with another object. Notify the ower.
        /// </summary>
        /// <param name="collision">The object that collided with the hitbox.</param>
        private void OnCollisionEnter(Collision collision)
        {
            m_Owner.HitboxCollision(collision);
        }
    }
}