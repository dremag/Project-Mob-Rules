using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System.Collections.Generic;
using Opsive.ThirdPersonController.Abilities;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Added to the same GameObject as the Animator, the AnimationMonitor will control the trigger based Animator and translate mecanim events to the event system used by the Third Person Controller.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class AnimatorMonitor : NetworkBehaviour
#else
    public class AnimatorMonitor : MonoBehaviour
#endif
    {
        [Tooltip("Should state changes be sent to the debug console?")]
        [SerializeField] protected bool m_DebugStateChanges;
        [Tooltip("The horizontal input dampening time")]
        [SerializeField] protected float m_HorizontalInputDampTime = 0.1f;
        [Tooltip("The forward input dampening time")]
        [SerializeField] protected float m_ForwardInputDampTime = 0.1f;
        [Tooltip("The default base state")]
        [SerializeField] protected AnimatorStateData m_BaseState = new AnimatorStateData("Movement", 0.1f);
        [Tooltip("The default upper body state")]
        [SerializeField] protected AnimatorStateData m_UpperBodyState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default left arm state")]
        [SerializeField] protected AnimatorStateData m_LeftArmState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default right arm state")]
        [SerializeField] protected AnimatorStateData m_RightArmState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default left hand state")]
        [SerializeField] protected AnimatorStateData m_LeftHandState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default right hand state")]
        [SerializeField] protected AnimatorStateData m_RightHandState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default additive state")]
        [SerializeField] protected AnimatorStateData m_AdditiveState = new AnimatorStateData("Idle", 0.1f);

        // Static variables
        private static int s_HorizontalInputHash = Animator.StringToHash("Horizontal Input");
        private static int s_ForwardInputHash = Animator.StringToHash("Forward Input");
        private static int s_YawHash = Animator.StringToHash("Yaw");
        private static int s_StateHash = Animator.StringToHash("State");
        private static int s_IntDataHash = Animator.StringToHash("Int Data");
        private static int s_FloatDataHash = Animator.StringToHash("Float Data");
        private static int s_ColliderCurveDataHash = Animator.StringToHash("Collider Curve Data");

        // SharedFields
        private SharedMethod<bool, string> m_ItemName = null;
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedProperty<Item> m_CurrentSecondaryItem = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;

        // Internal variables
        private string[] m_LayerNames;
        private Dictionary<string, int> m_StateNamesHash = new Dictionary<string, int>();
        private int[] m_ActiveStateHash;
        private bool m_IgnoreLowerPriority;
#if ENABLE_MULTIPLAYER
        private bool m_AnimatorInit;
#endif

        // Parameter values
        private float m_HorizontalInputValue;
        private float m_ForwardInputValue;
        private float m_YawValue;
        private int m_StateValue;
        private int m_IntDataValue;
        private float m_FloatDataValue;

        // Exposed properties
        public float HorizontalInputValue { get { return m_Animator.GetFloat(s_HorizontalInputHash); } }
        public float ForwardInputValue { get { return m_Animator.GetFloat(s_ForwardInputHash); } }
        public float YawValue { get { return m_Animator.GetFloat(s_YawHash); } }
        public int StateValue { get { return m_StateValue; } }
        public int IntDataValue { get { return m_IntDataValue; } }
        public float FloatDataValue { get { return m_FloatDataValue; } }
        public int BaseLayerIndex { get { return 0; } }
        public int UpperLayerIndex { get { return 1; } }
        public int LeftArmLayerIndex { get { return 2; } }
        public int RightArmLayerIndex { get { return 3; } }
        public int LeftHandLayerIndex { get { return 4; } }
        public int RightHandLayerIndex { get { return 5; } }
        public int AdditiveLayerIndex { get { return 6; } }

        // Component references
        private GameObject m_GameObject;
        protected Animator m_Animator;
        private RigidbodyCharacterController m_Controller;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Animator = GetComponent<Animator>();
            m_Controller = GetComponent<RigidbodyCharacterController>();

            if (m_Animator.avatar == null) {
                Debug.LogError("Error: The Animator Avatar on " + m_GameObject + " is not assigned. Please assign an avatar within the inspector.");
            }

            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
            EventHandler.RegisterEvent(m_GameObject, "OnInventoryInitialized", OnInventoryInitialized);
        }

        /// <summary>
        /// Reset the active states list when the component is enabled again after a respawn.
        /// </summary>
        private void OnEnable()
        {
            if (m_ActiveStateHash != null) {
                for (int i = 0; i < m_ActiveStateHash.Length; ++i) {
                    m_ActiveStateHash[i] = 0;
                }
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Initialize the SharedFields so they are ready for when the server starts to send RPC calls.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            SharedManager.InitializeSharedFields(m_GameObject, this);
        }
#endif
        
        /// <summary>
        /// As soon as the inventory has been initialized finish initializing the AnimatorMonitor and play the correct states.
        /// </summary>
        private void OnInventoryInitialized()
        {
            // Do not listen for item events until the inventory initialization is complete.
            EventHandler.RegisterEvent(m_GameObject, "OnUpdateAnimator", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemUse", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemStopUse", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemReload", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemReloadComplete", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnInventoryLoadDefaultLoadout", PlayDefaultStates);
#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent(m_GameObject, "OnInventoryNetworkMessageAdd", PlayDefaultStates);
#endif
            EventHandler.RegisterEvent<Item>(m_GameObject, "OnInventoryDualWieldItemChange", OnDualWieldItemChange);

            // The SharedFields may not have been initialized yet so load them now.
            if (m_ItemName == null) {
                SharedManager.InitializeSharedFields(m_GameObject, this);
            }
            // The inventory may be initialized before Awake is called in which case there needs to be a reference to the controller.
            if (m_Controller == null) {
                m_Controller = GetComponent<RigidbodyCharacterController>();
            }
            // Set the correct states.
            PlayDefaultStates();
        }

        /// <summary>
        /// Plays the starting Animator states. There is no blending.
        /// </summary>
        public void PlayDefaultStates()
        {
            // The Animator may not be enabled if the character died and the Inventory respawn event occurs before the Animator Monitor respawn event.
            if (!m_Animator.enabled) {
                return;
            }

            if (m_LayerNames == null) {
                m_LayerNames = new string[m_Animator.layerCount];
                m_ActiveStateHash = new int[m_Animator.layerCount];
            }
            for (int i = 0; i < m_Animator.layerCount; ++i) {
                m_LayerNames[i] = m_Animator.GetLayerName(i) + ".";
                m_ActiveStateHash[i] = 0;
            }

            m_Animator.SetFloat(s_HorizontalInputHash, m_HorizontalInputValue);
            m_Animator.SetFloat(s_ForwardInputHash, m_ForwardInputValue);
            m_Animator.SetFloat(s_YawHash, m_YawValue);
            m_Animator.SetInteger(s_StateHash, m_StateValue);
            m_Animator.SetInteger(s_IntDataHash, m_IntDataValue);
            m_Animator.SetFloat(s_FloatDataHash, m_FloatDataValue);

#if ENABLE_MULTIPLAYER
            m_AnimatorInit = true;
#endif
            DetermineStates();
#if ENABLE_MULTIPLAYER
            m_AnimatorInit = false;
#endif

            // Force the animator to play the default states immediately.
            m_Animator.Update(1000);
        }

        /// <summary>
        /// Callback from the animator when root motion has updated.
        /// </summary>
        protected virtual void OnAnimatorMove()
        {
            // The delta position will be NaN after the first respawn frame.
            if (float.IsNaN(m_Animator.deltaPosition.x)) {
                return;
            }

            for (int i = 0; i < m_Controller.Abilities.Length; ++i) {
                if (m_Controller.Abilities[i].IsActive && !m_Controller.Abilities[i].AnimatorMove()) {
                    return;
                }
            }

            // Don't read the delta position/rotation if not using root motion.
            if (!m_Controller.UsingRootMotion) {
                return;
            }
            if (m_Controller.Grounded) {
                m_Controller.RootMotionForce += m_Animator.deltaPosition * m_Controller.RootMotionSpeedMultiplier;
            } else {
                m_Controller.RootMotionForce = Vector3.zero;
            }
            m_Controller.RootMotionRotation *= m_Animator.deltaRotation;
        }

        /// <summary>
        /// Determine all of the layer states
        /// </summary>
        public void DetermineStates()
        {
            DetermineStates(true);
        }

        /// <summary>
        /// Determine the Animator states.
        /// </summary>
        /// <param name="checkAbilities">Should the abilities be checked to determine if they have control?</param>
        public void DetermineStates(bool checkAbilities)
        {
            // Do not determine any states if the inventory hasn't been initialized yet.
            if (m_ItemName == null) {
                return;
            }

            m_IgnoreLowerPriority = false;
            var baseChanged = DetermineState(BaseLayerIndex, m_BaseState, checkAbilities, true);
            DetermineState(UpperLayerIndex, m_UpperBodyState, true, baseChanged);
            DetermineState(LeftArmLayerIndex, m_LeftArmState, true, false);
            DetermineState(RightArmLayerIndex, m_RightArmState, true, false);
            DetermineState(LeftHandLayerIndex, m_LeftHandState, true, false);
            DetermineState(RightHandLayerIndex, m_RightHandState, true, false);
            DetermineState(AdditiveLayerIndex, m_AdditiveState, false, false);
        }

        /// <summary>
        /// Determine the state that the specified layer should be in.
        /// </summary>
        /// <param name="layer">The layer to determine the state of.</param>
        /// <param name="defaultState">The default state to be in if no other states should run.</param>
        /// <param name="checkAbilities">Should the abilities be checked to determine if they have control?</param>
        /// <param name="baseStart">Is the base layer being set at the same time?</param>
        /// <returns>True if the state was changed.</returns>
        public virtual bool DetermineState(int layer, AnimatorStateData defaultState, bool checkAbilities, bool baseStart)
        {
            // Return early if there is no layer at the specified layer index.
            if (layer > m_Animator.layerCount - 1) {
                return false;
            }

            // Item actions are the highest priority.
            bool stateChange;
            AnimatorItemStateData state;
            if ((state = HasItemState(true, layer, 0, false, out stateChange)) != null) {
                // Abilities have the chance to override a high priority item state.
                var allowStateChange = true;
                for (int i = 0; i < m_Controller.Abilities.Length; ++i) {
                    if (m_Controller.Abilities[i].IsActive && m_Controller.Abilities[i].HasAnimatorControl() && m_Controller.Abilities[i].OverrideItemState(layer)) {
                        allowStateChange = false;
                    }
                }
                
                if (allowStateChange) {
                    return ChangeItemState(state, layer, 0);
                }
            }

            // Synchronize with the base layer.
            var normalizedTime = 0f;
            if (!baseStart) {
                var baseLayer = BaseLayerIndex;
                if (m_Animator.IsInTransition(baseLayer)) {
                    normalizedTime = m_Animator.GetNextAnimatorStateInfo(baseLayer).normalizedTime % 1;
                } else {
                    normalizedTime = m_Animator.GetCurrentAnimatorStateInfo(baseLayer).normalizedTime % 1;
                }
            }

            for (int i = 0; i < m_Controller.Abilities.Length; ++i) {
                if (m_Controller.Abilities[i].IsActive && m_Controller.Abilities[i].HasAnimatorControl()) {
                    if (!m_Controller.Abilities[i].AllowStateTransitions(layer)) {
                        return false;
                    }
                    var destinationState = m_Controller.Abilities[i].GetDestinationState(layer);
                    if (!string.IsNullOrEmpty(destinationState)) {
                        // Give the item state one more chance to play if an ability state should play.
                        if ((state = HasItemState(false, layer, normalizedTime, false, out stateChange)) != null) {
                            // The ability has to match in order for the item state to override the ability state.
                            if (!m_IgnoreLowerPriority && state.Ability == m_Controller.Abilities[i]) {
                                return ChangeItemState(state, layer, 0);
                            }
                        }
                        return ChangeAnimatorStates(layer, destinationState, m_Controller.Abilities[i].GetTransitionDuration(), m_Controller.Abilities[i].CanReplayAnimationStates(),
                                                    m_Controller.Abilities[i].SpeedMultiplier, m_Controller.Abilities[i].GetNormalizedTime());
                    }
                    if (!m_Controller.Abilities[i].IsConcurrentAblity()) {
                        break;
                    }
                }
            }
            if (!m_IgnoreLowerPriority && (state = HasItemState(false, layer, normalizedTime, false, out stateChange)) != null) {
                return ChangeItemState(state, layer, normalizedTime);
            }
            return ChangeAnimatorStates(layer, defaultState, 0);
        }

        [System.Obsolete("AnimatorMonitor.FormatLowerBodyState is obsolete. Use AnimatorMonitor.FormatStateName instead.")]
        public string FormatLowerBodyState(AnimatorItemStateData stateData)
        {
            return FormatStateName(stateData, BaseLayerIndex);
        }

        [System.Obsolete("AnimatorMonitor.FormatUpperBodyState is obsolete. Use AnimatorMonitor.FormatStateName instead.")]
        public string FormatUpperBodyState(AnimatorItemStateData stateData)
        {
            return FormatStateName(stateData, UpperLayerIndex);
        }

        /// <summary>
        /// Formats the state name from the AnimatorItemStateData. The result will be in the format "ItemType.ItemType AbilityType.StateName".
        /// </summary>
        /// <param name="stateData">The AnimatorItemStateData to format.</param>
        /// <param name="layer">The layer to use for formatting.</param>
        /// <returns>The formatted state name.</returns>
        public string FormatStateName(AnimatorItemStateData stateData, int layer)
        {
            if (stateData == null) {
                return string.Empty;
            }

            var stateDataName = stateData.Name;
            var stateName = string.Empty;
            if (stateData.ItemNamePrefix && m_ItemName != null) {
                stateName = m_ItemName.Invoke(stateData.ItemType == null ? true : (stateData.ItemType is PrimaryItemType || stateData.ItemType is DualWieldItemType));
            }
            if (stateData.Ability != null) {
                // The state name can be overridden by the ability destination state.
                var destinationState = stateData.Ability.GetDestinationState(layer);
                if (!string.IsNullOrEmpty(destinationState)) {
                    // Use the item state name instead of the state provided by the ability.
                    var substateIndex = destinationState.IndexOf('.');
                    stateDataName = destinationState.Remove(substateIndex + 1, destinationState.Length - substateIndex - 1) + stateData.Name;
                    if (!string.IsNullOrEmpty(stateName)) {
                        stateName += "." + stateName + " ";
                    }
                } else {
                    stateName += ".";
                }
            } else if (!string.IsNullOrEmpty(stateName)) {
                stateName += ".";
            }
            return string.IsNullOrEmpty(stateName) ? stateDataName : stateName + stateDataName;
        }

        /// <summary>
        /// Formats the state name based off of if the ItemName should be used.
        /// </summary>
        /// <param name="stateName">The state name to format.</param>
        /// <returns>The formatted state name.</returns>
        public string FormatStateName(string stateName)
        {
            return FormatStateName(stateName, true);
        }

        /// <summary>
        /// Does the current item use the specified ability layer?
        /// </summary>
        /// <param name="ability">The ability checking against the item.</param>
        /// <param name="layer">The layer to check against.</param>
        /// <returns>True if the item should use the specified layer.</returns>
        public bool ItemUsesAbilityLayer(Ability ability, int layer)
        {
            Item item;
            if (!m_IgnoreLowerPriority && (item = m_CurrentPrimaryItem.Get()) != null) {
                var state = item.GetDestinationState(false, layer);
                if (state != null && state.Ability == ability) {
                    return true;
                }
                // The primary item doesn't need to play any states - try the dual wielded item.
                item = m_CurrentDualWieldItem.Get();
                if (item != null) {
                    state = item.GetDestinationState(false, layer);
                    if (state != null && state.Ability == ability) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Determines the stateName based off of if the ItemName should be used.
        /// </summary>
        /// <param name="stateName">The state name to format.</param>
        /// <param name="primaryItem">Is the item a PrimaryItemType?</param>
        /// <returns>The formatted state name.</returns>
        public string FormatStateName(string stateName, bool primaryItem)
        {
            // ItemName may be null if there is no inventory.
            if (m_ItemName != null) {
                return m_ItemName.Invoke(primaryItem) + "." + stateName;
            }
            return stateName;
        }

        /// <summary>
        /// Tries to change to the state for each equipped item.
        /// </summary>
        /// <param name="highPriority">Should only the high priority states be retrieved?</param>
        /// <param name="layer">The current animator layer.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <param name="stateChange">True if the state was changed.</param>
        /// <returns>The state that the Animator should changed to. Note that this state may be the same as the previous state so no changes may be necessary.</returns>
        private AnimatorItemStateData HasItemState(bool highPriority, int layer, float normalizedTime, bool changeStates, out bool stateChange)
        {
            stateChange = false;
            AnimatorItemStateData state;
            if ((state = HasItemState(highPriority, layer, normalizedTime, m_CurrentPrimaryItem.Get(), changeStates, ref stateChange)) != null) {
                return state;
            }
            if ((state = HasItemState(highPriority, layer, normalizedTime, m_CurrentDualWieldItem.Get(), changeStates, ref stateChange)) != null) {
                return state;
            }
            // The SecondayItemType can only respond to high priority state changes.
            if (highPriority) {
                if ((state = HasItemState(highPriority, layer, normalizedTime, m_CurrentSecondaryItem.Get(), changeStates, ref stateChange)) != null) {
                    return state;
                }
            }
            return null;
        }

        /// <summary>
        /// Tries to change to the state requested by the specified item.
        /// </summary>
        /// <param name="highPriority">Should only the high priority states be retrieved?</param>
        /// <param name="layer">The current animator layer.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <param name="item">The item which could change the Animator states.</param>
        /// <param name="changeStates">Should the state actually be changed?</param>
        /// <param name="stateChange">True if the state was changed.</param>
        /// <returns>The state that the Animator should changed to. Note that this state may be the same as the previous state so no changes may be necessary.</returns>
        private AnimatorItemStateData HasItemState(bool highPriority, int layer, float normalizedTime, Item item, bool changeStates, ref bool stateChange)
        {
            if (item != null) {
                var state = item.GetDestinationState(highPriority, layer);
                if (state != null) {
                    if (changeStates) {
                        stateChange = ChangeItemState(state, layer, normalizedTime);
                    }
                }
                return state;
            }
            return null;
        }

        /// <summary>
        /// Changes the animator to the specified state.
        /// </summary>
        /// <param name="state">The state to transition to.</param>
        /// <param name="layer">The current animator layer.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <returns>True if the state was changed.</returns>
        private bool ChangeItemState(AnimatorItemStateData state, int layer, float normalizedTime)
        {
            m_IgnoreLowerPriority = state.IgnoreLowerPriority;
            return ChangeAnimatorStates(layer, state, normalizedTime);
        }

        /*private void Update()
        {
            if (m_Animator.layerCount > 1) {
                // Useful for debugging synchronization problems:
                var baseLayer = m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                baseLayer -= (int)baseLayer;
                var upperLayer = m_Animator.GetCurrentAnimatorStateInfo(1).normalizedTime;
                upperLayer -= (int)upperLayer;
                Debug.Log("Base Time: " + baseLayer + " Upper Time: " + upperLayer + " Diff: " + (baseLayer - upperLayer));
            }
        }*/

        /// <summary>
        /// Change the Animator layer to the specified state with the desired transition.
        /// </summary>
        /// <param name="layer">The layer to change the state on.</param>
        /// <param name="animatorStateData">The data about the destination state.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <returns>True if the state was changed.</returns>
        private bool ChangeAnimatorStates(int layer, AnimatorStateData animatorStateData, float normalizedTime)
        {
            // Animator state data may be null if there is no inventory and an item state is trying to be changed.
            if (animatorStateData == null) {
                return false;
            }

            // The destination state depends on the equipped item and whether or not that item specifies a lower body state name. Format the lower and upper
            // states to take the item into account.
            var destinationState = string.Empty;
            if (animatorStateData is AnimatorItemStateData) {
                var animatorItemStateData = animatorStateData as AnimatorItemStateData;
                destinationState = FormatStateName(animatorItemStateData, layer);
                // Allow item states change the root motion state. This will allow the character to stop moving when a state should be still. 
                if (!m_Controller.UseRootMotion) {
                    m_Controller.ForceItemRootMotion = animatorItemStateData.ForceRootMotion;
                }
            } else {
                destinationState = animatorStateData.Name;
            }

            return ChangeAnimatorStates(layer, destinationState, animatorStateData.TransitionDuration, animatorStateData.CanReplay, animatorStateData.SpeedMultiplier, normalizedTime);
        }

        /// <summary>
        /// Change the Animator layer to the specified state with the desired transition.
        /// </summary>
        /// <param name="layer">The layer to change the state on.</param>
        /// <param name="destinationState">The name of the destination state.</param>
        /// <param name="transitionDuration">The transtiion duration to the destination state.</param>
        /// <param name="canReplay">Can the state be replayed if it is already playing?</param>
        /// <param name="speedMultiplier">The Animator speed multiplier of the destination state.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <returns>True if the state was changed.</returns>
        private bool ChangeAnimatorStates(int layer, string destinationState, float transitionDuration, bool canReplay, float speedMultiplier, float normalizedTime)
        {
            if (!string.IsNullOrEmpty(destinationState)) {
                // Do a check to ensure the destination state is unique.
                if (!canReplay && m_ActiveStateHash[layer] == Animator.StringToHash(destinationState)) {
                    return false;
                }

                // Prevent the transition duration from being infinitely long.
                var normalizedDuration = transitionDuration / m_Animator.GetCurrentAnimatorStateInfo(layer).length;
                if (float.IsInfinity(normalizedDuration)) {
                    normalizedDuration = transitionDuration;
                }
                m_ActiveStateHash[layer] = Animator.StringToHash(destinationState);
                if (m_DebugStateChanges) {
                    Debug.Log(m_GameObject.name + " State Change - State: " + m_LayerNames[layer] + destinationState + " Duration: " + normalizedDuration + " Time: " + normalizedTime + " Frame: " + Time.frameCount);
                }

                var stateHash = GetStateNameHash(m_LayerNames[layer] + destinationState);
#if UNITY_EDITOR && !(UNITY_4_6 || UNITY_4_7)
                if (!m_Animator.HasState(layer, stateHash)) {
                    Debug.LogError("Error: Unable to transition to " + m_LayerNames[layer] + destinationState + " because the state doesn't exist.");
                    return false;
                }
#endif

#if ENABLE_MULTIPLAYER
                // Crossfade the states on all of the clients if in a network game and currently executing on the server.
                if (isServer) {
                    RpcCrossFade(stateHash, normalizedDuration, layer, normalizedTime, speedMultiplier);
                }
                // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                // in which case the method will be called with the Rpc call.
                // Also check for AnimatorInit because Unity does not syncronize when the character initially spawns.
                if (!isClient || m_AnimatorInit) {
#endif
                    CrossFade(stateHash, normalizedDuration, layer, normalizedTime, speedMultiplier);
#if ENABLE_MULTIPLAYER
                }
#endif
                return true;
            }
            return false;
        }

        /// <summary>
        /// Retrieves the hash given a state name.
        /// </summary>
        /// <param name="stateName">The state name to retrieve the hash of.</param>
        /// <returns>The Animator hash value.</returns>
        private int GetStateNameHash(string stateName)
        {
            // Use a dictionary for quick lookup.
            int hash;
            if (m_StateNamesHash.TryGetValue(stateName, out hash)) {
                return hash;
            }

            hash = Animator.StringToHash(stateName);
            m_StateNamesHash.Add(stateName, hash);
            return hash;
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Create a dynamic transition between the current state and the destination state.
        /// </summary>
        /// <param name="stateHash">The name of the destination state.</param>
        /// <param name="normalizedDuration">The duration of the transition. Value is in source state normalized time.</param>
        /// <param name="layer">Layer index containing the destination state. </param>
        /// <param name="normalizedTime">Start time of the current destination state.</param>
        [ClientRpc]
        private void RpcCrossFade(int stateHash, float normalizedDuration, int layer, float normalizedTime, float speedMultiplier)
        {
            CrossFade(stateHash, normalizedDuration, layer, normalizedTime, speedMultiplier);
        }
#endif

        /// <summary>
        /// Create a dynamic transition between the current state and the destination state.
        /// </summary>
        /// <param name="stateHash">The name of the destination state.</param>
        /// <param name="normalizedDuration">The duration of the transition. Value is in source state normalized time.</param>
        /// <param name="layer">Layer index containing the destination state. </param>
        /// <param name="normalizedTime">Start time of the current destination state.</param>
        private void CrossFade(int stateHash, float normalizedDuration, int layer, float normalizedTime, float speedMultiplier)
        {
            m_Animator.CrossFade(stateHash, normalizedDuration, layer, normalizedTime);

            // The speed value is a global value per Animator instead of per state. Only set a new speed if the layer is currently the base layer.
            if (layer == BaseLayerIndex) {
                m_Animator.speed = speedMultiplier;
            }
        }

        /// <summary>
        /// Updates the horizontal input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetHorizontalInputValue(float value)
        {
            SetHorizontalInputValue(value, m_HorizontalInputDampTime);
        }

        /// <summary>
        /// Updates the horizontal input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="dampTime">The time allowed for the parameter to reach the value.</param>
        public void SetHorizontalInputValue(float value, float dampTime)
        {
            if (value != HorizontalInputValue) {
                m_Animator.SetFloat(s_HorizontalInputHash, value, dampTime, Time.fixedDeltaTime);
                m_HorizontalInputValue = m_Animator.GetFloat(s_HorizontalInputHash);
            }
        }

        /// <summary>
        /// Updates the veritcal input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetForwardInputValue(float value)
        {
            SetForwardInputValue(value, m_ForwardInputDampTime);
        }

        /// <summary>
        /// Updates the forward input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="dampTime">The time allowed for the parameter to reach the value.</param>
        public void SetForwardInputValue(float value, float dampTime)
        {
            if (value != ForwardInputValue) {
                m_Animator.SetFloat(s_ForwardInputHash, value, dampTime, Time.fixedDeltaTime);
                m_ForwardInputValue = m_Animator.GetFloat(s_ForwardInputHash);
            }
        }

        /// <summary>
        /// Updates the yaw value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetYawValue(float value)
        {
            if (value != YawValue) {
                m_Animator.SetFloat(s_YawHash, value, 0.1f, Time.fixedDeltaTime);
                m_YawValue = m_Animator.GetFloat(s_YawHash);
            }
        }

        /// <summary>
        /// Updates the state value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetStateValue(int value)
        {
            if (value != StateValue) {
                m_StateValue = value;
                m_Animator.SetInteger(s_StateHash, value);
            }
        }

        /// <summary>
        /// Updates the int data value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetIntDataValue(int value)
        {
            if (value != IntDataValue) {
                m_IntDataValue = value;
                m_Animator.SetInteger(s_IntDataHash, value);
            }
        }

        /// <summary>
        /// Updates the float data value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetFloatDataValue(float value)
        {
            if (value != FloatDataValue) {
                m_FloatDataValue = value;
                m_Animator.SetFloat(s_FloatDataHash, value);
            }
        }

        /// <summary>
        /// Updates the float data value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="dampTime">The time allowed for the parameter to reach the value.</param>
        public void SetFloatDataValue(float value, float dampTime)
        {
            if (value != FloatDataValue) {
                var currentVelocity = m_FloatDataValue;
                m_FloatDataValue = Mathf.SmoothDamp(m_FloatDataValue, value, ref currentVelocity, dampTime);
                m_Animator.SetFloat(s_FloatDataHash, m_FloatDataValue);
            }
        }

        /// <summary>
        /// Returns the collider curve data value.
        /// </summary>
        public float GetColliderCurveData()
        {
            return m_Animator.GetFloat(s_ColliderCurveDataHash);
        }

        /// <summary>
        /// Executes an event on the EventHandler. Call the corresponding server or client method.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEvent(string eventName)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcExecuteEvent(eventName);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ExecuteEventLocal(eventName);
#if ENABLE_MULTIPLAYER
            }
#endif
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Executes an event on the EventHandler on the client.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        [ClientRpc]
        private void RpcExecuteEvent(string eventName)
        {
            ExecuteEventLocal(eventName);
        }
#endif

        /// <summary>
        /// Executes an event on the EventHandler.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        private void ExecuteEventLocal(string eventName)
        {
            EventHandler.ExecuteEvent(m_GameObject, eventName);
        }

        /// <summary>
        /// Executes an event on the EventHandler as soon as the base layer is no longer in a transition.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEventNoLowerTransition(string eventName)
        {
            if (m_Animator.IsInTransition(BaseLayerIndex)) {
                return;
            }
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcExecuteEvent(eventName);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ExecuteEventLocal(eventName);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// Executes an event on the EventHandler as soon as the upper body is no longer in a transition.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEventNoUpperTransition(string eventName)
        {
            if (m_Animator.IsInTransition(UpperLayerIndex)) {
                return;
            }
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcExecuteEvent(eventName);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ExecuteEventLocal(eventName);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// An item has been used.
        /// </summary>
        /// <param name="itemTypeIndex">The corresponding index of the item used.</param>
        public void ItemUsed(int itemTypeIndex)
        {
            System.Type itemType;
            if (itemTypeIndex == 0) {
                itemType = typeof(PrimaryItemType);
            } else if (itemTypeIndex == 1) {
                itemType = typeof(SecondaryItemType);
            } else { // DualWieldItemType.
                itemType = typeof(DualWieldItemType);
            }
            EventHandler.ExecuteEvent<System.Type>(m_GameObject, "OnAnimatorItemUsed", itemType);
        }

        /// <summary>
        /// An dual wielded item has been changed.
        /// </summary>
        /// <param name="item">The dual wield item. Can be null.</param>
        private void OnDualWieldItemChange(Item item)
        {
            DetermineStates();
        }

        /// <summary>
        /// The character has died. Stop responding to state changes.
        /// </summary>
        private void OnDeath()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
        }

        /// <summary>
        /// The character has respawned. Play the default states.
        /// </summary>
        private void OnRespawn()
        {
            // The animator may have been disabled by the ragdoll so ensure it is enabled.
            m_Animator.enabled = true;
            PlayDefaultStates();
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }
    }
}