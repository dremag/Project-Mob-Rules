using UnityEngine;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// The base class for both mobile and standalone (keyboard/mouse and controller) input. This base class exists so UnityInput doesn't need to know if it
    /// is working with mobile controls or standalone controls.
    /// </summary>
    public abstract class UnityInputBase
    {
        /// <summary>
        /// The type of button action to check against.
        /// </summary>
        public enum ButtonAction { GetButton, GetButtonDown, GetButtonUp }

        /// <summary>
        /// Returns if the button is true with the specified ButtonAction.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="action">The type of action to check.</param>
        /// <returns>The status of the action.</returns>
        public abstract bool GetButton(string name, ButtonAction action);

        /// <summary>
        /// Returns true if a double press occurred (double click or double tap).
        /// </summary>
        /// <param name="name">The button name to check for a double press.</param>
        /// <returns>True if a double press occurred (double click or double tap).</returns>
        public abstract bool GetDoublePress(string name);

        /// <summary>
        /// Returns the axis of the specified button.
        /// </summary>
        /// <param name="axisName">The name of the axis.</param>
        /// <returns>The axis value.</returns>
        public abstract float GetAxis(string axisName);

        /// <summary>
        /// Returns the raw axis of the specified button.
        /// </summary>
        /// <param name="axisName">The name of the axis.</param>
        /// <returns>The raw axis value.</returns>
        public abstract float GetAxisRaw(string axisName);

        /// <summary>
        /// Returns the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        public abstract Vector2 GetMousePosition();
    }
}