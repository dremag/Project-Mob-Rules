using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Extends the health component by allowing the character to take fall damage. The amount of damage is specified by a curve.
    /// </summary>
    public class CharacterHealth : Health
    {
        [Tooltip("Should fall damage be applied?")]
        [SerializeField] protected bool m_UseFallDamage;
        [Tooltip("The minimum height that the character has to fall in order for any damage to be applied")]
        [SerializeField] protected float m_MinFallDamageHeight;
        [Tooltip("A fall greater than this value is an instant death")]
        [SerializeField] protected float m_DeathHeight;
        [Tooltip("The amount of damage to apply when the player falls by the minimum fall damage height")]
        [SerializeField] protected float m_MinFallDamage;
        [Tooltip("The amount of damage to apply when the player falls just less than the death height")]
        [SerializeField] protected float m_MaxFallDamage;
        [Tooltip("A curve specifying the amount of damage to apply if the character falls between the min and max fall damage values")]
        [SerializeField] protected AnimationCurve m_DamageCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Prevent a body part from applying a multiplier if the distance is greater than the specified threshold")]
        [SerializeField] protected float m_DamageDistanceThreshold = 0.15f;
        [Tooltip("Optionally specify any sounds to play when damaged")]
        [SerializeField] protected AudioClip[] m_DamageSound;

        // Component references
        private AudioSource m_AudioSource;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_AudioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Register for any events that the health should be aware of.
        /// </summary>
        private void OnEnable()
        {
            if (m_UseFallDamage) {
                EventHandler.RegisterEvent<float>(m_GameObject, "OnControllerLand", OnCharacterLand);
            }
        }

        /// <summary>
        /// Unregister for any events that the health was registered for.
        /// </summary>
        private void OnDisable()
        {
            if (m_UseFallDamage) {
                EventHandler.UnregisterEvent<float>(m_GameObject, "OnControllerLand", OnCharacterLand);
            }
        }

        /// <summary>
        /// Override Damage to add an extra damage amount depending on the bone hit.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-exposive force will be used.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        public override void Damage(float amount, Vector3 position, Vector3 force, float radius, GameObject attacker, GameObject hitGameObject)
        {
            // Optionally play any damage sounds
            if (m_AudioSource != null && m_DamageSound.Length > 0) {
                m_AudioSource.clip = m_DamageSound[Random.Range(0, m_DamageSound.Length)];
                m_AudioSource.Play();
            }

            base.Damage(amount, position, force, radius, attacker, hitGameObject);
        }

        /// <summary>
        /// The character has landed after falling a spcified amount. Determine if any damage should be taken.
        /// </summary>
        /// <param name="fallHeight"></param>
        private void OnCharacterLand(float fallHeight)
        {
            // Return immediately if the fall height is less than the minimum height that the character has to fall in order to start taking damage.
            if (fallHeight < m_MinFallDamageHeight) {
                return;
            }

            var damageAmount = 0f;
            // The fall was too great, specify an infinite amount of damage.
            if (fallHeight >= m_DeathHeight) {
                damageAmount = Mathf.Infinity;
            } else {
                // The fall was somewhere in between the min and max fall height. Use the damage curve to determine how much damage to apply.
                var normalizedHeight = (fallHeight - m_MinFallDamageHeight) / (m_DeathHeight - m_MinFallDamageHeight);
                var damageAmountMultiplier = m_DamageCurve.Evaluate(normalizedHeight);
                damageAmount = m_MinFallDamage + damageAmountMultiplier * (m_MaxFallDamage - m_MinFallDamage);
            }

            // Apply the damage.
            Damage(damageAmount, m_Transform.position, Vector3.zero);
        }
    }
}