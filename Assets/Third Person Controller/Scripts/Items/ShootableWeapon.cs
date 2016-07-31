using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any weapon that can shoot. This includes pistols, rocket launchers, bow and arrows, etc.
    /// </summary>
    public class ShootableWeapon : Weapon, IReloadableItem, IFlashlightUseable, ILaserSightUseable
    {
        /// <summary>
        /// Specifies how the weapon should be fired.
        /// </summary>
        protected enum FireType
        {
            Instant, // First the shot immediately
            ChargeAndFire, // Waits for the Used callback and then fires
            ChargeAndHold // Fires as soon as the fire button is released
        }

        /// <summary>
        /// The mode in which the weapon fires multiple shots.
        /// </summary>
        public enum FireMode {
            SemiAuto, // Fire discrete shots, don't continue to fire until the player fires again
            FullAuto, // Keep firing until the ammo runs out or the player stops firing 
            Burst // Keep firing until the burst rate is zero
        }

        [Tooltip("The state while reloading the item")]
        [SerializeField] protected AnimatorItemCollectionData m_ReloadStates = new AnimatorItemCollectionData("Reload", "Reload", 0.2f, true);

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
        [Tooltip("The number of rounds in the clip")]
        [SerializeField] protected int m_ClipSize = 50;
        [Tooltip("The number of rounds to fire in a single shot")]
        [SerializeField] protected int m_FireCount = 1;
        [Tooltip("Specifies how the weapon should be fired")]
        [SerializeField] protected FireType m_FireType = FireType.Instant;
        [Tooltip("Should the weapon wait to fire until the used event?")]
        [SerializeField] protected bool m_FireOnUsedEvent;
        [Tooltip("Should the weapon wait for the OnAnimatorItemEndUse to return to a non-use state?")]
        [SerializeField] protected bool m_WaitForEndUseEvent;
        [Tooltip("Does the weapon use a scope UI?")]
        [SerializeField] protected bool m_UseScope;
        [Tooltip("Should the weapon reload automatically once it is out of ammo?")]
        [SerializeField] protected bool m_AutoReload;
        [Tooltip("The amount of recoil to apply when the weapon is fired")]
        [SerializeField] protected float m_RecoilAmount = 0.1f;
        [Tooltip("The random spread of the bullets once they are fired")]
        [SerializeField] protected float m_Spread = 0.01f;
        [Tooltip("The speed at which to regenerate the ammo")]
        [SerializeField] protected float m_RegenerateRate;
        [Tooltip("The amount of ammo to add each regenerative tick. RegenerativeRate must be greater than 0")]
        [SerializeField] protected int m_RegenerateAmount;

        [Tooltip("The name of the state to play when idle. The Animator component must exist on the item GameObject")]
        [SerializeField] protected string m_IdleAnimationStateName;
        [Tooltip("The name of the state to play when firing. The Animator component must exist on the item GameObject")]
        [SerializeField] protected string m_FireAnimationStateName;
        [Tooltip("The name of the state to play when reloading. The Animator component must exist on the item GameObject")]
        [SerializeField] protected string m_ReloadAnimationStateName;

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
        [SerializeField] protected ParticleSystem m_Particles;

        [Tooltip("Optionally specify a sound that should randomly play when the weapon is fired")]
        [SerializeField] protected AudioClip[] m_FireSound;
        [Tooltip("If Fire Sound is specified, play the sound after the specified delay")]
        [SerializeField] protected float m_FireSoundDelay;
        [Tooltip("Optionally specify a sound that should randomly play when the weapon is fired and out of ammo")]
        [SerializeField] protected AudioClip[] m_EmptyFireSound;
        [Tooltip("Optionally specify a sound that should randomly play when the weapon is reloaded")]
        [SerializeField] protected AudioClip[] m_ReloadSound;

        [Tooltip("Optionally specify a projectile that the weapon should use")]
        [SerializeField] protected GameObject m_Projectile;
        [Tooltip("Should the projectile always be visible? This only applies to weapons that have a projectile")]
        [SerializeField] protected bool m_ProjectileAlwaysVisible;
        [Tooltip("If a projectile and always visible is enabled, the location is the position and rotation that the rest projectile spawns at")]
        [SerializeField] protected Transform m_ProjectileRestLocation;
        [Tooltip("If a projectile and always visible is enabled, the parent is the GameObject that should be the parent of the projectile")]
        [SerializeField] protected Transform m_ProjectileRestParent;

        [Tooltip("A LayerMask of the layers that can be hit when fired at. This only applies to weapons that do not have a projectile")]
        [SerializeField] protected LayerMask m_HitscanImpactLayers = -1;
        [Tooltip("Optionally specify an event to send when the hitscan fire hits a target")]
        [SerializeField] protected string m_HitscanDamageEvent;
        [Tooltip("The amount of damage done to the object hit. This only applies to weapons that do not have a projectile")]
        [SerializeField] protected float m_HitscanDamageAmount = 10;
        [Tooltip("How much force is applied to the object hit. This only applies to weapons that do not have a projectile")]
        [SerializeField] protected float m_HitscanImpactForce = 5;
        [Tooltip("Optionally specify a default decal that should be applied to the object hit. This only applies to weapons that do not have a projectile and " +
                 "only used if no per-object decal is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultHitscanDecal;
        [Tooltip("Optionally specify any default dust that should appear on top of the object hit. This only applies to weapons that do not have a projectile and " +
                  "used if no per-object dust is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultHitscanDust;
        [Tooltip("Optionally specify any default sparks that should appear on top of the object hit. This only applies to weapons that do not have a projectile and " +
                 "only used if no per-object spark is setup in the ObjectManager")]
        [SerializeField] protected GameObject m_DefaultHitscanSpark;
        [Tooltip("Optionally specify a default impact sound that should play at the point of the object hit. This only applies to weapons that do not have a projectile and " +
                 "only used if no per-object sound is setup in the ObjectManager")]
        [SerializeField] protected AudioClip m_DefaultHitscanImpactSound;
        [Tooltip("Optionally specify a tracer that should should appear when the hitscan weapon is fired")]
        [SerializeField] protected GameObject m_Tracer;
        [Tooltip("If Tracer is specified, the location is the position and rotation that the tracer spawns at")]
        [SerializeField] protected Transform m_TracerLocation;

        [Tooltip("Should the weapon overheat after firing too many shots?")]
        [SerializeField] protected bool m_Overheat;
        [Tooltip("The number of shots it takes for the weapon to overheat")]
        [SerializeField] protected float m_OverheatShotCount;
        [Tooltip("The time it takes for the weapon to cooldown after overheating")]
        [SerializeField] protected float m_CooldownDuration;

        [Tooltip("Optionally specify a flashlight that should shine in the direction of the target")]
        [SerializeField] protected GameObject m_Flashlight;
        [Tooltip("If the flashlight GameObject is specified, should the flashlight automatically activate when aim or should it manually be toggled?")]
        [SerializeField] protected bool m_ActivateFlashlightOnAim;
        [Tooltip("Optionally specify a laser sight that should point at the target")]
        [SerializeField] protected GameObject m_LaserSight;
        [Tooltip("If the laser sight GameObject is specified, should the laser sight automatically activate when aim or should it manually be toggled?")]
        [SerializeField] protected bool m_ActivateLaserSightOnAim;
        [Tooltip("If the laser sight GameObject is specified, should the crosshairs be disabled when the laser sight is active?")]
        [SerializeField] protected bool m_DisableCrosshairsWhenLaserSightActive = true;

        // Exposed properties for the Item Builder
        public Transform FirePoint { set { m_FirePoint = value; } }
        public AnimatorItemCollectionData ReloadStates { get { return m_ReloadStates; } }

        // SharedFields
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
        private bool m_Reloading;
        private RaycastHit m_RaycastHit;
        private int m_ShotsFired;
        private float m_RegenerateDelay;
        private ScheduledEvent m_OverheatEvent;
        private ScheduledEvent m_RegenerateEvent;
        private bool m_ScopeShown;
        private Collider[] m_CharacterColliders;
        private int m_IdleAnimationStateHash;
        private int m_FireAnimationStateHash;
        private int m_ReloadAnimationStateHash;
        private Vector3 m_ProjectileRestLocalPosition;
        private Quaternion m_ProjectileRestLocalRotation;
        private Collision m_WeaponCollision;

        // Component references
        private AudioSource m_AudioSource;
        private Animator m_Animator;
        private GameObject m_RestProjectile;
        private Transform m_CharacterTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_AudioSource = GetComponent<AudioSource>();
            m_Animator = GetComponent<Animator>();
            if (m_Animator != null) {
                m_IdleAnimationStateHash = Animator.StringToHash(m_IdleAnimationStateName);
                m_FireAnimationStateHash = Animator.StringToHash(m_FireAnimationStateName);
                m_ReloadAnimationStateHash = Animator.StringToHash(m_ReloadAnimationStateName);
            }

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
        /// Register for any events that the weapon should be aware of. Ensure the attachments are disabled.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            if (m_Character != null && m_UseScope) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", ShowScope);
            }
            if (m_Flashlight != null && m_Flashlight.activeSelf) {
                m_Flashlight.SetActive(false);
            }
            if (m_LaserSight != null && m_LaserSight.activeSelf) {
                m_LaserSight.SetActive(false);
            }

            // Start playing the idle state.
            if (m_Animator != null && m_IdleAnimationStateHash != 0) {
                m_Animator.Play(m_IdleAnimationStateHash);
            }

            EventHandler.RegisterEvent(m_GameObject, "OnItemEmptyClip", EmptyClip);
            EventHandler.RegisterEvent(m_GameObject, "OnItemAddFireEffects", AddFireEffects);
            EventHandler.RegisterEvent<Transform, Vector3, Vector3>(m_GameObject, "OnItemAddHitscanEffects", AddHitscanEffects);

            // Init may not have been called from the inventory so the character GameObject may not have been assigned yet.
            if (m_Character != null) {
                // Show the rest projectile and scope if the character switched to this weapon.
                if (m_Controller.Aiming) {
                    ShowHideRestProjectile(true);
                    ShowScope(true);
                }

                EventHandler.RegisterEvent(m_Character, "OnItemReloadComplete", ReloadComplete);
                if (m_Projectile != null && m_ProjectileAlwaysVisible) {
                    EventHandler.RegisterEvent(m_Character, "OnItemShowRestProjectile", ShowRestProjectile);
                }
                if (m_WaitForEndUseEvent) {
                    EventHandler.RegisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
                }
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
            // The animation states should begin fresh.
            m_ReloadStates.ResetNextState();

            if (m_Character != null && m_UseScope) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerAim", ShowScope);
                if (m_ScopeShown) {
                    ShowScope(false);
                }
            }
            if (m_Flashlight != null && m_Flashlight.activeSelf) {
                m_Flashlight.SetActive(false);
            }
            if (m_LaserSight != null && m_LaserSight.activeSelf) {
                m_LaserSight.SetActive(false);

                if (m_DisableCrosshairsWhenLaserSightActive) {
                    EventHandler.ExecuteEvent<bool>(m_Character, "OnLaserSightUseableLaserSightActive", false);
                }
            }
            m_Reloading = false;
            ShowHideRestProjectile(false);

            EventHandler.UnregisterEvent(m_GameObject, "OnItemEmptyClip", EmptyClip);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemAddFireEffects", AddFireEffects);
            EventHandler.UnregisterEvent<Transform, Vector3, Vector3>(m_GameObject, "OnItemAddHitscanEffects", AddHitscanEffects);

            // The character may be null if Init hasn't been called yet.
            if (m_Character != null) {
                EventHandler.UnregisterEvent(m_Character, "OnItemReloadComplete", ReloadComplete);
                if (m_Projectile != null && m_ProjectileAlwaysVisible) {
                    EventHandler.UnregisterEvent(m_Character, "OnItemShowRestProjectile", ShowRestProjectile);
                }
                if (m_WaitForEndUseEvent) {
                    EventHandler.UnregisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
                }
            }
        }

        /// <summary>
        /// Initializes the item after it has been added to the Inventory.
        /// </summary>
        /// <param name="inventory">The parent character's inventory.</param>
        public override void Init(Inventory inventory)
        {
            base.Init(inventory);

            // The inventory may be initialized before the item so cache the components in the Init method.
            m_CharacterTransform = inventory.transform;
            m_Controller = m_CharacterTransform.GetComponentInChildren<RigidbodyCharacterController>();

            // When the projectile is instantiated it should ignore all of the character's colliders.
            if (m_Projectile != null) {
                m_CharacterColliders = m_Character.GetComponents<Collider>();
            }

            // Initialize the animation states.
            m_ReloadStates.Initialize(m_ItemType);

            // Register for character events within Init to allow the weapon to recieve the callback even when the weapon isn't active. This allows
            // the character to pickup ammo for a weapon before picking up the weapon and having the ammo already loaded.
            EventHandler.RegisterEvent<Item, bool, bool>(m_Character, "OnInventoryConsumableItemCountChange", ConsumableItemCountChange);
            EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);

            // Register for character events if the GameObject is active. OnEnable normally registers for these callbacks but in this case OnEnable has already occurred.
            if (gameObject.activeSelf) {
                // Show the rest projectile and scope if the character switched to this weapon.
                if (m_Controller.Aiming) {
                    ShowHideRestProjectile(true);
                    ShowScope(true);
                }

                EventHandler.RegisterEvent(m_Character, "OnItemReloadComplete", ReloadComplete);
                if (m_Projectile != null && m_ProjectileAlwaysVisible) {
                    EventHandler.RegisterEvent(m_Character, "OnItemShowRestProjectile", ShowRestProjectile);
                }
                if (m_WaitForEndUseEvent) {
                    EventHandler.UnregisterEvent(m_Character, "OnAnimatorItemEndUse", EndUse);
                }
            }
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
            // Any animation called by the Weapon component is a high priority animation.
            if (highPriority) {
                if (IsReloading()) {
                    var reloadState = m_ReloadStates.GetState(layer, m_Controller.Moving);
                    if (reloadState != null) {
                        return reloadState;
                    }
                }
            }
            return base.GetDestinationState(highPriority, layer);
        }

        /// <summary>
        /// Try to fire the weapon. The weapon may not be able to be fired for various reasons such as it already firing or it being out of ammo.
        /// </summary>
        public override bool TryUse()
        {
            if (!m_IsFiring && m_CanFire && !m_Reloading && m_LastShootTime + m_ShootDelay < Time.time && m_OverheatEvent == null) {
                if (m_Inventory.GetItemCount(m_ItemType) > 0) {
                    m_IsFiring = true;
                    // Prevent the weapon from continuously firing if it not a fully automatic. AI agents do not have to follow this because they don't manually stop firing.
                    m_CanFire = m_FireMode == FireMode.FullAuto || m_AIAgent.Invoke();
                    // Play a fire animation.
                    if (m_Animator != null && m_FireAnimationStateHash != 0) {
                        m_Animator.Play(m_FireAnimationStateHash, 0, 0);
                    }
                    // Do not regenerate any more ammo after starting to fire.
                    if (m_RegenerateEvent != null) {
                        Scheduler.Cancel(ref m_RegenerateEvent);
                    }
                    // Wait to fire if the weapon shouldn't instantly fire. When the animations are ready to fire the Used method will be called.
                    if (m_FireType == FireType.Instant) {
                        // Wait until the used event is called before firing.
                        if (!m_FireOnUsedEvent) {
                            DoFire();
                        }
                    } else {
                        m_ReadyToFire = false;
                        EventHandler.ExecuteEvent(m_Character, "OnUpdateAnimator");
                    }
                    return true;
                } else {
                    EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
#if ENABLE_MULTIPLAYER
                    m_NetworkMonitor.ExecuteItemEvent(m_ItemType.ID, "OnItemEmptyClip");
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
            if ((m_FireMode != FireMode.SemiAuto && m_Inventory.GetItemCount(m_ItemType, true) == 0) || (m_FireMode == FireMode.Burst && m_CurrentBurst == 0)) {
                m_EmptyClipEvent = Scheduler.Schedule(m_ShootDelay, EmptyClip);
            }
        }

        /// <summary>
        /// Can the item be used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        public override bool CanUse()
        {
            if (!base.CanUse()) {
                return false;
            }
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
            if ((m_FireType == FireType.Instant && (m_FireEvent == null || m_AIAgent.Invoke())) || m_Inventory.GetItemCount(m_ItemType) == 0) {
                if (m_FireOnUsedEvent && m_Inventory.GetItemCount(m_ItemType) > 0) {
                    DoFire();
                }
                if (!m_WaitForEndUseEvent) {
                    StopFiring(true);
                }
            } else {
                m_ReadyToFire = true;
                if (m_FireType == FireType.ChargeAndFire) {
                    DoFire();
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

            if (m_ReadyToFire && m_FireType == FireType.ChargeAndHold) {
                DoFire();
            }

            // Don't stop firing if waiting for an end use event or the weapon will stop firing by itself.
            if (m_WaitForEndUseEvent || (m_FireType == FireType.Instant && m_FireMode == FireMode.SemiAuto)) {
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
            m_Inventory.UseItem(m_ItemType, 1);

            // Add any fire effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
            m_NetworkMonitor.ExecuteItemEvent(m_ItemType.ID, "OnItemAddFireEffects");
#else
            AddFireEffects();
#endif

            // Determine the the weapon should be fired again.
            var repeatFire = m_Inventory.GetItemCount(m_ItemType) > 0 && m_FireMode != FireMode.SemiAuto;
            if (m_FireMode == FireMode.Burst) {
                m_CurrentBurst--;
                if (m_CurrentBurst == 0) {
                    repeatFire = false;
                }
            }

            // Increase the number of continuous shots fired if the weapon can overheat.
            if (m_Overheat) {
                m_ShotsFired++;
                if (m_ShotsFired >= m_OverheatShotCount) {
                    repeatFire = false;
                    m_OverheatEvent = Scheduler.Schedule(m_CooldownDuration, OverheatComplete);
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
            if (m_Particles) {
                m_Particles.Play();
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
            GameObject projectileGameObject;
            // Use the rest projectile if it exists. If it does not exist then spawn a new projectile.
            if (m_RestProjectile != null) {
                projectileGameObject = m_RestProjectile;
                projectileGameObject.transform.parent = null;
                projectileGameObject.transform.position = m_FirePoint.position;
                projectileGameObject.transform.rotation = rotation * m_Projectile.transform.rotation;
                m_RestProjectile = null;
            } else {
                projectileGameObject = ObjectPool.Spawn(m_Projectile, m_FirePoint.position, rotation * m_Projectile.transform.rotation);
            }
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
                m_NetworkMonitor.ExecuteItemEvent(m_ItemType.ID, "OnItemAddHitscanEffects", m_RaycastHit.transform.gameObject, m_RaycastHit.point, m_RaycastHit.normal);
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
                var decal = ObjectManager.ObjectForItem(hitTransform.tag, m_ItemType, ObjectManager.ObjectCategory.Decal) as GameObject;
                if (decal == null) {
                    decal = m_DefaultHitscanDecal;
                }
                if (decal != null) {
                    // Apply a decal to the hit point. Offset the decal by a small amount so it doesn't interset with the object hit.
                    DecalManager.Add(decal, hitPoint + hitNormal * 0.02f, decal.transform.rotation * hitRotation, hitTransform);
                }
            }

            // Spawn a dust particle effect at the hit point.
            var dust = ObjectManager.ObjectForItem(hitTransform.tag, m_ItemType, ObjectManager.ObjectCategory.Dust) as GameObject;
            if (dust == null) {
                dust = m_DefaultHitscanDust;
            }
            if (dust != null) {
                ObjectPool.Instantiate(dust, hitPoint, dust.transform.rotation * hitRotation);
            }

            // Spawn a spark particle effect at the hit point.
            var spark = ObjectManager.ObjectForItem(hitTransform.tag, m_ItemType, ObjectManager.ObjectCategory.Spark) as GameObject;
            if (spark == null) {
                spark = m_DefaultHitscanSpark;
            }
            if (spark != null) {
                ObjectPool.Instantiate(spark, hitPoint, spark.transform.rotation * hitRotation);
            }

            // Play a sound at the hit point.
            var audioClip = ObjectManager.ObjectForItem(hitTransform.tag, m_ItemType, ObjectManager.ObjectCategory.Audio) as AudioClip;
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

            m_IsFiring = success && m_FireEvent != null;
            m_CurrentBurst = m_BurstRate;
            m_ShotsFired = 0;
            m_AimStates.NextState();
            m_UseStates.NextState();
            if (m_AutoReload && m_Inventory.GetItemCount(m_ItemType, true) == 0) {
                StartReload();
            }

            if (m_FireEvent != null) {
                Scheduler.Cancel(ref m_FireEvent);
            }

            // Keep repeating the empty clip method until the stop used event is called.
            if (m_EmptyClipEvent == null && success && 
                ((m_FireMode != FireMode.SemiAuto && m_Inventory.GetItemCount(m_ItemType, true) == 0) || (m_FireMode == FireMode.Burst && m_CurrentBurst == 0))) {
                m_EmptyClipEvent = Scheduler.Schedule(m_ShootDelay, EmptyClip);
            }

            // Play any ending animations if the weapon stopped firing through a non-used event.
            if (!success && m_Animator != null && m_IdleAnimationStateHash != 0) {
                m_Animator.Play(m_IdleAnimationStateHash, 0, 0);
            }

            if (m_Particles) {
                m_Particles.Stop();
            }

            if (m_RegenerateDelay != 0) {
                m_RegenerateEvent = Scheduler.Schedule(m_RegenerateDelay, RegenerateAmmo);
            }

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
        /// The collider has collided with another object.
        /// </summary>
        /// <param name="collision">The object that collided with the ShootableWeapon.</param>
        protected override void OnCollisionEnter(Collision collision)
        {
            // If the controller is null then the weapon hasn't been initialized yet.
            if (m_Controller == null) {
                return;
            }

            // Only forward the collision info on if the controller is aiming. This prevents the character from reacting to the collision when the weapon is resting.
            if (m_Controller.Aiming) {
                base.OnCollisionEnter(collision);
            } else {
                // Keep track of the collision info if the character starts to aim.
                m_WeaponCollision = collision;
            }
        }

        /// <summary>
        /// The collider has exited its collision with another object.
        /// </summary>
        /// <param name="collision">The object that had collided with the Item.</param>
        protected override void OnCollisionExit(Collision collision)
        {
            // If the controller is null then the weapon hasn't been initialized yet.
            if (m_Controller == null) {
                return;
            }

            base.OnCollisionExit(collision);

            // Reset the weapon collision data if the character is not aiming to prevent the item from reporting the collision point.
            if (!m_Controller.Aiming) {
                m_WeaponCollision = null;
            }
        }

        /// <summary>
        /// Callback from the controller when the item starts to aim.
        /// </summary>
        protected override void OnStartAim()
        {
            base.OnStartAim();

            ShowScope(true);
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        protected override void OnAim(bool aim)
        {
            base.OnAim(aim);

            ShowHideRestProjectile(aim);

            if (!aim) {
                if (InUse()) {
                    StopFiring(false);
                }
                // When the character is no longer aiming reset the aim/use states so they will begin fresh.
                m_ReloadStates.ResetNextState();
            } else if (m_WeaponCollision != null) {
                // The weapon just started to aim and is colliding with an object. Report that object to the base class.
                base.OnCollisionEnter(m_WeaponCollision);
                m_WeaponCollision = null;
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
            // DualWield items require the inventory to already contain multiple items. The DualWielded item may never be picked up so it doesn't need to take ammo from the PrimaryItem.
            if (added && ((item.ItemType.Equals(m_ItemType) || 
                          (m_ItemType is DualWieldItemType && (m_ItemType as DualWieldItemType).PrimaryItem.Equals(item.ItemType)) && m_Inventory.GetItemCount(m_ItemType, true, true) > 1) ||
                          (item.ItemType is DualWieldItemType && (item.ItemType as DualWieldItemType).PrimaryItem.Equals(m_ItemType)) && m_Inventory.GetItemCount(m_ItemType, true, true) > 1)) {
                if (m_Inventory.GetItemCount(m_ItemType, true) == 0) {
                    if (immediateChange) {
                        ReloadComplete();
                    } else if (m_AutoReload) {
                        StartReload();
                    }
                }
            }
        }

        /// <summary>
        /// Starts to reload the weapon. Returns if the weapon is empty.
        /// </summary>
        public void StartReload()
        {
            // The weapon is already being reloaded so it cannot be reloaded again.
            if (m_Reloading) {
                return;
            }

            // Can't reload if the clip size if infinitely large.
            if (m_ClipSize == int.MaxValue) {
                return;
            }

            // Can't reload if the clip is full.
            var loadedCount = m_Inventory.GetItemCount(m_ItemType, true);
            if (loadedCount == m_ClipSize) {
                return;
            }

            // Ask the inventory how much ammo is remaining.
            var unloadedCount = m_Inventory.GetItemCount(m_ItemType, false);
            if (unloadedCount > 0) {
                // Stop firing while reloading.
                if (InUse()) {
                    StopFiring(false);
                }

                // Hide the scope while reloading.
                if (m_ScopeShown) {
                    EventHandler.ExecuteEvent<bool>(m_Character, "OnItemShowScope", false);
                }

                // Play the reload animation on the item.
                if (m_Animator != null && m_ReloadAnimationStateHash != 0) {
                    m_Animator.Play(m_ReloadAnimationStateHash);
                }

                // Play a sound.
                if (m_ReloadSound != null && m_ReloadSound.Length > 0) {
                    m_AudioSource.clip = m_ReloadSound[Random.Range(0, m_ReloadSound.Length)];
                    m_AudioSource.Play();
                }

                m_Reloading = true;
                EventHandler.ExecuteEvent(m_Character, "OnItemReload");
            }
        }

        /// <summary>
        /// Is the item reloading?
        /// </summary>
        /// <returns>True if the item is reloading.</returns>
        public bool IsReloading()
        {
            return m_Reloading;
        }

        /// <summary>
        /// Tries to stop the item reload.
        /// </summary>
        public void TryStopReload()
        {
            if (m_Reloading) {
                m_Reloading = false;
                EventHandler.ExecuteEvent(m_Character, "OnUpdateAnimator");
            }
        }

        /// <summary>
        /// The weapon was overheated and OverheatComplete will be called when the weapon has cooled down.
        /// </summary>
        private void OverheatComplete()
        {
            m_OverheatEvent = null;
        }

        /// <summary>
        /// Does the actual reload.
        /// </summary>
        private void ReloadComplete()
        {
            var unloadedCount = m_Inventory.GetItemCount(m_ItemType, false);
            var loadedCount = m_Inventory.GetItemCount(m_ItemType, true);

            // If there is a DualWieldItemType for the PrimaryItemType then the primary item needs to share the unloaded ammo.
            int amount = 0;
            var dualWieldItemType = m_Inventory.DualWieldItemForPrimaryItem(m_ItemType);
            if (dualWieldItemType != null || m_ItemType is DualWieldItemType) {
                var dualWieldLoadedCount = 0;
                if (dualWieldItemType != null) {
                    dualWieldLoadedCount = m_Inventory.GetItemCount(dualWieldItemType, true);
                } else {
                    dualWieldLoadedCount = loadedCount;
                }
                if (unloadedCount < (m_ClipSize - loadedCount) + (m_ClipSize - dualWieldLoadedCount)) {
                    amount = unloadedCount / 2;
                    // The primary loaded count should never be lower then the dual wielded loaded count after reloading.
                    if (loadedCount < dualWieldLoadedCount) {
                        var loadedCountDiff = Mathf.CeilToInt((dualWieldLoadedCount - loadedCount) / 2f);
                        amount += loadedCountDiff;
                        if (loadedCountDiff % 2 == 1 && dualWieldLoadedCount - loadedCount > 1) {
                            amount += 1;
                        }
                    } else if (unloadedCount % 2 == 1 && loadedCount == dualWieldLoadedCount) {
                        amount += 1;
                    }
                } else {
                    amount = m_ClipSize - loadedCount;
                }
            } else {
                if (unloadedCount < m_ClipSize - loadedCount) {
                    amount = unloadedCount;
                } else {
                    amount = m_ClipSize - loadedCount;
                }
            }

            m_Inventory.ReloadItem(m_ItemType, amount);
            m_Reloading = false;
            if (m_Controller.Aiming) {
                ShowHideRestProjectile(true);
            }

            if (!m_IsFiring) {
                EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
            }

            // If the scope was shown, show the scope again after reloading.
            if (m_ScopeShown) {
                EventHandler.ExecuteEvent<bool>(m_Character, "OnItemShowScope", true);
            }
        }

        /// <summary>
        /// The weapon should regenerate the ammo.
        /// </summary>
        private void RegenerateAmmo()
        {
            // Only regenerate if there is unloaded ammo.
            if (m_Inventory.GetItemCount(m_ItemType, false) > 0) {
                var loadedCount = m_Inventory.GetItemCount(m_ItemType, true);
                // Don't regenerate if the clip is already full.
                if (loadedCount < m_ClipSize) {
                    var amount = m_RegenerateAmount;
#if UNITY_EDITOR
                    if (amount == 0) {
                        Debug.LogWarning("Warning: RegenerateAmount must be a positive number.");
                        amount = 1;
                    }
#endif
                    // Do not let the regenerative amount go over the clip size.
                    if (amount > m_ClipSize - loadedCount) {
                        amount = m_ClipSize - loadedCount;
                    }

                    m_Inventory.ReloadItem(m_ItemType, amount);

                    // Keep regenerating.
                    m_RegenerateEvent = Scheduler.Schedule(m_RegenerateDelay, RegenerateAmmo);
                }
            }
        }

        /// <summary>
        /// Shows or hide the rest projectile.
        /// </summary>
        /// <param name="show">Should the rest projectile be shown?</param>
        private void ShowHideRestProjectile(bool show)
        {
            // A rest projectile should not be shown if the weapon doesn't use projectiles or the projectile isn't always visible.
            if (m_Projectile == null || !m_ProjectileAlwaysVisible) {
                return;
            }

            if (show && m_RestProjectile == null) {
                // Don't show a rest projectile if the inventory doesn't have a projectile.
                if (m_Inventory.GetItemCount(m_ItemType, true) == 0) {
                    return;
                }

                // Store a new local position/rotation if the projectile is being initialized. Alternatively the method is being called after the projectile has been fired
                // so it needs to be spawned according to that local position/rotation.
                if (m_ProjectileRestLocalPosition == Vector3.zero) {
                    m_RestProjectile = ObjectPool.Spawn(m_Projectile, m_ProjectileRestLocation.position, m_ProjectileRestLocation.rotation, m_ProjectileRestParent);
                    m_ProjectileRestLocalPosition = m_RestProjectile.transform.localPosition;
                    m_ProjectileRestLocalRotation = m_RestProjectile.transform.localRotation;
                } else {
                    m_RestProjectile = ObjectPool.Spawn(m_Projectile, Vector3.zero, Quaternion.identity, m_ProjectileRestParent);
                    m_RestProjectile.transform.localPosition = m_ProjectileRestLocalPosition;
                    m_RestProjectile.transform.localRotation = m_ProjectileRestLocalRotation;
                }
                // The projectile GameObject has been spawned. Tell the projectile to wait to move until it has been initialized.
                var projectile = m_RestProjectile.GetComponent<Projectile>();
                projectile.WaitForInitialization();
            } else if (!show && m_RestProjectile != null) {
                ObjectPool.Destroy(m_RestProjectile);
                m_RestProjectile = null;
            }
        }

        /// <summary>
        /// The character has started to reload and a new rest projectile should be spawned.
        /// </summary>
        private void ShowRestProjectile()
        {
            if (m_RestProjectile == null) {
                ShowHideRestProjectile(true);
            }
        }

        /// <summary>
        /// Executes an event which shows or hides the scope UI.
        /// </summary>
        /// <param name="scope">Should the scope be shown?</param>
        private void ShowScope(bool show)
        {
            // Return early if a scope isn't used.
            if (!m_UseScope) {
                return;
            }

            EventHandler.ExecuteEvent<bool>(m_Character, "OnItemShowScope", show);
            m_ScopeShown = show;
        }

        /// <summary>
        /// Toggles the activate state of the flashlight.
        /// </summary>
        public void ToggleFlashlight()
        {
            if (m_Flashlight == null) {
                return;
            }

            m_Flashlight.SetActive(!m_Flashlight.activeSelf);
        }

        /// <summary>
        /// Activates or deactivates the flashlight when the item is aimed.
        /// </summary>
        /// <param name="activate">Should the flashlight be active?</param>
        public void ActivateFlashlightOnAim(bool activate)
        {
            // Don't change the activate state if the flashlight GameObject doesn't exist or it shouldn't automatically be updated on aim.
            // The flashlight can always be deactivated.
            if (m_Flashlight == null || (activate && !m_ActivateFlashlightOnAim)) {
                return;
            }

            m_Flashlight.SetActive(activate);
        }

        /// <summary>
        /// Toggles the activate state of the laser sight.
        /// </summary>
        public void ToggleLaserSight()
        {
            if (m_LaserSight == null) {
                return;
            }

            m_LaserSight.SetActive(!m_LaserSight.activeSelf);
            if (m_DisableCrosshairsWhenLaserSightActive) {
                EventHandler.ExecuteEvent<bool>(m_Character, "OnLaserSightUseableLaserSightActive", m_LaserSight.activeSelf);
            }
        }

        /// <summary>
        /// Activates or deactivates the laser sightt when the item is aimed.
        /// </summary>
        /// <param name="activate">Should the laser sight be active?</param>
        public void ActivateLaserSightOnAim(bool activate)
        {
            // Don't change the activate state if the laser sight GameObject doesn't exist or it shouldn't automatically be updated on aim.
            // The laser sight can always be deactivated.
            if (m_LaserSight == null || (activate && !m_ActivateLaserSightOnAim)) {
                return;
            }

            m_LaserSight.SetActive(activate);
            if (m_DisableCrosshairsWhenLaserSightActive) {
                EventHandler.ExecuteEvent<bool>(m_Character, "OnLaserSightUseableLaserSightActive", activate);
            }
        }
    }
}