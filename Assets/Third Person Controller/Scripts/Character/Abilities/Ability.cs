using UnityEngine;
using System;
using System.Collections;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// Abstract class for any added functionality to the RigidbodyCharacterController. A new ability can add as much or as little functionality as necessary. In some cases
    /// the ability can completely take control of the RigidbodyCharacterController. Methods that return bool indicate if the RigidbodyCharacterController should stop 
    /// execution of that method with its own functionality.
    /// </summary>
    public abstract class Ability : MonoBehaviour
    {
        // Specifies how the ability can be started
        public enum AbilityStartType { Automatic, Manual, ButtonDown, DoublePress }
        // Specifies how the ability can be stopped. Button toggle means that the same button has to be pressed again after the ability has started to stop the ability
        public enum AbilityStopType { Automatic, Manual, ButtonUp, ButtonToggle }

        [Tooltip("A lower index means the ability has a higher priority of being started compared to other abilities")]
        [HideInInspector]
        [SerializeField] protected int m_Index;
        [Tooltip("The button name that can start or stop the ability")]
        [SerializeField] protected string m_InputName;
        [Tooltip("Specifies how the ability can be started")]
        [SerializeField] protected AbilityStartType m_StartType;
        [Tooltip("Specifies how the ability can be stopped")]
        [SerializeField] protected AbilityStopType m_StopType = AbilityStopType.Manual;
        [Tooltip("The length of time it takes to transition to the ability")]
        [SerializeField] protected float m_TransitionDuration = 0.1f;
        [Tooltip("The Animator multiplier of the state")]
        [SerializeField] protected float m_SpeedMultiplier = 1;
        [Tooltip("The sprite indicator used to specify that the ability can start or is active")]
        [SerializeField] protected Sprite m_Indicator;

        // Exposed properties
        public bool IsActive { get { return m_Active; } }
        public int Index { get { return m_Index; } set { m_Index = value; } }
        public string InputName
        {
            get { return m_InputName; }
            set
            {
                m_InputName = value;
            }
        }
        public AbilityStartType StartType { get { return m_StartType; } set { m_StartType = value; } }
        public AbilityStopType StopType { get { return m_StopType; } set { m_StopType = value; } }
        public float SpeedMultiplier { get { return m_SpeedMultiplier; } set { m_SpeedMultiplier = value; } }
        protected Sprite Indicator { get { return m_Indicator; } set { m_Indicator = value; } }
        public bool EquipItem { set { m_EquipItem = value; } }

        // SharedFields
        protected SharedProperty<bool> m_ItemEquipped = null;
        protected SharedProperty<AnimatorItemStateData> m_ItemIdleState = null;
        protected SharedProperty<AnimatorItemStateData> m_ItemMovingState = null;
#if !ENABLE_MULTIPLAYER
        protected SharedMethod<bool> m_IndependentLook = null;
#endif

        // Static variables
        private static WaitForEndOfFrame s_EndOfFrame;

        // Internal variables
        private bool m_Active;
        private bool m_ShowIndicator;
        protected bool m_EquipItem;
        private IEnumerator m_MoveToTargetRoutine;

        // Component references
        protected GameObject m_GameObject;
        protected Transform m_Transform;
        protected Animator m_Animator;
        protected RigidbodyCharacterController m_Controller;
        protected AnimatorMonitor m_AnimatorMonitor;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected virtual void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_Animator = GetComponent<Animator>();
            m_Controller = GetComponent<RigidbodyCharacterController>();
            m_AnimatorMonitor = GetComponent<AnimatorMonitor>();
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        protected virtual void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
            // Independent look characters do not need to communicate with the camera. Do not initialze the SharedFields on the network to prevent non-local characters from
            // using the main camera to determine their look direction. TargetLookPosition has been implemented by the NetworkMonitor component.
#if !ENABLE_MULTIPLAYER
            if (!m_IndependentLook.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
#endif
        }

        /// <summary>
        /// The character has been disabled. Perform any cleanup.
        /// </summary>
        private void OnDisable()
        {
            if (m_ShowIndicator) {
                m_ShowIndicator = false;
                EventHandler.ExecuteEvent<Sprite>(m_GameObject, "OnControllerAbilityChange", null);
            }
        }

        /// <summary>
        /// Can this ability run at the same time as another ability?
        /// </summary>
        /// <returns>True if this ability can run with another ability.</returns>
        public virtual bool IsConcurrentAblity() { return false; }

        /// <summary>
        /// Should the ability's priority be ignored? This only applies if both the active and current ability allow the priority to be ignored.
        /// </summary>
        /// <returns>True if the priority should be ignored.</returns>
        public virtual bool ShouldIgnorePriority() { return false; }

        /// <summary>
        /// Executed on every ability to allow the ability to update. The ability may need to update if it needs to do something when inactive or show a GUI icon when the ability can be started.
        /// </summary>
        public virtual void UpdateAbility()
        {
            if (m_Indicator != null && m_StartType != AbilityStartType.Automatic && m_StartType != AbilityStartType.Manual) {
                var showIndicator = !IsActive && CanStartAbility();
                if (showIndicator != m_ShowIndicator) {
                    m_ShowIndicator = showIndicator;
                    var abilityType = m_ShowIndicator ? Indicator : null;
                    EventHandler.ExecuteEvent<Sprite>(m_GameObject, "OnControllerAbilityChange", abilityType);
                }
            }
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public virtual bool CanStartAbility() { return true; }

        /// <summary>
        /// Can the specified ability start?
        /// </summary>
        /// <param name="ability">The ability that is trying to start.</param>
        /// <returns>True if the ability can be started.</returns>
        public virtual bool CanStartAbility(Ability ability) { return true; }

        /// <summary>
        /// When an ability is about to be stopped notify which ability is starting.
        /// </summary>
        /// <param name="ability">The ability that is about to start.</param>
        public virtual void AbilityWillStart(Ability ability) { }

        /// <summary>
        /// Can the ability be stopped?
        /// </summary>
        /// <returns>True if the ability can be stopped.</returns>
        public virtual bool CanStopAbility() { return true; }

        /// <summary>
        /// Starts executing the ability. AbilityStarted may be delayed if the character cannot have an item equipped.
        /// </summary>
        public void StartAbility()
        {
            m_Active = true;
            EventHandler.ExecuteEvent<Ability>(m_GameObject, "OnAbilityStart", this);

            // m_EquipItem may already be true if the ability set it ahead of time.
            if (m_EquipItem || CanHaveItemEquipped() || m_ItemEquipped.Get() == false) {
                AbilityStarted();
            } else {
                m_EquipItem = true;
                EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipped", AbilityStarted);
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
            }
        }

        /// <summary>
        /// The ability has been started.
        /// </summary>
        protected virtual void AbilityStarted()
        {
            // No longer need to listen for the unequipped event if the item was previously equipped.
            if (m_EquipItem) {
                EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipped", AbilityStarted);
            }
            // Reset the Animator only if the ability has control.
            if (HasAnimatorControl()) {
                m_AnimatorMonitor.SetStateValue(0);
                m_AnimatorMonitor.DetermineStates();
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public virtual string GetDestinationState(int layer) { return string.Empty; }

        /// <summary>
        /// Returns the duration of the state transition.
        /// </summary>
        /// <returns>The duration of the state transition.</returns>
        public virtual float GetTransitionDuration() { return m_TransitionDuration; }

        /// <summary>
        /// Returns the normalized time that the state should transition to.
        /// </summary>
        /// <returns>The normalized time that the state should transition to.</returns>
        public virtual float GetNormalizedTime() { return 0; }

        /// <summary>
        /// Can the ability replay animation states?
        /// </summary>
        public virtual bool CanReplayAnimationStates() { return false; }

        /// <summary>
        /// Stop the ability from running.
        /// </summary>
        /// <returns>True if the ability was stopped.</returns>
        public bool StopAbility()
        {
            return StopAbility(false);
        }

        /// <summary>
        /// Stop the ability from running.
        /// </summary>
        /// <param name="force">Should the ability be force stopped?</param>
        /// <returns>True if the ability was stopped.</returns>
        public bool StopAbility(bool force)
        {
            if (!m_Active || (!force && !CanStopAbility())) {
                return false;
            }

            // Do the actual stop.
            if (force || ShouldProceedWithStop()) {
                AbilityStopped();
            }

            return true;
        }

        /// <summary>
        /// Should the ability proceed with the stop? Returning false allows the abilities to perform any cleanup animations.
        /// </summary>
        /// <returns>True if the ability should be stopped. If false is returned the ability must call AbilityStopped later.</returns>
        protected virtual bool ShouldProceedWithStop() { return true; }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected virtual void AbilityStopped()
        {
            // Equip the item again if it was equipped when the ability started.
            if (m_EquipItem) {
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                m_EquipItem = false;
            }

            if (m_MoveToTargetRoutine != null) {
                StopCoroutine(m_MoveToTargetRoutine);
            }
            m_Active = false;
            m_Controller.AbilityStopped();
            if (HasAnimatorControl()) {
                m_AnimatorMonitor.DetermineStates();
            }
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its Move method?</returns>
        public virtual bool Move() { return true; }

        /// <summary>
        /// Perform checks to determine if the character is on the ground.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckGround method?</returns>
        public virtual bool CheckGround() { return true; }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public virtual bool CheckForExternalForces(float xPercent, float zPercent) { return true; }

        /// <summary>
        /// Ensure the current movement direction is valid.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its CheckMovement method?</returns>
        public virtual bool CheckMovement() { return true; }

        /// <summary>
        /// Set the physic material based on the grounded and stepping state.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its SetPhysicsMaterial method?</returns>
        public virtual bool SetPhysicMaterial() { return true; }

        /// <summary>
        /// Apply any movement.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateMovement method?</returns>
        public virtual bool UpdateMovement() { return true; }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateRotation method?</returns>
        public virtual bool UpdateRotation() { return true; }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its UpdateAnimator method?</returns>
        public virtual bool UpdateAnimator() { return true; }

        /// <summary>
        /// The Animator has changed positions or rotations.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController continue execution of its OnAnimatorMove method?</returns>
        public virtual bool AnimatorMove() { return true; }

        /// <summary>
        /// Should the upper body be forced to have the same time as the lower body? 
        /// </summary>
        /// <returns>True if the upper body should be forced to have the same time as the lower body.</returns>
        public virtual bool ForceUpperBodySynchronization() { return false; }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if the Animator should not update to reflect the current state.</returns>
        public virtual bool HasAnimatorControl()
        {
            // Do not allow Animator Control if the character is moving into position. The ability hasn't really started yet.
            return m_MoveToTargetRoutine == null;
        }

        /// <summary>
        /// Can the AnimatorMonitor make state transitions?
        /// </summary>
        /// <param name="layer">The layer to check against.</param>
        /// <returns>True if the AnimatorMonitor should be able to make state transitions.</returns>
        public virtual bool AllowStateTransitions(int layer) { return true; }

        /// <summary>
        /// Should the ability override the item's high priority state?
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>True if the ability should override the item state.</returns>
        public virtual bool OverrideItemState(int layer) { return false; }

        /// <summary>
        /// Can the character have an item equipped while the ability is active?
        /// </summary>
        /// <returns>True if the character can have an item equipped.</returns>
        public virtual bool CanHaveItemEquipped() { return true; }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// Interaction includes being able to Aim, Use or Reload the item.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public virtual bool CanInteractItem() { return true; }

        /// <summary>
        /// The character wants to use the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        public virtual bool CanUseItem() { return true; }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public virtual bool CanUseIK(int layer) { return true; }

        /// <summary>
        /// Should the input vector be local to the character's rotation when ensuring movement is valid?
        /// </summary>
        /// <returns>True if local movement value should be used.</returns>
        public virtual bool UseLocalMovement() { return false; }

        /// <summary>
        /// Should item collisions be allowed while the ability is active?
        /// </summary>
        /// <returns>True if item collisions should be allowed.</returns>
        public virtual bool AllowItemCollisions() { return false; }

        /// <summary>
        /// Move to the target position/rotation at the move spped. The onComplete action will be called after the character has arrived.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="targetRotation">The target rotation.</param>
        /// <param name="minMoveSpeed">The minimum speed to move positions.</param>
        /// <param name="onComplete">The action to call after the character has arrived.</param>
        protected void MoveToTarget(Vector3 targetPosition, Quaternion targetRotation, float minMoveSpeed, Action onComplete)
        {
            m_MoveToTargetRoutine = MoveToTargetLocal(targetPosition, targetRotation, minMoveSpeed, onComplete);
            StartCoroutine(m_MoveToTargetRoutine);
        }

        /// <summary>
        /// Local method to move to the target position/rotation at the move spped. The onComplete action will be called after the character has arrived.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="targetRotation">The target rotation.</param>
        /// <param name="minMoveSpeed">The minimum speed to move positions.</param>
        /// <param name="onComplete">The action to call after the character has arrived.</param>
        private IEnumerator MoveToTargetLocal(Vector3 targetPosition, Quaternion targetRotation, float minMoveSpeed, Action onComplete)
        {
            if (s_EndOfFrame == null) {
                s_EndOfFrame = new WaitForEndOfFrame();
            }
            
            // Use the existing speed if it is greater then the minimum so the character will smoothly move to the target.
            var moveSpeed = m_Controller.InputVector.magnitude > minMoveSpeed ? m_Controller.InputVector.magnitude : minMoveSpeed;

            // Stop any existing velocities.
            m_Controller.StopMovement();

            // Gradually move towards the target position/rotation.
            var startOffset = m_Transform.InverseTransformPoint(targetPosition);
            while (Mathf.Abs(m_Transform.position.x - targetPosition.x) > 0.1f || Mathf.Abs(m_Transform.position.z - targetPosition.z) > 0.1f || 
                        Quaternion.Angle(m_Transform.rotation, targetRotation) > 0.1f) {
                // Rotate towards the target rotation.
                m_Controller.SetRotation(Quaternion.Slerp(m_Transform.rotation, targetRotation, m_Controller.RotationSpeed * Time.deltaTime));

                // Use Root Motion to determine how far to move.
                var position = m_Transform.position;
                position.x = Mathf.MoveTowards(position.x, targetPosition.x, m_Controller.RootMotionForce.x > 0.05f ? m_Controller.RootMotionForce.x : 0.05f);
                position.z = Mathf.MoveTowards(position.z, targetPosition.z, m_Controller.RootMotionForce.z > 0.05f ? m_Controller.RootMotionForce.z : 0.05f);
                m_Controller.SetPosition(position);

                // Keep walking towards the target position. Slow down as the character gets closer.
                var offset = m_Transform.InverseTransformPoint(targetPosition);
                var strength = (startOffset.x < 0.001f ? 0 : offset.x / startOffset.x);
                m_AnimatorMonitor.SetHorizontalInputValue(moveSpeed * strength);
                strength = (startOffset.z < 0.001f ? 0 : offset.z / startOffset.z);
                m_AnimatorMonitor.SetForwardInputValue(moveSpeed * strength);

                yield return s_EndOfFrame;
            }

            // The character has arrived. Set the position and rotation to its final position.
            m_Controller.SetPosition(targetPosition);
            m_Controller.SetRotation(targetRotation);
            // The character is no longer moving.
            m_Controller.StopMovement();

            // The coroutine has ended.
            m_MoveToTargetRoutine = null;

            // Let the calling ability know that the character has arrived.
            onComplete();
        }
    }
}