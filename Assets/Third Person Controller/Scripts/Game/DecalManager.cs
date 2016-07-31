using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Manages all of the decals from weapons/explosions. Will fade away older decals when a maximum number of decals have been added.
    /// </summary>
    public class DecalManager : MonoBehaviour
    {
        // Static variables
        private static DecalManager s_Instance;
        private static DecalManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!m_Initialized) {
                    Debug.LogWarning("Warning: DecalManager is null. A GameObject has been created with the component automatically added. Please run Scene Setup from the Start Window.");
                    s_Instance = new GameObject("DecalManager").AddComponent<DecalManager>();
                }
#endif
                return s_Instance;
            }
        }

        [Tooltip("The maximum number of decals before the decals start to fade away")]
        [SerializeField] protected int m_MaxDecals = 100;
        [Tooltip("The speed at which the decal fades when it needs to fade away")]
        [SerializeField] protected float m_FadeAmount = 0.005f;
        [Tooltip("A mask of layers that decals cannot appear on (such as the Player layer)")]
        [SerializeField] protected LayerMask m_PreventDecalOnLayers;

        // Internal variables
#if UNITY_EDITOR
        private static bool m_Initialized;
#endif
        private List<Renderer> m_ActiveDecals = new List<Renderer>();

        /// <summary>
        /// Assign the static variables.
        /// </summary>
        private void OnEnable()
        {
            s_Instance = this;
#if UNITY_EDITOR
            m_Initialized = true;
#endif
        }

        /// <summary>
        /// Disables the DecalManager. It will be enabled when a decal is added.
        /// </summary>
        private void Start()
        {
            enabled = false;
        }

        /// <summary>
        /// If the DecalManager is holding the maximum number of decals then start to fade the older decals.
        /// </summary>
        private void Update()
        {
            if (m_ActiveDecals.Count > m_MaxDecals) {
                for (int i = 0; i < (m_ActiveDecals.Count - m_MaxDecals); ++i) {
                    Fade(m_ActiveDecals[i]);
                }

                if (m_ActiveDecals[0].material.color.a <= 0) {
                    RemoveOldestDecal();
                }
            } else {
                // The maximum number of decals hasn't been reached yet so there is no reason why the DecalManager needs to stay active.
                enabled = false;
            }
        }

        /// <summary>
        /// Returns if a decal can be added to a particular layer.
        /// </summary>
        /// <param name="layer">The layer that may not be able to have a decal added to it.</param>
        /// <returns>True if the decal can be added to the specified layer</returns>
        public static bool CanAddDecal(int layer)
        {
            return Instance.CanAddDecalInternal(layer);
        }

        /// <summary>
        /// Internal method which returns if a decal can be added to a particular layer.
        /// </summary>
        /// <param name="layer">The layer that may not be able to have a decal added to it.</param>
        /// <returns>True if the decal can be added to the specified layer</returns>
        private bool CanAddDecalInternal(int layer)
        {
            return !Utility.InLayerMask(layer, m_PreventDecalOnLayers.value);
        }

        /// <summary>
        /// Add a new decal to the DecalManager.
        /// </summary>
        /// <param name="original">A reference to the decal prefab.</param>
        /// <param name="position">The position of the decal.</param>
        /// <param name="rotation">The rotation of the decal.</param>
        /// <returns>The newly added decal.</returns>
        public static GameObject Add(GameObject original, Vector3 position, Quaternion rotation)
        {
            return Add(original, position, rotation, null);
        }

        /// <summary>
        /// Add a new decal to the DecalManager.
        /// </summary>
        /// <param name="original">A reference to the decal prefab.</param>
        /// <param name="position">The position of the decal.</param>
        /// <param name="rotation">The rotation of the decal.</param>
        /// <param name="parent">The parent of the decal. This allows the parent to move and the decal will move with it.</param>
        /// <returns>The newly added decal.</returns>
        public static GameObject Add(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            return Instance.AddInternal(original, position, rotation, parent);
        }

        /// <summary>
        /// The internal method to add a new decal to the DecalManager.
        /// </summary>
        /// <param name="original">A reference to the decal prefab.</param>
        /// <param name="position">The position of the decal.</param>
        /// <param name="rotation">The rotation of the decal.</param>
        /// <param name="parent">The parent of the decal. This allows the parent to move and the decal will move with it.</param>
        /// <returns>The newly added decal.</returns>
        private GameObject AddInternal(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            // Don't add the decal if the parent is already disabled.
            if (parent != null && parent.gameObject.activeSelf == false) {
                return null;
            }

            var decal = ObjectPool.Instantiate(original, position, rotation, parent);

            // The decal is pooled so reset the alpha back to 1.
            var decalRenderer = decal.GetComponent<Renderer>();
            var color = decalRenderer.material.color;
            color.a = 1;
            decalRenderer.material.color = color;

            m_ActiveDecals.Add(decalRenderer);
            if (!enabled) {
                enabled = true;
            }
            return decal;
        }

        /// <summary>
        /// Remove the oldest decal, the one that is first in the active decal list.
        /// </summary>
        private void RemoveOldestDecal()
        {
            var decal = m_ActiveDecals[0];
            m_ActiveDecals.RemoveAt(0);
            ObjectPool.Destroy(decal.gameObject);
        }

        /// <summary>
        /// The decal has served its purpose and now should be removed.
        /// </summary>
        /// <param name="decal">The decal to remove.</param>
        public static void Remove(Renderer decal)
        {
            Instance.RemoveInternal(decal);
        }

        /// <summary>
        /// The internal method to remove the decal based off of the Renderer.
        /// </summary>
        /// <param name="decal">The decal to remove.</param>
        private void RemoveInternal(Renderer decal)
        {
            var index = m_ActiveDecals.IndexOf(decal);
            if (index != -1) {
                m_ActiveDecals.RemoveAt(index);
                ObjectPool.Destroy(decal.gameObject);
            }
        }

        /// <summary>
        /// The maximum number of decals have been added and now the decal should start to fade away.
        /// </summary>
        /// <param name="decal">The decal to fade.</param>
        private void Fade(Renderer decal)
        {
            var color = decal.material.color;
            color.a -= m_FadeAmount;
            decal.material.color = color;
        }
    }
}