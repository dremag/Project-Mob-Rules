using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The Water object acts as a trigger which will start or stop the Swim ability.
    /// </summary>
    public class Water : MonoBehaviour
    {
        [Tooltip("Splash particles that should be played if the character jumps into the water")]
        [SerializeField] protected ParticleSystem m_EntrySplash;
        [Tooltip("Play the entry splash if the y velocity is greater than this value")]
        [SerializeField] protected float m_EntrySplashVerticalVelocity = 2;

        /// <summary>
        /// An object has entered the trigger. Determine if it is a character with the Swim ability.
        /// </summary>
        /// <param name="other">The potential character.</param>
        private void OnTriggerEnter(Collider other)
        {
            var controller = other.GetComponent<RigidbodyCharacterController>();
            if (controller != null) {
                var swimAbility = controller.GetComponent<Abilities.Swim>();
                if (swimAbility != null) {
                    // Play a splash particle if the character jumps into the water.
                    if (m_EntrySplash != null && controller.Velocity.y < -Mathf.Abs(m_EntrySplashVerticalVelocity)) {
                        m_EntrySplash.transform.position = other.transform.position;
                        m_EntrySplash.Play();
                    }
                    controller.TryStartAbility(swimAbility);
                }
            }
        }

        /// <summary>
        /// An object has left the trigger. Stop the swim ability if the leaving object is a character.
        /// </summary>
        /// <param name="other">The potential character.</param>
        private void OnTriggerExit(Collider other)
        {
            var controller = other.GetComponent<RigidbodyCharacterController>();
            if (controller != null) {
                var swimAbility = controller.GetComponent<Abilities.Swim>();
                if (swimAbility != null) {
                    controller.TryStopAbility(swimAbility);
                }
            }
        }
    }
}