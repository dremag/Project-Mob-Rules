using UnityEngine;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// Abstract class to expose a common interface for any input implementation.
    /// </summary>
    public abstract class PlayerInput : MonoBehaviour
    {
        /// <summary>
        /// Return true if the button is being pressed.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True of the button is being pressed.</returns>
        public virtual bool GetButton(string name, bool positiveAxis) { return false; }

        /// <summary>
        /// Return true if the button was pressed this frame. Will use the joystick axis if a joystick is connected.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True if the button is pressed this frame.</returns>
        public virtual bool GetButtonDown(string name, bool positiveAxis) { return false; }

        /// <summary>
        /// Return true if the button was pressed this frame.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is pressed this frame.</returns>
        public virtual bool GetButtonDown(string name) { return GetButtonDown(name, false); }

        /// <summary>
        /// Return true if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is up.</returns>
        public virtual bool GetButtonUp(string name) { return GetButtonUp(name, false); }

        /// <summary>
        /// Return true if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="useJoystick">Should the input be using the joystick axis if connected?</param>
        /// <returns>True if the button is up.</returns>
        public virtual bool GetButtonUp(string name, bool useJoystickAxis) { return false; }

        /// <summary>
        /// Return true if a double press occurred (double click or double tap).
        /// </summary>
        /// <param name="name">The button name to check for a double press.</param>
        /// <returns>True if a double press occurred (double click or double tap).</returns>
        public virtual bool GetDoublePress(string name) { return false; }

        /// <summary>
        /// Return the value of the axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the axis.</returns>
        public virtual float GetAxis(string name) { return 0; }

        /// <summary>
        /// Return the value of the raw axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the raw axis.</returns>
        public virtual float GetAxisRaw(string name) { return 0; }

        /// <summary>
        /// Is a controller connected?
        /// </summary>
        /// <returns>True if a controller is connected.</returns>
        public bool IsControllerConnected() { return UnityEngine.Input.GetJoystickNames().Length > 0; }

        /// <summary>
        /// Return the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        public virtual Vector2 GetMousePosition() { return Vector2.zero; }
    }
}