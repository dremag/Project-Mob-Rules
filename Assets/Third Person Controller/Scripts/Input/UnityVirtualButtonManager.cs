using UnityEngine;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// The UnityVirtualButtonManager should be added to the parent GameObject of all of the UnityVirtualButtons. This manager will enable or disable all of the UnityVirtualButtons set by
    /// the UnityInput.
    /// </summary>
    public class UnityVirtualButtonManager : MonoBehaviour
    {
        /// <summary>
        /// Enable or Disable all of the VirtualButton GameObjects.
        /// </summary>
        /// <param name="enabled">Should the VirtualButtons be enabled?</param>
        public void EnableVirtualButtons(bool enabled)
        {
            gameObject.SetActive(enabled);
            for (int i = 0; i < transform.childCount; ++i) {
                transform.GetChild(i).gameObject.SetActive(enabled);
            }
        }
    }
}