using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Base class for any item that can attack
    /// </summary>
    public abstract class Weapon : Item, IUseableItem
    {
        [Tooltip("The state while using the item")]
        [SerializeField] protected AnimatorItemCollectionData m_UseStates = new AnimatorItemCollectionData("Attack", "Attack", 0.1f, true);
        [Tooltip("Can the item be used in the air?")]
        [SerializeField] protected bool m_CanUseInAir = true;
        
        // Exposed properties for the Item Builder
        public AnimatorItemCollectionData UseStates { get { return m_UseStates; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Init(Inventory inventory)
        {
            base.Init(inventory);

            // Initialize the animation states.
            m_UseStates.Initialize(m_ItemType);
        }

        /// <summary>
        /// Perform any cleanup when the item is disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            // The animation states should begin fresh.
            m_UseStates.ResetNextState();
        }

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
                if (InUse()) {
                    var useState = m_UseStates.GetState(layer, m_Controller.Moving);
                    if (useState != null) {
                        return useState;
                    }
                }
            }
            return base.GetDestinationState(highPriority, layer);
        }

        /// <summary>
        /// Try to perform the use. Depending on the weapon this may not always succeed. For example, if the user is trying to shoot a weapon that was shot a half
        /// second ago cannot be used if the weapon can only be fired once per second.
        /// <returns>True if the item was used.</returns>
        /// </summary>
        public virtual bool TryUse() { return false; }

        /// <summary>
        /// Can the weapon be used?
        /// </summary>
        /// <returns>True if the weapon can be used.</returns>
        public virtual bool CanUse()
        {
            if (!m_CanUseInAir && !m_Controller.Grounded) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Is the weapon currently in use?
        /// </summary>
        /// <returns>True if the weapon is in use.</returns>
        public virtual bool InUse() { return false; }

        /// <summary>
        /// Stop the weapon from being used. This may not always succeed. For example, a melee weapon cannot be interrupted if it is already in the middle of its motion. 
        /// </summary>
        public virtual void TryStopUse() { }

        /// <summary>
        /// Stop the item from being used. This may not always succeed. For example, a melee weapon cannot be interrupted if it is already in the middle of its motion. 
        /// </summary>
        public virtual void Used() { }

        /// <summary>
        /// The item is being unequipped. Stop the item from being used.
        /// </summary>
        protected override void OnItemUnequipping()
        {
            TryStopUse();

            base.OnItemUnequipping();
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        protected override void OnAim(bool aim)
        {
            base.OnAim(aim);

            if (!aim) {
                // When the character is no longer aiming reset the animation states so they will begin fresh.
                m_UseStates.ResetNextState();
            }
        }
    }
}