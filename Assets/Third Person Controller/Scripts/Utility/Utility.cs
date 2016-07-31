using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A collection of small utility methods.
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// Returns the component of type T which has been added to a parent of the inputted transform.
        /// </summary>
        /// <typeparam name="T">The type of component to search for.</typeparam>
        /// <param name="transform">The child transform.</param>
        /// <returns>The parent component.</returns>
        public static T GetComponentInParent<T>(this Transform transform) where T : Component
        {
            T component = transform.GetComponent<T>();
            if (component != null) {
                return component;
            }
            var parent = transform.parent;
            if (parent != null) {
                return parent.GetComponentInParent<T>();
            }
            return null;
        }
        
        /// <summary>
        /// Returns the first instance of T found in the transform's child. This is different from GetComponentInChildren because it does not return the component
        /// that is attached to the calling transform
        /// </summary>
        /// <typeparam name="T">The type of component to search for.</typeparam>
        /// <param name="transform">The transform to start searching from.</param>
        /// <returns>The child component.</returns>
        public static T GetChildComponent<T>(this Transform transform) where T : Component
        {
            for (int i = 0; i < transform.childCount; ++i) {
                var childComponent = transform.GetChild(i).GetComponentInChildren<T>();
                if (childComponent != null) {
                    return childComponent;
                }
            }
            return null;
        }

        /// <summary>
        /// Restricts the angle between -360 and 360 degrees.
        /// </summary>
        /// <param name="angle">The angle to restrict.</param>
        /// <returns>An angle between -360 and 360 degrees.</returns>
        public static float RestrictAngle(float angle)
        {
            if (angle < -360) {
                angle += 360;
            }
            if (angle > 360) {
                angle -= 360;
            }
            return angle;
        }

        /// <summary>
        /// Restricts the angle between -180 and 180 degrees.
        /// </summary>
        /// <param name="angle">The angle to restrict.</param>
        /// <returns>An angle between -180 and 180 degrees.</returns>
        public static float RestrictInnerAngle(float angle)
        {
            if (angle < -180) {
                angle += 360;
            }
            if (angle > 180) {
                angle -= 360;
            }
            return angle;
        }

        /// <summary>
        /// Clamp the angle between the min and max angle values.
        /// </summary>
        /// <param name="angle">The angle to be clamped.</param>
        /// <param name="min">The minimum angle value.</param>
        /// <param name="max">The maximum angle value.</param>
        /// <returns></returns>
        public static float ClampAngle(float angle, float min, float max)
        {
            return Mathf.Clamp(RestrictAngle(angle), min, max);
        }

        /// <summary>
        /// Returns true if layer is within the layerMask.
        /// </summary>
        /// <param name="layer">The layer to check.</param>
        /// <param name="layerMask">The mask to compare against.</param>
        /// <returns>True if the layer is within the layer mask.</returns>
        public static bool InLayerMask(int layer, int layerMask)
        {
            return ((1 << layer) & layerMask) == (1 << layer);
        }

        /// <summary>
        /// Returns the camera with the MainCamera tag or the camera with the CameraMonitor attached.
        /// </summary>
        /// <returns></returns>
        public static Camera FindCamera()
        {
            if (Camera.main != null && Camera.main.GetComponent<CameraMonitor>() != null) {
                return Camera.main;
            }
            for (int i = 0; i < Camera.allCameras.Length; ++i) {
                if (Camera.allCameras[i].GetComponent<CameraMonitor>() != null) {
                    return Camera.allCameras[i];
                }
            }
            Debug.LogWarning("No camera exists with the CameraMonitor component. Has this component been added to a camera?");
            return null;
        }
    }
}