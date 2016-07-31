using UnityEngine;
using System;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the current Item. 
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class ItemHandler : NetworkBehaviour
#else
    public class ItemHandler : MonoBehaviour
#endif
    {
        [Tooltip("Can the item be reloaded through a button map?")]
        [SerializeField] protected bool m_CanReloadItem = true;
        [Tooltip("Can the secondary item be used through a button map?")]
        [SerializeField] protected bool m_CanUseSecondaryItem = true;
        [Tooltip("Can the flashlight be toggled through a button map?")]
        [SerializeField] protected bool m_CanToggleFlashlight = true;
        [Tooltip("Can the laser sight be toggled through a button map?")]
        [SerializeField] protected bool m_CanToggleLaserSight = true;

        // SharedFields
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedProperty<Item> m_CurrentSecondaryItem = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;
        private SharedMethod<Item, bool> m_TryUseItem = null;
        private SharedMethod<bool> m_IndependentLook = null;
        private SharedMethod<bool> m_CanInteractItem = null;
        private SharedMethod<bool> m_CanUseItem = null;

        // Internal variables
        private enum UseType { None, Primary, DualWield, /* Empty value to make bitwise operations work */ PrimaryFiller, Secondary}
        private UseType m_UseType = UseType.None;
        private IUseableItem m_ItemUsePending;
        private bool m_StopUse;
        private bool m_Aiming;
        private bool m_AllowGameplayInput = true;
        private bool m_HasDualWieldItem;
        private bool m_SecondaryInputWait;

        // Component references
        private GameObject m_GameObject;
        private PlayerInput m_PlayerInput;
        private RigidbodyCharacterController m_Controller;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_PlayerInput = GetComponent<PlayerInput>();
            m_Controller = GetComponent<RigidbodyCharacterController>();

            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
            EventHandler.RegisterEvent<Item>(m_GameObject, "OnInventoryPrimaryItemChange", PrimaryItemChange);
            EventHandler.RegisterEvent<Type>(m_GameObject, "OnAnimatorItemUsed", OnUsed);
            EventHandler.RegisterEvent<Abilities.Ability>(m_GameObject, "OnAbilityStart", OnAbilityStart);
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Initialize the SharedFields so they are ready for when the server starts to send RPC calls.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            SharedManager.InitializeSharedFields(m_GameObject, this);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowInventoryInput", AllowGameplayInput);
        }
#endif

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            // The SharedFields would have already been initialized if in a network game.
            if (m_CurrentPrimaryItem == null) {
                SharedManager.InitializeSharedFields(m_GameObject, this);
                EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
                EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowInventoryInput", AllowGameplayInput);
            }

            // An AI Agent does not use PlayerInput so Update does not need to run.
            if (GetComponent<PlayerInput>() == null) {
                enabled = false;
            }
        }

        /// <summary>
        /// Notify the item that the user wants to perform an action.
        /// </summary>
        private void Update()
        {
            if (!m_AllowGameplayInput) {
                return;
            }

#if ENABLE_MULTIPLAYER
            if (!isLocalPlayer) {
                return;
            }
#endif

            // Try to use the item.
            var hasDualWieldItem = m_CurrentDualWieldItem.Get() != null && m_CurrentDualWieldItem.Get() is IUseableItem;
            var useType = UseType.None;
            int extensionIndex = -1;
            if (hasDualWieldItem) {
                // Use a different mapping when dual wielded items exist.
                if (m_PlayerInput.GetButtonDown(Constants.DualWieldLeftUseInputName, true)) {
                    useType |= UseType.DualWield;
                }
                if (m_PlayerInput.GetButtonDown(Constants.DualWieldRightUseInputName, true)) {
                    useType |= UseType.Primary;
                }
            } else {
                if (m_PlayerInput.GetButtonDown(m_Controller.Movement != RigidbodyCharacterController.MovementType.PointClick ? Constants.UseInputName : Constants.DualWieldRightUseInputName, true)) {
                    useType |= UseType.Primary;
                } else {
                    // The extension item may be able to be used.
                    var primaryItem = m_CurrentPrimaryItem.Get();
                    if (primaryItem != null && primaryItem.ItemExtensions != null) {
                        for (int i = 0; i < primaryItem.ItemExtensions.Length; ++i) {
                            if (m_PlayerInput.GetButtonDown(primaryItem.ItemExtensions[i].UseInputName)) {
                                useType |= UseType.Primary;
                                extensionIndex = i;
                                break;
                            }
                        }
                    }
                }
            }
            if (useType != UseType.None) {
#if ENABLE_MULTIPLAYER
                if (extensionIndex != -1) {
                    CmdTryUseItemExtension(useType, extensionIndex);
                } else {
                    CmdTryUseItem(useType);
                }
#else
                TryUseItem(useType, extensionIndex);
#endif
            // Stop the use as soon as the player releases the Use input.
            } else {
                var stopUse = false;
                var stopPrimaryItem = true;
                if (hasDualWieldItem) {
                    if (m_PlayerInput.GetButtonUp(Constants.DualWieldLeftUseInputName, true)) {
                        stopUse = true;
                        stopPrimaryItem = false;
                    }
                    if (m_PlayerInput.GetButtonUp(Constants.DualWieldRightUseInputName, true)) {
                        stopUse = true;
                    }
                } else {
                    if (m_PlayerInput.GetButtonUp(m_Controller.Movement != RigidbodyCharacterController.MovementType.PointClick ? Constants.UseInputName : Constants.DualWieldRightUseInputName, true)) {
                        stopUse = true;
                    } else {
                        // The extension item may be able to be stopped.
                        var primaryItem = m_CurrentPrimaryItem.Get();
                        if (primaryItem != null && primaryItem.ItemExtensions != null) {
                            for (int i = 0; i < primaryItem.ItemExtensions.Length; ++i) {
                                if (m_PlayerInput.GetButtonUp(primaryItem.ItemExtensions[i].UseInputName, true)) {
                                    stopUse = true;
                                    extensionIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (stopUse) {
#if ENABLE_MULTIPLAYER
                    if (extensionIndex != -1) {
                        CmdTryStopExtensionUse(stopPrimaryItem, extensionIndex);
                    } else {
                        CmdTryStopUse(stopPrimaryItem);
                    }
#else
                    TryStopUse(stopPrimaryItem, extensionIndex);
#endif
                }
            }

            // Reload the item if the item can be reloaded.
            if (m_CanReloadItem && m_PlayerInput.GetButtonDown(Constants.ReloadInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryReload();
#else
                TryReload();
#endif
            }

            // Use the Secondary Item if there is no dual wield item.
            if (m_CanUseSecondaryItem) {
                if (m_CurrentDualWieldItem.Get() == null && !m_SecondaryInputWait && m_PlayerInput.GetButton(Constants.UseSecondaryInputName, true)) {
                    // The drop dual wield item may be mapped to the same input as the secondary item. Prevent the secondary item from being used until the button has returned
                    // to the up position.
                    if (!m_HasDualWieldItem && !m_SecondaryInputWait) {
#if ENABLE_MULTIPLAYER
                    CmdTryUseItem(UseType.Secondary);
#else
                        TryUseItem(UseType.Secondary);
#endif
                    } else {
                        m_SecondaryInputWait = true;
                    }
                } else if (m_PlayerInput.GetButtonUp(Constants.UseSecondaryInputName, true)) {
                    m_SecondaryInputWait = false;
                }
            }

            if (m_CanToggleFlashlight && m_PlayerInput.GetButtonDown(Constants.ToggleFlashlightInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryToggleFlashlight();
#endif
                TryToggleFlashlight();
            }

            if (m_CanToggleLaserSight && m_PlayerInput.GetButtonDown(Constants.ToggleLaserSightInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryToggleLaserSight();
#endif
                TryToggleLaserSight();
            }

            // The Update execution order isn't guarenteed to be in any sort of order. Store if the item has been dual wielded to allow the secondary input to use the value from the last
            // frame to prevent the InventoryHandler from dropping the item in the current frame and the ItemHandler thinking that it has already been dropped.
            m_HasDualWieldItem = hasDualWieldItem;
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Tries to use the specified item extension on the server.
        /// </summary>
        /// <param name="useType">Specifies the type of item that should be used.</param>
        /// <param name="extensionIndex">Specifies the index of the extension that should be used.</param>
        /// <returns>True if the item was used.</returns>
        [Command]
        private void CmdTryUseItemExtension(UseType useType, int extensionIndex)
        {
            TryUseItem(useType, extensionIndex);
        }

        /// <summary>
        /// Tries to use the specified item on the server. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="useType">Specifies the type of item that should be used.</param>
        /// <returns>True if the item was used.</returns>
        [Command]
        private void CmdTryUseItem(UseType useType)
        {
            TryUseItem(useType);
        }

        /// <summary>
        /// Tries to stop the active item extension from being used on the server.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be stopped?</param>
        /// <param name="extensionIndex">Specifies the index of the extension that should be stopped.</param>
        [Command]
        private void CmdTryStopExtensionUse(bool primaryItem, int extensionIndex)
        {
            TryStopUse(primaryItem, extensionIndex);
        }

        /// <summary>
        /// Tries to stop the active item from being used on the server.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be stopped?</param>
        [Command]
        private void CmdTryStopUse(bool primaryItem)
        {
            TryStopUse(primaryItem);
        }

        /// <summary>
        /// Tries to reload the item on the server.
        /// </summary>
        [Command]
        private void CmdTryReload()
        {
            TryReload();
        }

        /// <summary>
        /// Tries to toggle the flashlight on the server.
        /// </summary>
        [Command]
        private void CmdTryToggleFlashlight()
        {
            RpcTryToggleFlashlight();
        }

        /// <summary>
        /// Tries to toggle the flashlight on the clients.
        /// </summary>
        [ClientRpc]
        private void RpcTryToggleFlashlight()
        {
            // The flashlight would have already been toggled if a local player.
            if (isLocalPlayer) {
                return;
            }
            TryToggleFlashlight();
        }

        /// <summary>
        /// Tries to toggle the laser sight on the clients.
        /// </summary>
        [Command]
        private void CmdTryToggleLaserSight()
        {
            RpcTryToggleLaserSight();
        }

        [ClientRpc]
        private void RpcTryToggleLaserSight()
        {
            // The laser sight would have already been toggled if a local player.
            if (isLocalPlayer) {
                return;
            }
            TryToggleLaserSight();
        }
#endif

        /// <summary>
        /// Tries to use the specified item. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="useType">Should the primary item and/or the dual wielded item be used?</param>
        /// <returns>True if the item was used.</returns>
        public bool TryUseItem(Type itemType)
        {
            UseType useType;
            if (typeof(PrimaryItemType).IsAssignableFrom(itemType)) {
                useType = UseType.Primary;
            } else if (typeof(DualWieldItemType).IsAssignableFrom(itemType)) {
                useType = UseType.DualWield;
            } else {
                useType = UseType.Secondary;
            }
            return TryUseItem(useType);
        }

        /// <summary>
        /// Tries to use the specified item. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="useType">Specifies the type of item that should be used.</param>
        /// <returns>True if the item was used.</returns>
        private bool TryUseItem(UseType useType)
        {
            return TryUseItem(useType, -1);
        }

        /// <summary>
        /// Tries to use the specified item. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="useType">Specifies the type of item that should be used.</param>
        /// <param name="extensionIndex">Specifies the index of the extension that should be used.</param>
        /// <returns>True if the item was used.</returns>
        private bool TryUseItem(UseType useType, int extensionIndex)
        {
            // Return early if the item cannot be interacted with or used.
            if (!m_CanInteractItem.Invoke() || !m_CanUseItem.Invoke()) {
                return false;
            }
            IUseableItem useableItem = null;
            var primaryItem = true;
            if (((int)useType & (int)UseType.Primary) == (int)UseType.Primary) {
                useableItem = m_CurrentPrimaryItem.Get() as IUseableItem;
            } else if (((int)useType & (int)UseType.DualWield) == (int)UseType.DualWield) {
                useableItem = m_CurrentDualWieldItem.Get() as IUseableItem;
            } else if (((int)useType & (int)UseType.Secondary) == (int)UseType.Secondary) {
                useableItem = m_CurrentSecondaryItem.Get() as IUseableItem;
                primaryItem = false;
            }
            if (useableItem != null && useableItem.CanUse()) {
                // If the extension index isn't -1 then use the extension item.
                if (extensionIndex != -1) {
                    useableItem = (useableItem as Item).ItemExtensions[extensionIndex] as IUseableItem;
                    if (useableItem == null || !useableItem.CanUse()) {
                        return false;
                    }
                }
                if (primaryItem) {
                    // The UseType should always be updated.
                    m_UseType |= useType;
                    if (m_ItemUsePending == null) {
                        // The SharedMethod TryUseItem will return failure if the item cannot be used for any reason, such as a weapon not being aimed. If this happens
                        // register for the event which will let us know when the item is ready to be used.
                        m_ItemUsePending = useableItem;
                        var item = useableItem as Item;
                        if (item == null) {
                            item = (useableItem as ItemExtension).ParentItem;
                        }
                        if (m_TryUseItem.Invoke(item)) {
                            ReadyForUse();
                        } else {
                            EventHandler.RegisterEvent(m_GameObject, "OnItemReadyForUse", ReadyForUse);
                        }
                    }
                    return true;
                } else {
                    if (useableItem.TryUse()) {
                        // After the item is used the character may no longer be alive so don't execuate the events.
                        if (enabled || m_IndependentLook.Invoke()) {
                            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnUpdateAnimator", false);
                        }
                        return true;
                    }
                }
            }

            return false;
        }
        
        /// <summary>
        /// The primary item isn't always ready when the user wants to use it. For example, the primary item may be a weapon and that weapon needs to aim
        /// before it can fire. ReadyForUse will be called when the item is ready to be used.
        /// </summary>
        private void ReadyForUse()
        {
            // No longer need to listen to the event.
            EventHandler.UnregisterEvent(m_GameObject, "OnItemReadyForUse", ReadyForUse);
            // Try to use the item.
            if (m_ItemUsePending != null) {
                m_ItemUsePending.TryUse();

                // The item may have been stopped in the time that it took for the item to be ready. Let the item be used once and then stop the use.
                if (m_StopUse) {
                    m_ItemUsePending.TryStopUse();
                }
            }

            m_UseType = UseType.None;
            m_ItemUsePending = null;
            m_StopUse = false;
        }

        /// <summary>
        /// Callback from the Animator. Will be called when an item is registered for the Used callback.
        /// </summary>
        /// <param name="itemType">The type of item used.</param>
        private void OnUsed(Type itemType)
        {
            IUseableItem item;
            if (itemType.Equals(typeof(PrimaryItemType))) {
                item = m_CurrentPrimaryItem.Get() as IUseableItem;
            } else if (itemType.Equals(typeof(SecondaryItemType))) {
                item = m_CurrentSecondaryItem.Get() as IUseableItem;
            } else { // DualWieldItemType.
                item = m_CurrentDualWieldItem.Get() as IUseableItem;
            }
            if (item != null) {
                if (item.InUse()) {
                    item.Used();
                } else {
                    // The main item isn't in use. Check all of the item extensions.
                    var itemExtensions = (item as Item).ItemExtensions;
                    for (int i = 0; i < itemExtensions.Length; ++i) {
                        var useableExtension = itemExtensions[i] as IUseableItem;
                        if (useableExtension != null && useableExtension.InUse()) {
                            useableExtension.Used();
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        private void OnAim(bool aim)
        {
            m_Aiming = aim;
            Item item;
            if ((item = m_CurrentPrimaryItem.Get()) != null) {
                if (item is IFlashlightUseable) {
                    (item as IFlashlightUseable).ActivateFlashlightOnAim(aim);
                }
                if (item is ILaserSightUseable) {
                    (item as ILaserSightUseable).ActivateLaserSightOnAim(aim);
                }
            }
            if ((item = m_CurrentDualWieldItem.Get()) != null) {
                if (item is IFlashlightUseable) {
                    (item as IFlashlightUseable).ActivateFlashlightOnAim(aim);
                }
                if (item is ILaserSightUseable) {
                    (item as ILaserSightUseable).ActivateLaserSightOnAim(aim);
                }
            }
        }

        /// <summary>
        /// Callback from the inventory when the item is changed.
        /// </summary>
        /// <param name="item">The new item.</param>
        private void PrimaryItemChange(Item item)
        {
            // Do not listen for the ready event when the inventory switches items.
            if (m_ItemUsePending != null) {
                EventHandler.UnregisterEvent(m_GameObject, "OnItemReadyForUse", ReadyForUse);
                m_ItemUsePending = null;
            }
        }

        /// <summary>
        /// Tries to stop the current primary item from being used.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be stopped? If false the dual wield item will be stopped.</param>
        public void TryStopUse(bool primaryItem)
        {
            TryStopUse(primaryItem, -1);
        }

        /// <summary>
        /// Tries to stop the current primary item from being used.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be stopped? If false the dual wield item will be stopped.</param>
        /// <param name="extensionIndex">Specifies the index of the extension that should be stopped.</param>
        public void TryStopUse(bool primaryItem, int extensionIndex)
        {
            var item = (primaryItem ? m_CurrentPrimaryItem.Get() : m_CurrentDualWieldItem.Get()) as IUseableItem;
            if (item != null) {
                if (extensionIndex != -1) {
                    item = (item as Item).ItemExtensions[extensionIndex] as IUseableItem;
                    if (item != null) {
                        item.TryStopUse();
                    }
                } else {
                    item.TryStopUse();
                }
            }

            if (m_ItemUsePending != null) {
                m_StopUse = true;
            } 
        }

        /// <summary>
        /// Stops the use of all active items.
        /// </summary>
        public void TryStopAllUse()
        {
            var item = m_CurrentPrimaryItem.Get();
            if (item != null) {
                TryStopUse(true);
                for (int i = 0; i < item.ItemExtensions.Length; ++i) {
                    TryStopUse(true, i);
                }
            }

            item = m_CurrentDualWieldItem.Get();
            if (item != null) {
                TryStopUse(false);
                for (int i = 0; i < item.ItemExtensions.Length; ++i) {
                    TryStopUse(false, i);
                }
            }
        }

        /// <summary>
        /// Tries to reload the current item. Will return false if the item doesn't derive from IReloadableItem
        /// </summary>
        /// <returns>True if the item was reloaded.</returns>
        public bool TryReload()
        {
            // Return early if the item cannot be interacted with.
            if (!m_CanInteractItem.Invoke()) {
                return false;
            }

            var startReload = false;
            Item item;
            if ((item = m_CurrentPrimaryItem.Get()) != null && item is IReloadableItem) {
                if (item is IReloadableItem) {
                    (item as IReloadableItem).StartReload();
                    startReload = true;
                }
            }

            if ((item = m_CurrentDualWieldItem.Get()) != null) {
                if (item is IReloadableItem) {
                    (item as IReloadableItem).StartReload();
                    startReload = true;
                }
            }
            return startReload;
        }

        /// <summary>
        /// Tries to toggle the flashlight on or off.
        /// </summary>
        private void TryToggleFlashlight()
        {
            // The flashlight can only be toggled while aiming.
            if (m_Aiming) {
                Item item;
                if ((item = m_CurrentPrimaryItem.Get()) != null && item is IFlashlightUseable) {
                    var flashlightUseable = item as IFlashlightUseable;
                    flashlightUseable.ToggleFlashlight();
                }

                if ((item = m_CurrentDualWieldItem.Get()) != null && item is IFlashlightUseable) {
                    var flashlightUseable = item as IFlashlightUseable;
                    flashlightUseable.ToggleFlashlight();
                }
            }
        }

        /// <summary>
        /// Tries to toggle the laser sight on or off.
        /// </summary>
        private void TryToggleLaserSight()
        {
            // The laser sight can only be toggled while aiming.
            if (m_Aiming) {
                Item item;
                if ((item = m_CurrentPrimaryItem.Get()) != null && item is ILaserSightUseable) {
                    var laserSightUseable = item as ILaserSightUseable;
                    laserSightUseable.ToggleLaserSight();
                }

                if ((item = m_CurrentDualWieldItem.Get()) != null && item is ILaserSightUseable) {
                    var laserSightUseable = item as ILaserSightUseable;
                    laserSightUseable.ToggleLaserSight();
                }
            }
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            m_AllowGameplayInput = allow;
            if (!allow) {
                TryStopUse(true);
                TryStopUse(false);
            }
        }

        /// <summary>
        /// A new ability has started. Determine if the handler should stop using the item.
        /// </summary>
        /// <param name="ability">The ability that was started.</param>
        private void OnAbilityStart(Abilities.Ability ability)
        {
            if (!ability.CanInteractItem() || !ability.CanUseItem()) {
                TryStopAllUse();
            }
        }

        /// <summary>
        /// The character has died. Disable the component.
        /// </summary>
        private void OnDeath()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = false;
        }

        /// <summary>
        /// The character has respawned. Enable the component.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            if (!m_IndependentLook.Invoke()) {
                enabled = true;
            }
        }
    }
}