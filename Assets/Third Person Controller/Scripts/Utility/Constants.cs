using UnityEngine;

namespace Opsive.ThirdPersonController
{
    public static class Constants
    {
        // PlayerInput
        // If Disable When Button Down is true, the cursor will be disabled if the primary button is down
        private static string m_PrimaryDisableButtonName = "Fire1";
        // If Disable When Button Down is true, the cursor will be disabled if the secondary button is down
        private static string m_SecondaryDisableButtonName = "Fire2";

        // Exposed properties
        public static string PrimaryDisableButtonName { get { return m_PrimaryDisableButtonName; } }
        public static string SecondaryDisableButtonName { get { return m_SecondaryDisableButtonName; } }

        // ControllerHandler
        // The mapping to the horizontal input
        private static string m_HorizontalInputName = "Horizontal";
        // The mapping to the forward input
        private static string m_ForwardInputName = "Vertical";
        // The mapping to the aim input
        private static string m_AimInputName = "Fire2";

        // Exposed properties
        public static string HorizontalInputName { get { return m_HorizontalInputName; } }
        public static string ForwardInputName { get { return m_ForwardInputName; } }
        public static string AimInputName { get { return m_AimInputName; } }

        // ItemHandler
        // The mapping to the use input
        private static string m_UseInputName = "Fire1";
        // The mapping to the reload input
        private static string m_ReloadInputName = "Reload";
        // The mapping to the secondary input
        private static string m_UseSecondaryInputName = "Secondary";
        // The mapping to the left dual wield use input
        private static string m_DualWieldLeftUseInputName = "Fire1";
        // The mapping to the right dual wield use input
        private static string m_DualWieldRightUseInputName = "Fire2";
        // The mapping to the toggle flashlight input
        private static string m_ToggleFlashlightInputName = "Toggle Flashlight";
        // The mapping to the toggle laser sight input
        private static string m_ToggleLaserSightInputName = "Toggle Laser Sight";
        
        public static string UseInputName { get { return m_UseInputName; } }
        public static string ReloadInputName { get { return m_ReloadInputName; } }
        public static string UseSecondaryInputName { get { return m_UseSecondaryInputName; } }
        public static string DualWieldLeftUseInputName { get { return m_DualWieldLeftUseInputName; } }
        public static string DualWieldRightUseInputName { get { return m_DualWieldRightUseInputName; } }
        public static string ToggleFlashlightInputName { get { return m_ToggleFlashlightInputName; } }
        public static string ToggleLaserSightInputName { get { return m_ToggleLaserSightInputName; } }

        // InventoryHandler
        // The mapping to the next item input
        private static string m_NextItemInputName = "Next Item";
        // The mapping to the previous item input
        private static string m_PrevItemInputName = "Previous Item";
        // The mapping to the toggle equip/unequip item input
        private static string m_EquipItemToggleInputName = "Equip Item Toggle";
        // The mapping to drop the dual wielded item
        private static string m_DropDualWieldItemInputName = "Drop Dual Wield Item";
        // The mapping to the item scroll input
        private static string m_ItemScrollName = "Mouse ScrollWheel";
        // The mapping to the equip an item in the specified Inventory slot
        private static string[] m_EquipSpecifiedItem = new string[] { "Equip First Item", "Equip Second Item", "Equip Third Item", "Equip Fourth Item", "Equip Fifth Item" };
        
        public static string NextItemInputName { get { return m_NextItemInputName; } }
        public static string PrevItemInputName { get { return m_PrevItemInputName; } }
        public static string EquipItemToggleInputName { get { return m_EquipItemToggleInputName; } }
        public static string DropDualWieldItemInputName { get { return m_DropDualWieldItemInputName; } }
        public static string ItemScrollName { get { return m_ItemScrollName; } }
        public static string[] EquipSpecifiedItem { get { return m_EquipSpecifiedItem; } }

        // CameraHandler
        // The mapping to the yaw input
        private static string m_YawInputName = "Mouse X";
        // The mapping to the pitch input
        private static string m_PitchInputName = "Mouse Y";
        // The mapping to the zoom input
        private static string m_ZoomInputName = "Fire2";
        // The mapping to the zoom amount input
        private static string m_StepZoomName = "Mouse ScrollWheel";
        // The mapping to the secondary yaw input. Only used if the camera has a RPG view mode
        private static string m_SecondaryYawInputName = "Horizontal";

        // Exposed properties
        public static string YawInputName { get { return m_YawInputName; } }
        public static string PitchInputName { get { return m_PitchInputName; } }
        public static string ZoomInputName { get { return m_ZoomInputName; } }
        public static string StepZoomName { get { return m_StepZoomName; } }
        public static string SecondaryYawInputName { get { return m_SecondaryYawInputName; } }

        // PointClickControllerHandler
        // The mapping to the move input
        private static string m_MoveInputName = "Fire1";

        // Exposed properties
        public static string MoveInputName { get { return m_MoveInputName; } }

    }
}