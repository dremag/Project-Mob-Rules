using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any weapon extension that uses melee to damage the target.
    /// </summary>
    public class MeleeWeaponExtension : WeaponExtension, IHitboxItem
    {
        [Tooltip("The state to play if the melee weapon hits a fixed object")]
        [SerializeField] protected AnimatorItemCollectionData m_RecoilStates = new AnimatorItemCollectionData("Recoil", "Recoil", 0.2f, true);

        [Tooltip("The number of melee attacks per second")]
        [SerializeField] protected float m_AttackRate = 2;
        [Tooltip("The layers that the melee attack can hit")]
        [SerializeField] protected LayerMask m_AttackLayer;
        [Tooltip("Any other hitboxes that should be used when determining if the melee weapon hit a target")]
        [SerializeField] protected MeleeWeaponHitbox[] m_AttackHitboxes;
        [Tooltip("Can the attack be interrupted to move onto the next attack? The OnAnimatorItemAllowInterruption event must be added to the attack animation")]
        [SerializeField] protected bool m_CanInterruptAttack;

        [Tooltip("Optionally specify a sound that should randomly play when the weapon is attacked")]
        [SerializeField] protected AudioClip[] m_AttackSound;
        [Tooltip("If Attack Sound is specified, play the sound after the specified delay")]
        [SerializeField] protected float m_AttackSoundDelay;

        [Tooltip("Optionally specify an event to send to the object hit on damage")]
        [SerializeField] protected string m_DamageEvent;
        [Tooltip("The amount of damage done to the object hit")]
        [SerializeField] protected float m_DamageAmount = 10;
        [Tooltip("How much force is applied to the object hit")]
        [SerializeField] protected float m_ImpactForce = 5;
        [Tooltip("Optionally specify any default dust that should appear on at the location of the object hit. This is only used if no per-object dust is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultDust;
        [Tooltip("Optionally specify a default impact sound that should play at the point of the object hit. This is only used if no per-object sound is setup in the ObjectManager")]
        [SerializeField] protected AudioClip m_DefaultImpactSound;

        // SharedFields
        private SharedMethod<bool> m_IndependentLook = null;

        // Internal variables
        private float m_AttackDelay;
        private float m_LastAttackTime;
        private RaycastHit m_RaycastHit;
        private bool m_InUse;
        private bool m_AllowInterruption;
        private bool m_Recoil;

        // Component references
        private AudioSource m_AudioSource;
        private Rigidbody m_CharacterRigidbody;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_AudioSource = GetComponent<AudioSource>();

            m_AttackDelay = 1.0f / m_AttackRate;
            m_LastAttackTime = -m_AttackRate;

            // Register any hitboxes with the current MeleeWeapon.
            for (int i = 0; i < m_AttackHitboxes.Length; ++i) {
                m_AttackHitboxes[i].Owner = this;
                m_AttackHitboxes[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Register for any events that the weapon should be aware of.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            EventHandler.RegisterEvent<Transform, Vector3, Vector3>(m_GameObject, "OnItemAddMeleeEffects", AddMeleeEffects);
            EventHandler.RegisterEvent(m_GameObject, "OnItemAddAttackEffects", AddAttackEffects);
            
            // Init may not have been called from the inventory so the character GameObject may not have been assigned yet.
            if (m_Character != null) {
                EventHandler.RegisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
            }

            for (int i = 0; i < m_AttackHitboxes.Length; ++i) {
                m_AttackHitboxes[i].gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Perform any cleanup when the item extension is disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            // The weapon may still be in the process of being used if the character died while the animation is playing.
            EndUse();

            // The aim and use states should begin fresh.
            m_UseStates.ResetNextState();

            EventHandler.UnregisterEvent<Transform, Vector3, Vector3>(m_GameObject, "OnItemAddMeleeEffects", AddMeleeEffects);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemAddAttackEffects", AddAttackEffects);
            // The character may be null if Init hasn't been called yet.
            if (m_Character != null) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
                EventHandler.UnregisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
                EventHandler.RegisterEvent(m_Character, "OnAnimatorItemEndRecoil", EndRecoil);
                if (m_CanInterruptAttack) {
                    EventHandler.RegisterEvent(m_Character, "OnAnimatorItemAllowInterruption", AllowInterruption);
                }
            }

            for (int i = 0; i < m_AttackHitboxes.Length; ++i) {
                m_AttackHitboxes[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Initializes the item after it has been added to the Inventory.
        /// </summary>
        /// <param name="item">The item that this extension belongs to.</param>
        /// <param name="inventory">The parent character's inventory.</param>
        public override void Init(Item item, Inventory inventory)
        {
            base.Init(item, inventory);

            m_CharacterRigidbody = inventory.GetComponent<Rigidbody>();

            // Register for character events if the GameObject is active. OnEnable normally registers for these callbacks but in this case OnEnable has already occurred.
            if (m_GameObject.activeSelf) {
                EventHandler.RegisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
            }

            SharedManager.InitializeSharedFields(m_Character, this);
            // Independent look characters do not need to communicate with the camera. Do not initialze the SharedFields on the network to prevent non-local characters from
            // using the main camera to determine their look direction. TargetLookPosition has been implemented by the NetworkMonitor component.
#if !ENABLE_MULTIPLAYER
            if (!m_IndependentLook.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Initialize the camera SharedFields if on the network and a local player. Wait until Start because NetworkBehaviour.isLocalPlayer is not initialized before Init is called.
        /// </summary>
        private void Start()
        {
            if (!m_IndependentLook.Invoke() && m_IsLocalPlayer.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
        }
#endif

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="highPriority">Should the high priority animation be retrieved? High priority animations get tested before character movement.</param>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. A null value indicates no change.</returns>
        public override AnimatorItemStateData GetDestinationState(bool highPriority, int layer)
        {
            // Any animation called by the MeleeWeaponExtension component is a high priority animation.
            if (highPriority) {
                if (m_Recoil) {
                    var recoilState = m_RecoilStates.GetState(layer, m_Controller.Moving);
                    if (recoilState != null) {
                        return recoilState;
                    }
                }
            }
            return base.GetDestinationState(highPriority, layer);
        }

        /// <summary>
        /// Can the weapon be meleed?
        /// </summary>
        /// <returns>True if the weapon can be meleed.</returns>
        public override bool CanUse()
        {
            if (!base.CanUse()) {
                return false;
            }
            return !m_InUse || m_AllowInterruption;
        }

        /// <summary>
        /// Try to attack. The weapon may not be able to attack if the last attack was too recent.
        /// <returns>True if the item was used.</returns>
        /// </summary>
        public override bool TryUse()
        {
            // End the item use if the weapon is currently being used and can be interrupted. This will allow the next attack to play.
            if (m_InUse && m_AllowInterruption) {
                EndUse();
            }

            if (!m_InUse && m_LastAttackTime + m_AttackDelay < Time.time) {
                m_LastAttackTime = Time.time;
                m_InUse = true;
                // Add any melee starting effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
                m_NetworkMonitor.ExecuteItemEvent(m_ConsumableItemType.ID, "OnItemAddAttackEffects");
#else
                AddAttackEffects();
#endif
                EventHandler.ExecuteEvent(m_Character, "OnItemUse");
                return true;
            }
            return false;
        }

        /// <summary>
        /// The melee weapon has attacked, add any effects.
        /// </summary>
        private void AddAttackEffects()
        {
            // Play a attack sound.
            if (m_AttackSound != null && m_AttackSound.Length > 0) {
                m_AudioSource.clip = m_AttackSound[Random.Range(0, m_AttackSound.Length - 1)];
                if (m_AttackSoundDelay > 0) {
                    m_AudioSource.PlayDelayed(m_AttackSoundDelay);
                } else {
                    m_AudioSource.Play();
                }
            }
        }

        /// <summary>
        /// Is the melee weapon currently being used?
        /// </summary>
        /// <returns>True if the weapon is in use.</returns>
        public override bool InUse()
        {
            return m_InUse;
        }

        /// <summary>
        /// Attack the specified object.
        /// </summary>
        /// <param name="hitTransform">The Transform of the hit object.</param>
        /// <param name="hitPoint">The position of the collision.</param>
        /// <param name="hitNormal">The normal of the collision.</param>
        private void Attack(Transform hitTransform, Vector3 hitPoint, Vector3 hitNormal)
        {
            // Execute any custom events.
            if (!string.IsNullOrEmpty(m_DamageEvent)) {
                EventHandler.ExecuteEvent(hitTransform.gameObject, m_DamageEvent, m_DamageAmount, hitPoint, hitNormal * -m_ImpactForce);
            }
            Health hitHealth;
            Rigidbody hitRigidbody;
            // If the Health component exists it will apply a force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
            if ((hitHealth = hitTransform.GetComponentInParent<Health>()) != null) {
                hitHealth.Damage(m_DamageAmount, hitPoint, hitNormal * -m_ImpactForce, m_Character, hitTransform.gameObject);
            } else if (m_ImpactForce > 0 && (hitRigidbody = hitTransform.GetComponent<Rigidbody>()) != null && !hitRigidbody.isKinematic) {
                hitRigidbody.AddForceAtPosition(hitNormal * -m_ImpactForce, hitPoint);
            }

            // Add any melee effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
            m_NetworkMonitor.ExecuteItemEvent(m_ConsumableItemType.ID, "OnItemAddMeleeEffects", hitTransform.gameObject, hitPoint, hitNormal);
#else
            AddMeleeEffects(hitTransform, hitPoint, hitNormal);
#endif
        }

        /// <summary>
        /// Ends the weapon use.
        /// </summary>
        private void EndUse()
        {
            if (!m_InUse) {
                return;
            }
            m_InUse = false;
            m_AllowInterruption = false;
            m_UseStates.NextState();
            EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
        }

        /// <summary>
        /// The melee hit an object, add any melee effects.
        /// </summary>
        /// <param name="hitTransform">The transform that was hit.</param>
        /// <param name="hitPoint">The hit point.</param>
        /// <param name="hitNormal">The normal of the transform at the hit point.</param>
        private void AddMeleeEffects(Transform hitTransform, Vector3 hitPoint, Vector3 hitNormal)
        {
            // Spawn a dust particle effect at the hit point.
            var dust = ObjectManager.ObjectForItem(hitTransform.tag, m_ConsumableItemType, ObjectManager.ObjectCategory.Dust) as GameObject;
            if (dust == null) {
                dust = m_DefaultDust;
            }
            if (dust != null) {
                ObjectPool.Instantiate(dust, hitPoint, dust.transform.rotation * Quaternion.LookRotation(hitNormal));
            }

            // Play a sound at the hit point.
            var audioClip = ObjectManager.ObjectForItem(hitTransform.tag, m_ConsumableItemType, ObjectManager.ObjectCategory.Audio) as AudioClip;
            if (audioClip == null) {
                audioClip = m_DefaultImpactSound;
            }
            if (audioClip != null) {
                AudioSource.PlayClipAtPoint(audioClip, hitPoint);
            }
        }

        /// <summary>
        /// Allows the attack animation to be interrupted.
        /// </summary>
        private void AllowInterruption()
        {
            if (m_InUse) {
                m_AllowInterruption = true;
            }
        }

        /// <summary>
        /// The collider has collided with another object. Perform the attack if using the physics attack type.
        /// </summary>
        /// <param name="collision">The object that collided with the MeleeWeapon.</param>
        private void OnCollisionEnter(Collision collision)
        {
            if (m_InUse) {
#if ENABLE_MULTIPLAYER
                // The server will control the raycast logic.
                if (!m_IsServer.Invoke()) {
                    return;
                }
#endif
                if (Utility.InLayerMask(collision.gameObject.layer, m_AttackLayer.value)) {
                    Attack(collision.transform, collision.contacts[0].point, collision.contacts[0].normal);
                }

                // The character should play a recoil animation if the object does not have a Rigidbody, the Rigidbody is kinematic, or the Rigidbody is much heavier than the character.
                if (collision.rigidbody == null || collision.rigidbody.isKinematic || collision.rigidbody.mass > m_CharacterRigidbody.mass * 10) {
                    EndUse();
                    m_Recoil = true;
                    EventHandler.ExecuteEvent(m_Character, "OnUpdateAnimator");
                }
            }
        }

        /// <summary>
        /// The hitbox collided with another object.
        /// </summary>
        /// <param name="other">The object that collided with the hitbox.</param>
        public void HitboxCollision(Collision collision)
        {
            OnCollisionEnter(collision);
        }

        /// <summary>
        /// The recoil animation has ended.
        /// </summary>
        private void EndRecoil()
        {
            if (m_Recoil) {
                m_Recoil = false;
                EventHandler.ExecuteEvent(m_Character, "OnUpdateAnimator");
                m_UseStates.ResetNextState();
            }
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        protected override void OnAim(bool aim)
        {
            if (!aim) {
                if (InUse()) {
                    EndUse();
                }
            }
        }
    }
}