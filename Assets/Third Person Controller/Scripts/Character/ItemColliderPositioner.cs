using UnityEngine;

namespace Opsive.ThirdPersonController
{
    [System.Obsolete("The ItemColliderPosition component has been deprecated as of version 1.3. Please remove this component and the SphereCollider from your character.")]
    public class ItemColliderPositioner : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("Warning: The ItemColliderPosition component has been deprecated as of version 1.3. Please remove this component and the SphereCollider from your character.");
        }
    }
}