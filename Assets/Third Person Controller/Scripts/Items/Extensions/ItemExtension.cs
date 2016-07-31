using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// An item extension is the base class of anything that can add an extension to the parent item.
    /// Examples include having an assault rifle that primarily fires bullets, but can also shoot grenades (using ShootableWeaponExtension) and melee (using MeleeWeaponExtension).
    /// </summary>
    public abstract class ItemExtension : MonoBehaviour
    {
        [Tooltip("A reference to the consumable item type")]
        [SerializeField] protected ConsumableItemType m_ConsumableItemType;
        [Tooltip("The input name mapped to use the item extension")]
        [SerializeField] protected string m_UseInputName = "Extension Fire1";

        // Exposed properties
        public ConsumableItemType ConsumableItemType { get { return m_ConsumableItemType; } }
        public string UseInputName { get { return m_UseInputName; } }
        public Item ParentItem { get { return m_ParentItem; } }

        // SharedFields
        protected SharedProperty<Item> m_CurrentDualWieldItem = null;
#if ENABLE_MULTIPLAYER
        protected SharedMethod<bool> m_IsServer = null;
        protected SharedMethod<bool> m_IsLocalPlayer = null;
#endif

        // Component references
        private Transform m_HandTransform;
        protected GameObject m_GameObject;
        protected Transform m_Transform;
        protected AnimatorMonitor m_AnimatorMonitor;
        protected GameObject m_Character;
        protected RigidbodyCharacterController m_Controller;
        protected Inventory m_Inventory;
        protected Item m_ParentItem;
#if ENABLE_MULTIPLAYER
        protected NetworkMonitor m_NetworkMonitor;
#endif

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public virtual void Awake()
        {
            // Awake may have already been called if not on the network.
            if (m_HandTransform != null) {
                return;
            }

            // The parent of ItemPlacement will be a bone transform.
            m_HandTransform = GetComponentInParent<ItemPlacement>().transform.parent;

            m_GameObject = gameObject;
            m_Transform = transform;
        }
        
        /// <summary>
        /// Register for any events that the item extension should be aware of.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (m_Character != null) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }
        }

        /// <summary>
        /// Perform any cleanup when the item extension is disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (m_Character != null) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }
        }

        /// <summary>
        /// Initializes the item after it has been added to the Inventory.
        /// </summary>
        /// <param name="item">The item that this extension belongs to.</param>
        /// <param name="inventory">The parent character's inventory.</param>
        public virtual void Init(Item item, Inventory inventory)
        {
            m_ParentItem = item;
            m_Inventory = inventory;
            m_Character = inventory.gameObject;
#if ENABLE_MULTIPLAYER
            m_NetworkMonitor = m_Character.GetComponent<NetworkMonitor>();
#endif
            m_AnimatorMonitor = inventory.GetComponent<AnimatorMonitor>();
            m_Controller = inventory.GetComponent<RigidbodyCharacterController>();

            // Register for character events if the GameObject is active. OnEnable normally registers for these callbacks but in this case OnEnable has already occurred.
            if (gameObject.activeSelf) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }

            SharedManager.InitializeSharedFields(m_Character, this);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="highPriority">Should the high priority animation be retrieved? High priority animations get tested before character movement.</param>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. A null value indicates no change.</returns>
        public virtual AnimatorItemStateData GetDestinationState(bool highPriority, int layer)
        {
            return null;
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        protected virtual void OnAim(bool aim)
        {

        }
    }
}