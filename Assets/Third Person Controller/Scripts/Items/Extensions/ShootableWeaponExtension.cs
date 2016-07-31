using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any weapon extension that can shopt.
    /// </summary>
    public class ShootableWeaponExtension : WeaponExtension
    {
        /// <summary>
        /// The mode in which the weapon fires multiple shots.
        /// </summary>
        public enum FireMode {
            SemiAuto, // Fire discrete shots, don't continue to fire until the player fires again
            FullAuto, // Keep firing until the ammo runs out or the player stops firing 
            Burst // Keep firing until the burst rate is zero
        }

        [Tooltip("The point at which to do the actual firing")]
        [SerializeField] protected Transform m_FirePoint;
        [Tooltip("The mode in which the weapon fires multiple shots")]
        [SerializeField] protected FireMode m_FireMode = FireMode.SemiAuto;
        [Tooltip("The number of shots per second")]
        [SerializeField] protected float m_FireRate = 2;
        [Tooltip("If using the Burst FireMode, specifies the number of bursts the weapon can fire")]
        [SerializeField] protected int m_BurstRate = 5;
        [Tooltip("The distance in which the bullet can hit. This is only used for weapons that do not have a projectile")]
        [SerializeField] protected float m_FireRange = float.MaxValue;
        [Tooltip("The number of rounds to fire in a single shot")]
        [SerializeField] protected int m_FireCount = 1;
        [Tooltip("Should the weapon wait to fire until the used event?")]
        [SerializeField] protected bool m_FireOnUsedEvent;
        [Tooltip("Should the weapon wait for the OnAnimatorItemEndUse to return to a non-use state?")]
        [SerializeField] protected bool m_WaitForEndUseEvent;
        [Tooltip("The amount of recoil to apply when the weapon is fired")]
        [SerializeField] protected float m_RecoilAmount = 0.1f;
        [Tooltip("The random spread of the bullets once they are fired")]
        [SerializeField] protected float m_Spread = 0.01f;
        [Tooltip("The speed at which to regenerate the ammo")]
        [SerializeField] protected float m_RegenerateRate;
        [Tooltip("The amount of ammo to add each regenerative tick. RegenerativeRate must be greater than 0")]
        [SerializeField] protected int m_RegenerateAmount;

        [Tooltip("Optionally specify a shell that should be spawned when the weapon is fired")]
        [SerializeField] protected GameObject m_Shell;
        [Tooltip("If Shell is specified, the location is the position and rotation that the shell spawns at")]
        [SerializeField] protected Transform m_ShellLocation;
        [Tooltip("If Shell is specified, the force is the amount of force applied to the shell when it spawns")]
        [SerializeField] protected Vector3 m_ShellForce;
        [Tooltip("If Shell is specified, the force is the amount of torque applied to the shell when it spawns")]
        [SerializeField] protected Vector3 m_ShellTorque;

        [Tooltip("Optionally specify a muzzle flash that should appear when the weapon is fired")]
        [SerializeField] protected GameObject m_MuzzleFlash;
        [Tooltip("If Muzzle Flash is specified, the location is the position and rotation that the muzzle flash spawns at")]
        [SerializeField] protected Transform m_MuzzleFlashLocation;

        [Tooltip("Optionally specify any smoke that should appear when the weapon is fired")]
        [SerializeField] protected GameObject m_Smoke;
        [Tooltip("If Smoke is specified, the location is the position and rotation that the smoke spawns at")]
        [SerializeField] protected Transform m_SmokeLocation;

        [Tooltip("Optionally specify any particles that should play when the weapon is fired")]
        [SerializeField] protected ParticleSystem m_FireParticles;

        [Tooltip("Optionally specify a sound that should randomly play when the weapon is fired")]
        [SerializeField] protected AudioClip[] m_FireSound;
        [Tooltip("If Fire Sound is specified, play the sound after the specified delay")]
        [SerializeField] protected float m_FireSoundDelay;
        [Tooltip("Optionally specify a sound that should randomly play when the weapon is fired and out of ammo")]
        [SerializeField] protected AudioClip[] m_EmptyFireSound;

        [Tooltip("Optionally specify a projectile that the weapon should use")]
        [SerializeField] protected GameObject m_Projectile;

        [Tooltip("A LayerMask of the layers that can be hit when fired at. This only applies to weapons that do not have a projectile")]
        [SerializeField] protected LayerMask m_HitscanImpactLayers = -1;
        [Tooltip("Optionally specify an event to send when the hitscan fire hits a target")]
        [SerializeField] protected string m_HitscanDamageEvent;
        [Tooltip("The amount of damage done to the object hit. This only applies to weapons that do not have a projectile")]
        [SerializeField] protected float m_HitscanDamageAmount = 10;
        [Tooltip("How much force is applied to the object hit. This only applies to weapons that do not have a projectile")]
        [SerializeField] protected float m_HitscanImpactForce = 5;
        [Tooltip("Optionally specify a default decal that should be applied to the object hit. This only applies to weapons that do not have a projectile and " +
                 "only be used if no per-object decal is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultHitscanDecal;
        [Tooltip("Optionally specify any default dust that should appear on top of the object hit. This only applies to weapons that do not have a projectile and " +
                  "be used if no per-object dust is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultHitscanDust;
        [Tooltip("Optionally specify any default sparks that should appear on top of the object hit. This only applies to weapons that do not have a projectile and " +
                 "only be used if no per-object spark is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultHitscanSpark;
        [Tooltip("Optionally specify a default impact sound that should play at the point of the object hit. This only applies to weapons that do not have a projectile and " +
                 "only be used if no per-object sound is setup in the ObjectManager")]
        [SerializeField] protected AudioClip m_DefaultHitscanImpactSound;
        [Tooltip("Optionally specify a tracer that should should appear when the hitscan weapon is fired")]
        [SerializeField] protected GameObject m_Tracer;
        [Tooltip("If Tracer is specified, the location is the position and rotation that the tracer spawns at")]
        [SerializeField] protected Transform m_TracerLocation;

        // SharedFields
        private SharedMethod<bool> m_IndependentLook = null;
        private SharedMethod<bool> m_AIAgent = null;
        private SharedProperty<float> m_Recoil = null;
        private SharedMethod<bool, Vector3> m_TargetLookPosition = null;

        // Internal variables
        private float m_ShootDelay;
        private float m_LastShootTime;
        private int m_CurrentBurst;
        private bool m_IsFiring;
        private bool m_CanFire;
        private ScheduledEvent m_FireEvent;
        private ScheduledEvent m_EmptyClipEvent;
        private bool m_ReadyToFire;
        private float m_RegenerateDelay;
        private ScheduledEvent m_RegenerateEvent;
        private RaycastHit m_RaycastHit;
        private Collider[] m_CharacterColliders;

        // Component references
        private AudioSource m_AudioSource;
        private Collider[] m_Colliders;
        private Transform m_CharacterTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_AudioSource = GetComponent<AudioSource>();
            m_Colliders = GetComponents<Collider>();

            m_IsFiring = false;
            m_CanFire = true;
            m_CurrentBurst = m_BurstRate;
            m_ShootDelay = 1.0f / m_FireRate;
            m_LastShootTime = -m_ShootDelay;

            if (m_RegenerateRate > 0) {
                m_RegenerateDelay = 1.0f / m_RegenerateRate;
            }
        }

        /// <summary>
        /// Register for any events that the weapon should be aware of. Ensure the flashlight and laser sight are disabled.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            EventHandler.RegisterEvent(m_GameObject, "OnItemEmptyClip", EmptyClip);
            EventHandler.RegisterEvent(m_GameObject, "OnItemAddFireEffects", AddFireEffects);
            EventHandler.RegisterEvent<Transform, Vector3, Vector3>(m_GameObject, "OnItemAddHitscanEffects", AddHitscanEffects);

            // Init may not have been called from the inventory so the character GameObject may not have been assigned yet.
            if (m_Character != null && m_WaitForEndUseEvent) {
                EventHandler.RegisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
            }
        }

        /// <summary>
        /// Perform any cleanup when the item is disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            // The weapon may still be firing if the character died while the animation is playing.
            StopFiring(false);

            EventHandler.UnregisterEvent(m_GameObject, "OnItemEmptyClip", EmptyClip);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemAddFireEffects", AddFireEffects);
            EventHandler.UnregisterEvent<Transform, Vector3, Vector3>(m_GameObject, "OnItemAddHitscanEffects", AddHitscanEffects);

            // The character may be null if Init hasn't been called yet.
            if (m_Character != null) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
                if (m_WaitForEndUseEvent) {
                    EventHandler.UnregisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
                }
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

            // The inventory may be initialized before the item so cache the components in the Init method.
            m_CharacterTransform = inventory.transform;

            // When the projectile is instantiated it should ignore all of the character's colliders.
            if (m_Projectile != null) {
                m_CharacterColliders = m_Character.GetComponents<Collider>();
            }

            // Register for character events within Init to allow the weapon to recieve the callback even when the weapon isn't active. This allows
            // the character to pickup ammo for a weapon before picking up the weapon and having the ammo already loaded.
            EventHandler.RegisterEvent<Item, bool, bool>(m_Character, "OnInventoryConsumableItemCountChange", ConsumableItemCountChange);
            EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);

            // Register for character events if the GameObject is active. OnEnable normally registers for these callbacks but in this case OnEnable has already occurred.
            if (gameObject.activeSelf) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
                if (m_WaitForEndUseEvent) {
                    EventHandler.RegisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
                }
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
        /// Try to fire the weapon. The weapon may not be able to be fired for various reasons such as it already firing or it being out of ammo.
        /// </summary>
        public override bool TryUse()
        {
            if (!m_IsFiring && m_CanFire && m_LastShootTime + m_ShootDelay < Time.time) {
                if (m_Inventory.GetItemCount(m_ConsumableItemType) > 0) {
                    m_IsFiring = true;
                    // Prevent the weapon from continuously firing if it not a fully automatic. AI agents do not have to follow this because they don't manually stop firing.
                    m_CanFire = m_FireMode == FireMode.FullAuto || m_AIAgent.Invoke();

                    // Do not regenerate any more ammo after starting to fire.
                    if (m_RegenerateEvent != null) {
                        Scheduler.Cancel(ref m_RegenerateEvent);
                    }

                    // Wait until the used event is called before firing.
                    if (!m_FireOnUsedEvent) {
                        DoFire();
                    }

                    return true;
                } else {
                    EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
#if ENABLE_MULTIPLAYER
                    m_NetworkMonitor.ExecuteItemEvent(m_ConsumableItemType.ID, "OnItemEmptyClip");
#else
                    EmptyClip();
#endif
                }
            } else {
                EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
            }
            return false;
        }

        /// <summary>
        /// The weapon no longer has any ammo. Play the empty clip audio.
        /// </summary>
        private void EmptyClip()
        {
            if (m_EmptyFireSound != null && m_EmptyFireSound.Length > 0 && !m_AudioSource.isPlaying) {
                m_AudioSource.clip = m_EmptyFireSound[Random.Range(0, m_EmptyFireSound.Length)];
                m_AudioSource.Play();
            }

            // Keep repeating until the stop used event is called.
            if ((m_FireMode != FireMode.SemiAuto && m_Inventory.GetItemCount(m_ConsumableItemType, true) == 0) || (m_FireMode == FireMode.Burst && m_CurrentBurst == 0)) {
                m_EmptyClipEvent = Scheduler.Schedule(m_ShootDelay, EmptyClip);
            }
        }

        /// <summary>
        /// Can the item be used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        public override bool CanUse()
        {
            return m_CanFire;
        }

        /// <summary>
        /// Is the weapon currently being fired?
        /// </summary>
        /// <returns>True if the weapon is firing.</returns>
        public override bool InUse()
        {
            return m_IsFiring; 
        }

        /// <summary>
        /// The weapon has been used. Stop using the item if the fire type is instant or out of ammo. Do a fire if the item should first be charged.
        /// </summary>
        public override void Used()
        {
            if (m_FireEvent == null || m_Inventory.GetItemCount(m_ConsumableItemType) == 0) {
                if (m_FireOnUsedEvent && m_Inventory.GetItemCount(m_ConsumableItemType) > 0) {
                    DoFire();
                }
                if (!m_WaitForEndUseEvent) {
                    StopFiring(true);
                }
            }
        }

        /// <summary>
        /// Stop the weapon from firing.
        /// </summary>
        public override void TryStopUse()
        {
            m_CanFire = true;
            if (!InUse()) {
                // The weapon is no longer being fired. Cancel the empty clip event.
                if (m_EmptyClipEvent != null) {
                    Scheduler.Cancel(ref m_EmptyClipEvent);
                }
                return;
            }

            // Don't stop firing if waiting for an end use event or the weapon will stop firing by itself.
            if (m_WaitForEndUseEvent || m_FireMode == FireMode.SemiAuto) {
                return;
            }

            StopFiring(false);
        }

        /// <summary>
        /// Do the actual fire.
        /// </summary>
        private void DoFire()
        {
            EventHandler.ExecuteEvent(m_Character, "OnItemUse");
            m_LastShootTime = Time.time;

            // Fire as many projectiles or hitscan bullets as the fire count specifies.
            for (int i = 0; i < m_FireCount; ++i) {
                Fire();
            }

            // Decrement the amount of ammo from the inventory.
            m_Inventory.UseItem(m_ConsumableItemType, 1);

            // Add any fire effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
            m_NetworkMonitor.ExecuteItemEvent(m_ConsumableItemType.ID, "OnItemAddFireEffects");
#else
            AddFireEffects();
#endif

            // Determine the the weapon should be fired again.
            var repeatFire = m_Inventory.GetItemCount(m_ConsumableItemType) > 0 && m_FireMode != FireMode.SemiAuto;
            if (m_FireMode == FireMode.Burst) {
                m_CurrentBurst--;
                if (m_CurrentBurst == 0) {
                    repeatFire = false;
                }
            }
            
            // Fire again if necessary.
            if (repeatFire) {
                m_FireEvent = Scheduler.Schedule(m_ShootDelay, DoFire);
            }
        }

        /// <summary>
        /// A weapon has been fired, add any fire effects.
        /// </summary>
        private void AddFireEffects()
        {
            // Apply a recoil.
            if (m_Recoil != null) {
                m_Recoil.Set(m_RecoilAmount);
            }

            // Spawn a shell.
            if (m_Shell) {
                var shell = ObjectPool.Instantiate(m_Shell, m_ShellLocation.position, m_ShellLocation.rotation);
                shell.GetComponent<Rigidbody>().AddRelativeForce(m_ShellForce);
                shell.GetComponent<Rigidbody>().AddRelativeTorque(m_ShellTorque);
            }

            // Spawn a muzzle flash.
            if (m_MuzzleFlash) {
                // Choose a random z rotation angle.
                var eulerAngles = m_MuzzleFlashLocation.eulerAngles;
                eulerAngles.z = Random.Range(0, 360);
                var muzzleFlashObject = ObjectPool.Instantiate(m_MuzzleFlash, m_MuzzleFlashLocation.position, Quaternion.Euler(eulerAngles), m_Transform);
                MuzzleFlash muzzleFlash;
                if ((muzzleFlash = muzzleFlashObject.GetComponent<MuzzleFlash>()) != null) {
                    muzzleFlash.Show();
                }
            }

            // Spawn any smoke.
            if (m_Smoke) {
                ObjectPool.Instantiate(m_Smoke, m_SmokeLocation.position, m_SmokeLocation.rotation);
            }

            // Play any particle effects.
            if (m_FireParticles) {
                m_FireParticles.Play();
            }

            // Play a firing sound.
            if (m_FireSound != null && m_FireSound.Length > 0) {
                m_AudioSource.clip = m_FireSound[Random.Range(0, m_FireSound.Length)];
                if (m_FireSoundDelay > 0) {
                    m_AudioSource.PlayDelayed(m_FireSoundDelay);
                } else {
                    m_AudioSource.Play();
                }
            }
        }

        /// <summary>
        /// Virtual method to actually fire the weapon. Will fire a projectile if it exists, otherwise a hitscan fire will be used.
        /// </summary>
        protected virtual void Fire()
        {
            // Fire a projectile if it exists, otherwise fire a raycast.
            if (m_Projectile) {
                ProjectileFire();
            } else {
                HitscanFire();
            }
        }

        /// <summary>
        /// Spawns a projectile which will move in the firing direction.
        /// </summary>
        private void ProjectileFire()
        {
            var rotation = Quaternion.LookRotation(FireDirection());
            var projectileGameObject = ObjectPool.Spawn(m_Projectile, m_FirePoint.position, rotation * m_Projectile.transform.rotation);
            var projectile = projectileGameObject.GetComponent<Projectile>();
            projectile.Initialize(rotation * Vector3.forward, Vector3.zero, m_Character);
            var projectileCollider = projectile.GetComponent<Collider>();

            // Ignore all of the colliders to prevent the projectile from detonating as a result of the character. 
            if (projectileCollider != null) {
                for (int i = 0; i < m_Colliders.Length; ++i) {
                    LayerManager.IgnoreCollision(projectileCollider, m_Colliders[i]);
                }
                for (int i = 0; i < m_CharacterColliders.Length; ++i) {
                    LayerManager.IgnoreCollision(projectileCollider, m_CharacterColliders[i]);
                }
            }
        }

        /// <summary>
        /// Fire by casting a ray in the specified direction. If an object was hit apply the damage, apply a force, add a decal, etc.
        /// </summary>
        private void HitscanFire()
        {
            // Cast a ray between the fire point and the position found by the crosshairs camera ray.
            var fireDirection = FireDirection();
            if (Physics.Raycast(m_FirePoint.position, fireDirection, out m_RaycastHit, m_FireRange, m_HitscanImpactLayers.value)) {

                // Execute any custom events.
                if (!string.IsNullOrEmpty(m_HitscanDamageEvent)) {
                    EventHandler.ExecuteEvent(m_RaycastHit.collider.gameObject, m_HitscanDamageEvent, m_HitscanDamageAmount, m_RaycastHit.point, m_RaycastHit.normal * -m_HitscanImpactForce);
                }

                // If the Health component exists it will apply a force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
                Health hitHealth;
                if ((hitHealth = m_RaycastHit.transform.GetComponentInParent<Health>()) != null) {
                    hitHealth.Damage(m_HitscanDamageAmount, m_RaycastHit.point, fireDirection * m_HitscanImpactForce, m_Character, m_RaycastHit.transform.gameObject);
                } else if (m_HitscanImpactForce > 0 && m_RaycastHit.rigidbody != null && !m_RaycastHit.rigidbody.isKinematic) {
                    m_RaycastHit.rigidbody.AddForceAtPosition(fireDirection * m_HitscanImpactForce, m_RaycastHit.point);
                }

                // Add any hitscan effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
                m_NetworkMonitor.ExecuteItemEvent(m_ConsumableItemType.ID, "OnItemAddHitscanEffects", m_RaycastHit.transform.gameObject, m_RaycastHit.point, m_RaycastHit.normal);
#else
                AddHitscanEffects(m_RaycastHit.transform, m_RaycastHit.point, m_RaycastHit.normal);
#endif
            }
        }

        /// <summary>
        /// The hitscan has hit an object, add any hitscan effects.
        /// </summary>
        /// <param name="hitTransform">The transform that was hit.</param>
        /// <param name="hitPoint">The hit point.</param>
        /// <param name="hitNormal">The normal of the transform at the hit point.</param>
        private void AddHitscanEffects(Transform hitTransform, Vector3 hitPoint, Vector3 hitNormal)
        {
            var hitRotation = Quaternion.LookRotation(hitNormal);

            // Don't add the decal if the hit layer doesnt allow decals (such as other characters).
            if (DecalManager.CanAddDecal(hitTransform.gameObject.layer)) {
                var decal = ObjectManager.ObjectForItem(hitTransform.tag, m_ConsumableItemType, ObjectManager.ObjectCategory.Decal) as GameObject;
                if (decal == null) {
                    decal = m_DefaultHitscanDecal;
                }
                if (decal != null) {
                    // Apply a decal to the hit point. Offset the decal by a small amount so it doesn't interset with the object hit.
                    DecalManager.Add(decal, hitPoint + hitNormal * 0.02f, decal.transform.rotation * hitRotation, hitTransform);
                }
            }

            // Spawn a dust particle effect at the hit point.
            var dust = ObjectManager.ObjectForItem(hitTransform.tag, m_ConsumableItemType, ObjectManager.ObjectCategory.Dust) as GameObject;
            if (dust == null) {
                dust = m_DefaultHitscanDust;
            }
            if (dust != null) {
                ObjectPool.Instantiate(dust, hitPoint, dust.transform.rotation * hitRotation);
            }

            // Spawn a spark particle effect at the hit point.
            var spark = ObjectManager.ObjectForItem(hitTransform.tag, m_ConsumableItemType, ObjectManager.ObjectCategory.Spark) as GameObject;
            if (spark == null) {
                spark = m_DefaultHitscanSpark;
            }
            if (spark != null) {
                ObjectPool.Instantiate(spark, hitPoint, spark.transform.rotation * hitRotation);
            }

            // Play a sound at the hit point.
            var audioClip = ObjectManager.ObjectForItem(hitTransform.tag, m_ConsumableItemType, ObjectManager.ObjectCategory.Audio) as AudioClip;
            if (audioClip == null) {
                audioClip = m_DefaultHitscanImpactSound;
            }
            if (audioClip != null) {
                AudioSource.PlayClipAtPoint(audioClip, hitPoint);
            }

            // Spawn a tracer which moves to the hit point.
            if (m_Tracer) {
                var tracerObject = ObjectPool.Instantiate(m_Tracer, m_TracerLocation.position, m_TracerLocation.rotation);
                var tracer = tracerObject.GetComponent<Tracer>();
                if (tracer != null) {
                    tracer.SetHitPoint(hitPoint);
                }
            }
        }

        /// <summary>
        /// Determines the direction to fire based on the camera's look position and a random spread.
        /// </summary>
        /// <returns>The direction to fire.</returns>
        private Vector3 FireDirection()
        {
            Vector3 direction;
            // If TargetLookPosition is null then use the forward direction. It may be null if the AI agent doesn't have the AIAgent component attached.
            if (m_TargetLookPosition == null) {
                direction = m_CharacterTransform.forward;
            } else {
                direction = (m_TargetLookPosition.Invoke(true) - m_FirePoint.position).normalized;
            }

            // Add a random spread.
            if (m_Spread > 0) {
                var variance = Quaternion.AngleAxis(Random.Range(0, 360), direction) * Vector3.up * Random.Range(0, m_Spread);
                direction += variance;
            }

            return direction;
        }

        /// <summary>
        /// Stop firing the weapon. Cancel and reset the affected variables.
        /// </summary>
        /// <param name="success">Did the item successfully fire?</param>
        private void StopFiring(bool success)
        {
            // Can't stop firing if the weapon isn't firing to begin with.
            if (!m_IsFiring) {
                return;
            }

            if (m_FireEvent != null) {
                Scheduler.Cancel(ref m_FireEvent);
            }

            // Keep repeating the empty clip method until the stop used event is called.
            if (m_EmptyClipEvent == null && success &&
                ((m_FireMode != FireMode.SemiAuto && m_Inventory.GetItemCount(m_ConsumableItemType, true) == 0) || (m_FireMode == FireMode.Burst && m_CurrentBurst == 0))) {
                m_EmptyClipEvent = Scheduler.Schedule(m_ShootDelay, EmptyClip);
            }

            if (m_FireParticles) {
                m_FireParticles.Stop();
            }

            if (m_RegenerateDelay != 0) {
                m_RegenerateEvent = Scheduler.Schedule(m_RegenerateDelay, RegenerateAmmo);
            }

            m_IsFiring = false;
            m_CurrentBurst = m_BurstRate;
            m_UseStates.NextState();
            
            EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
        }

        /// <summary>
        /// Ends the weapon use.
        /// </summary>
        private void EndUse()
        {
            StopFiring(true);
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        protected override void OnAim(bool aim)
        {
            base.OnAim(aim);

            if (!aim) {
                if (InUse()) {
                    StopFiring(false);
                }
                // When the character is no longer aiming reset the animation states so they will begin fresh.
                m_UseStates.ResetNextState();
            }
        }

        /// <summary>
        /// Event notification when the inventory has changed its primary item. Stop firing if the weapon is currently firing.
        /// </summary>
        /// <param name="item">The new item. Can be null.</param>
        private void PrimaryItemChange(Item item)
        {
            StopFiring(false);
        }

        /// <summary>
        /// Event notification that the Inventory has added or removed consumable items. Determine if the weapon needs to be reloaded.
        /// </summary>
        /// <param name="item">The item whose consumable ammo has changed.</param>
        /// <param name="added">True if the consumable items were added.</param>
        /// <param name="immediateChange">True if the consumable item count should be changed immediately. This will be true when the player initially spawns.</param>
        private void ConsumableItemCountChange(Item item, bool added, bool immediateChange)
        {
            // DualWield items require the GameObject to be active. The DualWielded item may never be picked up so it doesn't need to take ammo from the PrimaryItem.
            if (added && (item.ItemType.Equals(m_ParentItem.ItemType) || 
                            (gameObject.activeSelf && m_ParentItem.ItemType is DualWieldItemType && (m_ParentItem.ItemType as DualWieldItemType).PrimaryItem.Equals(item.ItemType)))) {
                ReloadComplete(); // There are no reload animations with ShootableWeaponExtension.
            }
        }

        /// <summary>
        /// Does the actual reload.
        /// </summary>
        private void ReloadComplete()
        {
            m_Inventory.ReloadItem(m_ConsumableItemType, m_Inventory.GetItemCount(m_ConsumableItemType, false));

            if (!m_IsFiring) {
                EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
            }
        }

        /// <summary>
        /// The weapon should regenerate the ammo.
        /// </summary>
        private void RegenerateAmmo()
        {
            // Only regenerate if there is unloaded ammo.
            if (m_Inventory.GetItemCount(m_ConsumableItemType, false) > 0) {
                var amount = m_RegenerateAmount;
#if UNITY_EDITOR
                if (amount == 0) {
                    Debug.LogWarning("Warning: RegenerateAmount must be a positive number.");
                    amount = 1;
                }
#endif
                m_Inventory.ReloadItem(m_ConsumableItemType, amount);

                // Keep regenerating.
                m_RegenerateEvent = Scheduler.Schedule(m_RegenerateDelay, RegenerateAmmo);
            }
        }
    }
}