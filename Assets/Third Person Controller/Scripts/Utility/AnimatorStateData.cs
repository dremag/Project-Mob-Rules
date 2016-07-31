using UnityEngine;
using System;
using Opsive.ThirdPersonController.Abilities;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A small class that stores the state name and the amount of time that it takes to transition to that state.
    /// </summary>
    [System.Serializable]
    public class AnimatorStateData
    {
        [Tooltip("The name of the state")]
        [SerializeField] protected string m_Name = "Movement";
        [Tooltip("The time it takes to transition to the state")]
        [SerializeField] protected float m_TransitionDuration = 0.2f;
        [Tooltip("The Animator multiplier of the state")]
        [SerializeField] protected float m_SpeedMultiplier = 1;
        [Tooltip("Can the animation be replayed while it is already playing?")]
        [SerializeField] protected bool m_CanReplay;

        // Exposed properties
        public string Name { get { return m_Name; } }
        public float TransitionDuration { get { return m_TransitionDuration; } }
        public float SpeedMultiplier { get { return m_SpeedMultiplier; } }
        public bool CanReplay { get { return m_CanReplay; } }

        /// <summary>
        /// Constructor for AnimatorStateData.
        /// </summary>
        public AnimatorStateData(string name, float transitionDuration)
        {
            m_Name = name;
            m_TransitionDuration = transitionDuration;

            m_TransitionDuration = 0.2f;
            m_SpeedMultiplier = 1;
        }
    }

    /// <summary>
    /// Extends AnimatorStateData to store data specific to the item states.
    /// </summary>
    [System.Serializable]
    public class AnimatorItemStateData : AnimatorStateData
    {
        /// <summary>
        /// Specifies the layer that the state can play within.
        /// </summary>
        public enum AnimatorLayer { Base = 1,
                            UpperBody = 2,
                            LeftArm = 4,
                            RightArm = 8,
                            LeftHand = 16,
                            RightHand = 32
        }

        [Tooltip("Should the Item name be added to the start of the state name?")]
        [SerializeField] protected bool m_ItemNamePrefix;
        [Tooltip("Specifies the layers that the state can use")]
        [SerializeField] protected AnimatorLayer m_Layer = AnimatorLayer.UpperBody;
        [Tooltip("Should states with a lower item priority be ignored?")]
        [SerializeField] protected bool m_IgnoreLowerPriority;
        [Tooltip("Should the animation force root motion? Only applies if the Layer is using the base layer")]
        [SerializeField] protected bool m_ForceRootMotion;

        // Internal variables
        private ItemType m_ItemType;
        private Ability m_Ability;

        // Exposed properties
        public bool ItemNamePrefix { get { return m_ItemNamePrefix; } set { m_ItemNamePrefix = false; } }
        public AnimatorLayer Layer { set { m_Layer = value; } }
        public bool IgnoreLowerPriority { get { return m_IgnoreLowerPriority; } set { m_IgnoreLowerPriority = value; } }
        public bool ForceRootMotion { get { return m_ForceRootMotion; } }
        public ItemType ItemType { get { return m_ItemType; } set { m_ItemType = value; } }
        public Ability Ability { get { return m_Ability; } set { m_Ability = value; } }

        /// <summary>
        /// Constructor for AnimatorItemStateData.
        /// </summary>
        public AnimatorItemStateData(string name, float transitionDuration, bool itemNamePrefix)
            : base(name, transitionDuration)
        {
            m_ItemNamePrefix = itemNamePrefix;
        }

        /// <summary>
        /// Is the state within the specified layer?
        /// </summary>
        /// <param name="layer">The layer index to check against.</param>
        /// <returns>True if the state is within the layer.</returns>
        public bool IsStateWithinLayer(int layer)
        {
            return Utility.InLayerMask(layer, (int)m_Layer);
        }
    }

    /// <summary>
    /// Represents an array of AnimatorItemStateData. Can specify how to transition from one item state to another.
    /// </summary>
    [System.Serializable]
    public class AnimatorItemGroupData
    {
        /// <summary>
        /// Specifies how to change from one state to next.
        /// </summary>
        public enum Order { Random, // Randomlly choose a state within the array.
                            Sequential, // Move from the first element to the second, third, etc.
                            Combo, // Moves sequentually until the timeout is reached, then resets back to the start.
        }

        [Tooltip("Specifies how the Animator should transition from one state to another")]
        [SerializeField] protected Order m_StateOrder;
        [Tooltip("The amount of time that the next combo state must be run before resetting back to the start")]
        [SerializeField] protected float m_ComboTimeout = 1;
        [Tooltip("The list of states to cycle through")]
        [SerializeField] protected AnimatorItemStateData[] m_States;

        // Exposed properties
        public Order StateOrder { get { return m_StateOrder; } }
        public float ComboTimeout { get { return m_ComboTimeout; } }
        // Exposed properties for Item Builder
        public AnimatorItemStateData[] States { get { return m_States; } }

        // Internal variables
        private int m_NextStateIndex;
        [NonSerialized] private AnimatorItemCollectionData m_ParentCollection; // Must be NonSerialized or Unity will cause an infinite loop.

        /// <summary>
        /// Constructor for AnimatorItemGroupData.
        /// </summary>
        public AnimatorItemGroupData(string name, float transitionDuration, bool itemNamePrefix)
        {
            m_States = new AnimatorItemStateData[] { new AnimatorItemStateData(name, transitionDuration, itemNamePrefix) };
        }

        /// <summary>
        /// Initializes the AnimatorItemGroupData.
        /// </summary>
        /// <param name="parentCollection">The AnimatorItemCollectionData that represents the group.</param>
        /// <param name="itemType">The ItemType that represents the group.</param>
        /// <param name="ability">The Ability that represents the group.</param>
        public void Initialize(AnimatorItemCollectionData parentCollection, ItemType itemType, Ability ability)
        {
            m_ParentCollection = parentCollection;

            for (int i = 0; i < m_States.Length; ++i) {
                m_States[i].ItemType = itemType;
                m_States[i].Ability = ability;
            }
        }

        /// <summary>
        /// Returns the state in the array.
        /// </summary>
        /// <param name="layer">The layer to get the state of.</param>
        /// <returns>The state in the array.</returns>
        public AnimatorItemStateData GetState(int layer)
        {
            var nextStateIndex = m_StateOrder == Order.Random ? m_NextStateIndex : m_ParentCollection.NextStateIndex % m_States.Length;
            if (m_StateOrder == Order.Combo && m_ParentCollection.LastComboRetirevalTime + m_ComboTimeout < Time.time) {
                // Reset the state index if the retrieval time of the next state isn't faster than the timeout. This will force
                // the combo to reset back to the start.
                nextStateIndex = 0;
                m_ParentCollection.ResetNextState();
            }

            var itemState = m_States[nextStateIndex];
            if (itemState != null && itemState.IsStateWithinLayer(layer)) {
                return itemState;
            }
            return null;
        }

        /// <summary>
        /// Advance to the next state.
        /// </summary>
        public void NextState()
        {
            if (m_StateOrder == Order.Random) {
                m_NextStateIndex = UnityEngine.Random.Range(0, m_States.Length);
            }
        }
    }

    /// <summary>
    /// Contains an array of AnimatorItemGroupData elements. Allows for multiple sets of item states.
    /// </summary>
    [System.Serializable]
    public class AnimatorItemSetData
    {
        /// <summary>
        /// Specifies how to change from between the item states.
        /// </summary>
        public enum Order
        {
            Random, // Randomlly choose a group.
            Sequential, // Move from the first element to the second, third, etc.
        }
        [Tooltip("A list of the available item groups")]
        [SerializeField] protected AnimatorItemGroupData[] m_Groups;
        [Tooltip("Specifies how to change between the item states")]
        [SerializeField] protected Order m_GroupOrder;

        // Exposed properties for Item Builder
        public AnimatorItemGroupData[] Groups { get { return m_Groups; } }

        // Internal variables
        private int m_NextGroupIndex;

        /// <summary>
        /// Consturctor for AnimatorItemSetData.
        /// </summary>
        public AnimatorItemSetData(string name, float transitionDuration, bool itemNamePrefix)
        {
            m_Groups = new AnimatorItemGroupData[] { new AnimatorItemGroupData(name, transitionDuration, itemNamePrefix) };
        }

        /// <summary>
        /// Initializes the AnimatorItemSetData.
        /// </summary>
        /// <param name="parentCollection">The AnimatorItemCollectionData that represents the group.</param>
        /// <param name="itemType">The ItemType that represents the group.</param>
        public virtual void Initialize(AnimatorItemCollectionData parentCollection, ItemType itemType)
        {
            for (int i = 0; i < m_Groups.Length; ++i) {
                m_Groups[i].Initialize(parentCollection, itemType, null);
            }
        }

        /// <summary>
        /// Returns the next AnimatorItemGroupData.
        /// </summary>
        /// <returns>The next AnimatorItemGroupData.</returns>
        public AnimatorItemGroupData GetStates()
        {
            return m_Groups[m_NextGroupIndex];
        }

        /// <summary>
        /// Advance to the next state.
        /// </summary>
        public void NextState()
        {
            m_Groups[m_NextGroupIndex].NextState();
        }

        /// <summary>
        /// The next state index should be reset back to the beginning.
        /// </summary>
        public void ResetNextState()
        {
            if (m_GroupOrder == Order.Random) {
                m_NextGroupIndex = UnityEngine.Random.Range(0, m_Groups.Length);
            } else {
                m_NextGroupIndex++;
                if (m_NextGroupIndex >= m_Groups.Length) {
                    m_NextGroupIndex = 0;
                }
            }
        }
    }

    /// <summary>
    /// Extends AnimatorItemSetData by allowing a particular item state to belong to an Ability.
    /// </summary>
    [Serializable]
    public class AnimatorItemAbilitySetData : AnimatorItemSetData
    {
        [Tooltip("Specifies the ability that should be active when the states can play")]
        [SerializeField] protected Ability m_Ability;

        // Exposed Properties
        public Ability Ability { get { return m_Ability; } }

        /// <summary>
        /// Constructor for AnimatorItemAbilitySetData.
        /// </summary>
        public AnimatorItemAbilitySetData(string name, float transitionDuration, bool itemNamePrefix) : base(name, transitionDuration, itemNamePrefix) { }

        /// <summary>
        /// Initializes the AnimatorItemAbilitySetData.
        /// </summary>
        /// <param name="parentCollection">The AnimatorItemCollectionData that represents the set.</param>
        /// <param name="itemType">The ItemType that represents the set.</param>
        public override void Initialize(AnimatorItemCollectionData parentCollection, ItemType itemType)
        {
            for (int i = 0; i < m_Groups.Length; ++i) {
                m_Groups[i].Initialize(parentCollection, itemType, m_Ability);
            }
        }
    }

    /// <summary>
    /// Organizes a set of AnimatorItemGroupData into one parent object. 
    /// </summary>
    [System.Serializable]
    public class AnimatorItemCollectionData
    {
        [Tooltip("The states for when idle")]
        [SerializeField] protected AnimatorItemSetData m_Idle;
        [Tooltip("The states for when moving")]
        [SerializeField] protected AnimatorItemSetData m_Movement;
        [Tooltip("The states for when an ability is active")]
        [SerializeField] protected AnimatorItemAbilitySetData[] m_Abilities;

        // Internal variables
        private AnimatorItemSetData m_ActiveState;
        private int m_NextStateIndex;
        private float m_LastRetrievalTime = -1;

        // Exposed properties
        public int NextStateIndex { get { return m_NextStateIndex; } }
        public float LastComboRetirevalTime { get { return m_LastRetrievalTime; } }
        public float LastUpperBodyStateTransition { get { return m_ActiveState.GetStates().GetState(1).TransitionDuration; } }
        // Exposed properties for Item Builder
        public AnimatorItemSetData Idle { get { return m_Idle; } }
        public AnimatorItemSetData Movement { get { return m_Movement; } }

        /// <summary>
        /// Constructor for AnimatorItemCollectionData.
        /// </summary>
        public AnimatorItemCollectionData(string idleName, string movementName, float transitionDuration, bool itemNamePrefix)
        {
            m_Idle = new AnimatorItemSetData(idleName, transitionDuration, itemNamePrefix);
            m_Movement = new AnimatorItemSetData(movementName, transitionDuration, itemNamePrefix);
            m_Abilities = new AnimatorItemAbilitySetData[] { };
        }

        /// <summary>
        /// Initializes the AnimatorItemCollectionData to its starting values.
        /// </summary>
        /// <param name="itemType">The ItemType that represents the collection.</param>
        public void Initialize(ItemType itemType)
        {
            m_Idle.Initialize(this, itemType);
            m_Movement.Initialize(this, itemType);

            if (m_Abilities != null && m_Abilities.Length > 0) {
                for (int i = 0; i < m_Abilities.Length; ++i) {
                    // Arrange the ability states according to the ability priority. This allows the higher priority abilities to be able to specify the state name prefix
                    // ahead of the lower priority abilities.
                    Array.Sort(m_Abilities, delegate (AnimatorItemAbilitySetData state1, AnimatorItemAbilitySetData state2) {
                        if (state1.Ability == null || state2.Ability == null) {
                            return 0;
                        }
                        return state1.Ability.Index.CompareTo(state2.Ability.Index);
                    });

                    m_Abilities[i].Initialize(this, itemType);
                }
            }
        }

        /// <summary>
        /// Returns the AnimatorItemStateData of the given AnimatorItemGroupData.
        /// </summary>
        /// <param name="layer">The layer to get the state of.</param>
        /// <param name="moving">Is the character moving?</param>
        /// <returns>The AnimatorItemStateData of the given AnimatorItemGroupData. Can be null.</returns>
        public AnimatorItemStateData GetState(int layer, bool moving)
        {
            var abilityStates = m_Abilities;
            if (abilityStates != null) {
                // Abilities have the highest priority.
                for (int i = 0; i < abilityStates.Length; ++i) {
                    if (abilityStates[i].Ability.IsActive) {
                        var state = GetState(layer, abilityStates[i]);
                        if (state != null) {
                            return state;
                        }
                    }
                }
            }
            if (moving) {
                // Moving has the next highest priority.
                var state = GetState(layer, m_Movement);
                if (state != null) {
                    return state;
                }
            } else {
                // Idle has the lowest priority.
                var state = GetState(layer, m_Idle);
                if (state != null) {
                    return state;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the AnimatorItemStateData of the given AnimatorItemGroupData.
        /// </summary>
        /// <param name="layer">The layer to get the state of.</param>
        /// <param name="itemStates">The AnimatorItemGroupData to get the state of.</param>
        /// <returns>The AnimatorItemStateData of the given AnimatorItemGroupData. Can be null.</returns>
        private AnimatorItemStateData GetState(int layer, AnimatorItemSetData itemStates)
        {
            var stateGroup = itemStates.GetStates();
            if (stateGroup != null) {
                var state = stateGroup.GetState(layer);
                if (state != null) {
                    // Keep a reference to the active state for the NextState and ResetState callbacks.
                    m_ActiveState = itemStates;
                    return state;
                }
            }
            return null;
        }

        /// <summary>
        /// Advance to the next state.
        /// </summary>
        public void NextState()
        {
            m_NextStateIndex++;
            m_LastRetrievalTime = Time.time;

            if (m_ActiveState != null) {
                m_ActiveState.NextState();
            }
        }

        /// <summary>
        /// The next state index should be reset back to the beginning.
        /// </summary>
        public void ResetNextState()
        {
            m_NextStateIndex = 0;
            m_LastRetrievalTime = -1;

            if (m_ActiveState != null) {
                m_ActiveState.ResetNextState();
            }
        }
    }
}