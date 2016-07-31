using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Swim ability allows the character to swim while above water. The Swim ability activates as soon as the character enters the water even though they may not be swimming
    /// yet. It will allow normal character movement until the water reaches a predefined depth.
    /// </summary>
    public class Swim : Ability
    {
        // The list of enums that the swimming Animator states can be in.
        private enum SwimIDs { Swim, End, None }

        [Tooltip("The amount of resistance to apply while moving")]
        [SerializeField] protected float m_WaterResistance = 0.15f;
        [Tooltip("The speed that the character can rotate")]
        [SerializeField] protected float m_RotationSpeed = 1;
        [Tooltip("The water depth to start swimming at")]
        [SerializeField] protected float m_SwimDepth = 0.5f;
        [Tooltip("The amount of time that has to elapse before the character can transition between starting and stopping swimming again")]
        [SerializeField] protected float m_TransitionGracePeriod = 0.2f;
        [Tooltip("A reference to the ParticleSystem/AudioSource that can play a spash effect. Index 0 is LeftHand, 1 is RightHand, 2 is LeftFoot, 3 is RightFoot")]
        [SerializeField] protected ParticleSystem[] m_SplashGameObjects;
        [Tooltip("The number of particles to emit when playing a splash particle")]
        [SerializeField] protected int m_SplashEmitCount = 200;
        [Tooltip("Allows a random clip to play when splashing")]
        [SerializeField] protected AudioClip[] m_SplashAudio;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private SwimIDs m_SwimID;
        private bool m_Equipped;
        private float m_TransitionTime;
        private bool m_JumpIn;
        private AbilityStartType m_ShortClimbStartType;

        // SharedFields
        private SharedMethod<string, bool, bool> m_ChangeCameraState = null;

        // Component references
        private Rigidbody m_Rigidbody;
        private ShortClimb m_ShortClimb;
        private ParticleSystem[] m_SplashParticleSystems;
        private AudioSource[] m_SplashAudioSources;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
            m_ShortClimb = GetComponent<ShortClimb>();

            // Populate the ParticleSystem and AudioSource arrays.
            if (m_SplashGameObjects.Length > 0) {
                m_SplashParticleSystems = new ParticleSystem[m_SplashGameObjects.Length];
                m_SplashAudioSources = new AudioSource[m_SplashGameObjects.Length];
                for (int i = 0; i < m_SplashGameObjects.Length; ++i) {
                    m_SplashParticleSystems[i] = m_SplashGameObjects[i].GetComponent<ParticleSystem>();
                    m_SplashAudioSources[i] = m_SplashGameObjects[i].GetComponent<AudioSource>();
                }
            }
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The velocity may be postiive if the character is exiting the water. Do not start the ability again.
            if (m_Controller.Velocity.y > 0.001f) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            m_SwimID = SwimIDs.None;
            m_Controller.ForceRootMotion = true;

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorSwimStop", StopSwim);

            // Determine if the character is starting to swim by walking or jumping in.
            if (Physics.Raycast(m_Transform.position + m_Transform.up * m_Controller.CapsuleCollider.height, -m_Transform.up, out m_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                m_JumpIn = !Physics.Raycast(m_RaycastHit.point, -m_Transform.up, m_SwimDepth, LayerManager.Mask.IgnoreInvisibleLayersPlayerWater);
            }

            // If the short climb ability exists then set its start type to automatic. This will prevent the character from having to wait to exit the water.
            if (m_ShortClimb != null) {
                m_ShortClimbStartType = m_ShortClimb.StartType;
                m_ShortClimb.StartType = AbilityStartType.Automatic;
            }

            base.AbilityStarted();
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public override bool Move()
        {
            // Determine if the character should swim. Cast a ray down from the player's position to get the height of the water. Cast one more ray down from the
            // water's position to determine the height of the water. If no ground was hit then the character should swim.
            if (m_TransitionTime + m_TransitionGracePeriod < Time.time && Physics.Raycast(m_Transform.position + m_Transform.up * m_Controller.CapsuleCollider.height, -m_Transform.up, 
                                        out m_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                var swim = (m_RaycastHit.point.y - m_Transform.position.y > m_SwimDepth - 0.1f) && !Physics.Raycast(m_RaycastHit.point, -m_Transform.up, m_SwimDepth, LayerManager.Mask.IgnoreInvisibleLayersPlayerWater);
                if (swim && m_SwimID == SwimIDs.None) {
                    m_TransitionTime = Time.time;
                    m_SwimID = SwimIDs.Swim;
                    m_Controller.Grounded = true;
                    m_Controller.StopMovement();
                    m_Rigidbody.useGravity = false;
                    m_AnimatorMonitor.DetermineStates();

                    // Keep the camera above water.
                    if (m_ChangeCameraState != null) {
                        m_ChangeCameraState.Invoke("Swim", true);
                    }

                    // The item cannot be equipped.
                    if (m_ItemEquipped.Get()) {
                        EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                        m_Equipped = true;
                    }
                } else if (!swim && m_SwimID == SwimIDs.Swim) {
                    var slope = Mathf.Acos(m_RaycastHit.normal.y) * Mathf.Rad2Deg;
                    if (slope <= m_Controller.SlopeLimit) {
                        if (m_ChangeCameraState != null) {
                            m_ChangeCameraState.Invoke("Swim", false);
                        }

                        m_SwimID = SwimIDs.End;
                        m_Rigidbody.useGravity = true;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // If the character is not swimming yet then apply the normal character movement.
            if (m_SwimID != SwimIDs.Swim) {
                return string.Empty;
            }

            if (layer == m_AnimatorMonitor.BaseLayerIndex || layer == m_AnimatorMonitor.UpperLayerIndex) {
                if (m_JumpIn) {
                    // If the character jumped into the water then the start animation does not need to play.
                    return "Swim.Swim";
                } else {
                    return "Swim.Start";
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public override bool CheckForExternalForces(float xPercent, float zPercent)
        {
            // If the character is not swimming yet then apply the normal character movement.
            if (m_SwimID == SwimIDs.None) {
                return true;
            }
            // Do not allow any external forces while swimming.
            m_Controller.Velocity = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Perform checks to determine if the character is on the ground.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckGround method?</returns>
        public override bool CheckGround()
        {
            return false;
        }

        /// <summary>
        /// Ensure the current movement direction is valid.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckMovement method?</returns>
        public override bool CheckMovement()
        {
            return m_SwimID == SwimIDs.Swim;
        }

        /// <summary>
        /// Move according to the swimming root motion.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            if (m_SwimID == SwimIDs.None) {
                return true;
            }

            var force = Quaternion.Inverse(m_Transform.rotation) * m_Controller.RootMotionForce / (1 + m_WaterResistance);
            if (m_SwimID == SwimIDs.Swim) {
                // There is no horizontal or vertical movement with swimming. The character will rotate based on the camera direction.
                force.x = force.y = 0;
            } else {
                // Prevent the character from moving backwards when the ending animation is playing.
                force.z = Mathf.Abs(force.z);
            }
            m_Controller.SetPosition(m_Transform.position + (m_Transform.rotation * force));
            m_Controller.RootMotionForce = Vector3.zero;

            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // If the character is not swimming yet then apply the normal character movement.
            if (m_SwimID != SwimIDs.Swim) {
                return true;
            }

            // The character is swimming. Rotate based on the input.
            if (m_Controller.InputVector != Vector3.zero) {
                var targetRotation = Quaternion.Euler(0, Quaternion.LookRotation(m_Controller.LookRotation * m_Controller.InputVector.normalized).eulerAngles.y, 0);
                m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, targetRotation, m_RotationSpeed * Time.fixedDeltaTime);
            }
            return false;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            if (m_SwimID == SwimIDs.None) {
                return true;
            }
            m_AnimatorMonitor.SetStateValue((int)m_SwimID);
            m_AnimatorMonitor.SetForwardInputValue(m_Controller.InputVector.magnitude);
            return false;
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return m_SwimID != SwimIDs.Swim;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return m_SwimID != SwimIDs.Swim;
        }

        /// <summary>
        /// Optionally play a spash particle.
        /// </summary>
        /// <param name="limbIndex">The index of the limb that should play the splash particle.</param>
        private void Splash(int limbIndex)
        {
            // Do not play a splash particle if the character isn't moving.
            if (m_Controller.InputVector.magnitude < 0.1f) {
                return;
            }

            // Emit the splash particle if it isn't null. Use Emit instead of Play because play isn't always positioned correctly on first run.
            if (m_SplashParticleSystems != null && m_SplashParticleSystems[limbIndex] != null) {
                m_SplashParticleSystems[limbIndex].Emit(m_SplashEmitCount);
            }

            // Play a splash sound if the AudioSource isn't null.
            if (m_SplashAudioSources != null && m_SplashAudioSources[limbIndex] != null) {
                m_SplashAudioSources[limbIndex].clip = m_SplashAudio[Random.Range(0, m_SplashAudio.Length)];
                m_SplashAudioSources[limbIndex].Play();
            }
        }

        /// <summary>
        /// When an ability is about to be stopped notify which ability is starting.
        /// </summary>
        /// <param name="ability">The ability that is about to start.</param>
        public override void AbilityWillStart(Ability ability)
        {
            // Do not equip the item if the short climb is exiting the water.
            if (ability is ShortClimb) {
                if (m_Equipped) {
                    m_Equipped = false;
                    ability.EquipItem = true;
                }
            }
        }

        /// <summary>
        /// The stopping swim animation has finished playing.
        /// </summary>
        private void StopSwim()
        {
            if (m_SwimID == SwimIDs.End) {
                m_TransitionTime = Time.time;
                m_SwimID = SwimIDs.None;
                m_Controller.Grounded = true;
                m_AnimatorMonitor.DetermineStates();

                if (m_Equipped) {
                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                    m_Equipped = false;
                }
            }
        }

        /// <summary>
        /// Can the ability be stopped?
        /// </summary>
        /// <returns>True if the ability can be stopped.</returns>
        public override bool CanStopAbility()
        {
            // The ability can only be stopped if the character is no longer in water.
            if (Physics.Raycast(m_Transform.position + m_Transform.up * m_Controller.CapsuleCollider.height + m_Transform.forward * m_Controller.CapsuleCollider.radius, -m_Transform.up, out m_RaycastHit, Mathf.Infinity, 
                                    LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                return m_RaycastHit.transform.gameObject.layer != LayerManager.Water;
            }
            return true;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();
            // The SwimID will not be none if the character isn't exiting on a slope.
            if (m_SwimID != SwimIDs.None) {
                m_SwimID = SwimIDs.End;
                StopSwim();
            }
            m_Controller.ForceRootMotion = false;
            m_Rigidbody.useGravity = true;
            if (m_ShortClimb != null) {
                m_ShortClimb.StartType = m_ShortClimbStartType;
            }

            if (m_ChangeCameraState != null) {
                m_ChangeCameraState.Invoke("Swim", false);
            }
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorSwimStop", StopSwim);
        }
    }
}