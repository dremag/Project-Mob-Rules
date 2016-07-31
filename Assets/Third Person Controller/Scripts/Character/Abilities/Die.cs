using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// Play a death animation or enable the ragdoll when the character dies.
    /// </summary>
    public class Die : Ability
    {
        [Tooltip("Should the character use a ragdoll upon death?")]
        [SerializeField] protected bool m_RagdollDeath = true;
        [Tooltip("The ragdoll's rigidbodies will be set to kinematic as soon as the transforms have moved a total difference less than this amount between two frames")]
        [SerializeField] protected float m_SettledThreshold = 0.01f;
        [Tooltip("The number of frames that the rigidbodies have to be settled before they are set to kinematic")]
        [SerializeField] protected int m_SettledFrameCount = 5;
        [Tooltip("Should the ragdoll colliders be disabled? Set to false if the collider should be used for hit detection")]
        [SerializeField] protected bool m_DisableColliders = true;

        // Internal variables
        private bool m_CanStop;
        private int m_FrameCount = 0;
        private List<Transform> m_Transforms;
        private List<Collider> m_Colliders;
        private List<Rigidbody> m_Rigidbodies;
        private List<Vector3> m_PrevTransformPosition;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // A ragdoll death has to initialize the ragdoll components.
            if (m_RagdollDeath) {
                m_Transforms = new List<Transform>();
                m_Colliders = new List<Collider>();
                m_Rigidbodies = new List<Rigidbody>();
                m_PrevTransformPosition = new List<Vector3>();

                // Cache the components for quick access.
                var transforms = GetComponentsInChildren<Transform>();
                for (int i = 0; i < transforms.Length; ++i) {
                    if (transforms[i].gameObject == gameObject) {
                        continue;
                    }
                    m_Transforms.Add(transforms[i]);
                    m_PrevTransformPosition.Add(transforms[i].position);
                }
                var colliders = GetComponentsInChildren<Collider>();
                var characterCollider = GetComponent<RigidbodyCharacterController>().CapsuleCollider;
                for (int i = 0; i < colliders.Length; ++i) {
                    // Don't add the collider to the list if the collider is the main character's collider, a trigger, or on an item. The item can have its collider enabled.
                    if (colliders[i].gameObject == gameObject || colliders[i] == characterCollider || colliders[i].isTrigger || 
                            colliders[i].GetComponent<Item>() != null || colliders[i].GetComponent<MeleeWeaponHitbox>() != null) {
                        continue;
                    }
                    m_Colliders.Add(colliders[i]);
                    var colliderRigidbody = colliders[i].GetComponent<Rigidbody>();
                    if (colliderRigidbody != null) {
                        m_Rigidbodies.Add(colliderRigidbody);
                    }
                }
            }
        }

        /// <summary>
        /// When the game starts the ragdoll should initially be disabled.
        /// </summary>
        protected override void Start()
        {
            EventHandler.RegisterEvent<Vector3, Vector3, GameObject>(gameObject, "OnDeathDetails", OnDeath);
            if (m_RagdollDeath) {
                EnableRagdoll(false);
            }
        }

        /// <summary>
        /// The ability has started. Prevent from the ability from being stopped until it is explicitly said to stop and enable the ragdoll if requested.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_CanStop = false;
            // Ragdoll death needs to enable the ragdoll colliders.
            if (m_RagdollDeath) {
                m_FrameCount = 0;
                m_Animator.enabled = false;

                EnableRagdoll(true);
            } else {
                // Call DetermineStates directly because the animator may be disabled.
                m_AnimatorMonitor.DetermineStates();
            }
        }

        /// <summary>
        /// The ability can't be stopped until it explicitly says to.
        /// </summary>
        /// <returns></returns>
        public override bool CanStopAbility()
        {
            return m_CanStop;
        }

        /// <summary>
        /// If using a ragdoll death set the ragdoll's rigidbodies to kinematic as soon as all of the transforms have settled. This will prevent the ragdolls from twitching.
        /// </summary>
        public override bool Move()
        {
            if (m_RagdollDeath) {
                var settledValue = 0f;
                for (int i = 0; i < m_Transforms.Count; ++i) {
                    settledValue += (m_PrevTransformPosition[i] - m_Transforms[i].position).sqrMagnitude;
                    m_PrevTransformPosition[i] = m_Transforms[i].position;
                }

                if (settledValue != 0 && Mathf.Sqrt(settledValue) < m_SettledThreshold) {
                    if (m_FrameCount < m_SettledFrameCount) {
                        m_FrameCount++;
                    } else {
                        for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                            m_Rigidbodies[i].isKinematic = true;
                            m_Rigidbodies[i].constraints = RigidbodyConstraints.FreezeAll;
                        }
                        m_CanStop = true;
                        StopAbility();
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // Only the base layer can play a death animation. Ragdolls do not play a specific animation.
            if (m_RagdollDeath || layer != m_AnimatorMonitor.BaseLayerIndex) {
                return string.Empty;
            }

            return "Die.Die";
        }

        /// <summary>
        /// The ability has stopped. Disable the ragdoll if used.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            if (m_RagdollDeath) {
                m_Animator.enabled = true;
                EnableRagdoll(false);
            }
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return false;
        }

        /// <summary>
        /// The character has died. Start the ability.
        /// </summary>
        /// <param name="force">The amount of force which killed the character.</param>
        /// <param name="position">The position of the force.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        private void OnDeath(Vector3 force, Vector3 position, GameObject attacker)
        {
            EventHandler.UnregisterEvent<Vector3, Vector3, GameObject>(gameObject, "OnDeathDetails", OnDeath);
            EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);

            StartAbility();

            if (m_RagdollDeath) {
                for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                    m_Rigidbodies[i].AddForceAtPosition(force, position);
                }
            }
        }

        /// <summary>
        /// The character has respawned. Stop the ability.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.RegisterEvent<Vector3, Vector3, GameObject>(gameObject, "OnDeathDetails", OnDeath);
            EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);

            m_CanStop = true;
            StopAbility();
        }

        /// <summary>
        /// Enable or disable all of the ragdoll colliders and rigidbodies. 
        /// If enabling the ragdoll then save off the transform positions so we know when the character has settled into position.
        /// </summary>
        /// <param name="enable">Should the ragdoll be enabled?</param>
        private void EnableRagdoll(bool enable)
        {
            if (enable) {
                for (int i = 0; i < m_Transforms.Count; ++i) {
                    m_PrevTransformPosition[i] = m_Transforms[i].position;
                }
            }
            if (m_DisableColliders) {
                for (int i = 0; i < m_Colliders.Count; ++i) {
                    m_Colliders[i].enabled = enable;
                }
            }
            for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                m_Rigidbodies[i].useGravity = enable;
                m_Rigidbodies[i].isKinematic = !enable;
                m_Rigidbodies[i].constraints = (enable ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeAll);
                m_Rigidbodies[i].detectCollisions = true;
            }
        }
    }
}