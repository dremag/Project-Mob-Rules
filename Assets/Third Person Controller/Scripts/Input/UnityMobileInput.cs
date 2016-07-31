using UnityEngine;
using System.Collections.Generic;
using System;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// Translates UnityInput calls into UnityVirtualButton calls.
    /// </summary>
    public class UnityMobileInput : UnityInputBase
    {
        // Internal variable
        private Dictionary<string, UnityVirtualButton> m_VirtualButtonMap;

        /// <summary>
        /// Returns if the VirtualButton is true with the specified ButtonAction.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <param name="action">The type of action to check.</param>
        /// <returns>The status of the action.</returns>
        public override bool GetButton(string name, ButtonAction action)
        {
            switch (action) {
                case ButtonAction.GetButton:
                    return GetButton(name);
                case ButtonAction.GetButtonDown:
                    return GetButtonDown(name);
                case ButtonAction.GetButtonUp:
                    return GetButtonUp(name);
            }
            return false;
        }

        /// <summary>
        /// Return true if the VirtualButton is being pressed.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <returns>True if the VirtualButton is being pressed.</returns>
        private bool GetButton(string name)
        {
            if (m_VirtualButtonMap == null) {
                return false;
            }

            UnityVirtualButton virtualButton;
            if (m_VirtualButtonMap.TryGetValue(name, out virtualButton)) {
                return virtualButton.GetButton();
            }
            return false;
        }

        /// <summary>
        /// Return true if the VirtualButton is pressed this frame.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <returns>True if the VirtualButton is pressed this frame.</returns>
        private bool GetButtonDown(string name)
        {
            if (m_VirtualButtonMap == null) {
                return false;
            }

            UnityVirtualButton virtualButton;
            if (m_VirtualButtonMap.TryGetValue(name, out virtualButton)) {
                return virtualButton.GetButtonDown();
            }
            return false;
        }

        /// <summary>
        /// Return true if the VirtualButton is not being pressed.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <returns>True if the VirtualButton is not being pressed.</returns>
        private bool GetButtonUp(string name)
        {
            if (m_VirtualButtonMap == null) {
                return false;
            }

            UnityVirtualButton virtualButton;
            if (m_VirtualButtonMap.TryGetValue(name, out virtualButton)) {
                return virtualButton.GetButtonUp();
            }
            return false;
        }

        /// <summary>
        /// Returns true if a double tap occurred.
        /// </summary>
        /// <param name="name">The button name to check for a double tap.</param>
        /// <returns>True if a double tap happened.</returns>
        public override bool GetDoublePress(string name)
        {
            if (UnityEngine.Input.touchCount == 0) {
                return false;
            }
            var touch = UnityEngine.Input.touches[0];
            return touch.tapCount == 2;
        }

        /// <summary>
        /// Return the relative position of a swipe on the VirtualButton.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <returns>The relative position of a swipe on the VirtualButton.</returns>
        public override float GetAxis(string name)
        {
            if (m_VirtualButtonMap == null) {
                return 0;
            }

            UnityVirtualButton virtualButton;
            if (m_VirtualButtonMap.TryGetValue(name, out virtualButton)) {
                return virtualButton.GetAxis(name);
            }
            return 0;
        }

        /// <summary>
        /// Return the relative position of a swipe on the VirtualButton.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <returns>The relative position of a swipe on the VirtualButton.</returns>
        public override float GetAxisRaw(string name)
        {
            return GetAxis(name);
        }

        /// <summary>
        /// Binds a new VirtualButton with the specified values.
        /// </summary>
        /// <param name="virtualButtonName">The VirtualButton name.</param>
        /// <param name="virtualButtonValue">A reference to the VirtualButton.</param>
        public void AddVirtualButton(string virtualButtonName, UnityVirtualButton virtualButtonValue)
        {
            if (m_VirtualButtonMap == null) {
                m_VirtualButtonMap = new Dictionary<string, UnityVirtualButton>();
            }

            m_VirtualButtonMap.Add(virtualButtonName, virtualButtonValue);
        }

        /// <summary>
        /// Removes a VirtualButton from the mapping. Used when a VirutalButton is disabled.
        /// </summary>
        /// <param name="virtualButtonName">The name of the VirtualButton to remove.</param>
        public void RemoveVirtualButton(string virtualButtonName)
        {
            m_VirtualButtonMap.Remove(virtualButtonName);
        }

        /// <summary>
        /// Returns the position of the first finger.
        /// </summary>
        /// <returns>Position of the first finger.</returns>
        public override Vector2 GetMousePosition()
        {
            return Vector3.zero;
        }
    }
}