using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Easy access to the Unity layer system.
    /// </summary>
    public class LayerManager : MonoBehaviour
    {
        private static LayerManager s_Instance;
        private static LayerManager Instance { get { return s_Instance; } }

        // Built-in Unity layers
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;

        // Custom layers
        public const int CharacterCollider = 27;
        public const int VisualEffect = 28;
        public const int MovingPlatform = 29;
        public const int Enemy = 30;
        public const int Player = 31;

        // Internal variables
        private Dictionary<Collider, List<Collider>> m_IgnoreCollisionMap;

        /// <summary>
        /// A set of masks used by raycasts/spherecasts.
        /// </summary>
        public static class Mask
        {
            // Mask that ignores any invisible objects/water.
            public const int IgnoreInvisibleLayers = ~((1 << TransparentFX) | (1 << IgnoreRaycast) | (1 << VisualEffect));
            // Mask that ignores the current player and any invisible objects.
            public const int IgnoreInvisibleLayersPlayer = ~((1 << TransparentFX) | (1 << IgnoreRaycast) | (1 << VisualEffect) | (1 << Player) | (1 << CharacterCollider));
            // Mask that ignores the current player and any invisible objects/water.
            public const int IgnoreInvisibleLayersPlayerWater = ~((1 << TransparentFX) | (1 << IgnoreRaycast) | (1 << VisualEffect) | (1 << Player) | (1 << CharacterCollider) | (1 << Water));
            // Mask that specifies the ground.
            public const int Ground = ~((1 << IgnoreRaycast) | (1 << Player) | (1 << CharacterCollider) | (1 << Water) | (1 << VisualEffect));
        }

        /// <summary>
        /// Setups the layer collisions.
        /// </summary>
        public void Awake()
        {
            s_Instance = this;

            Physics.IgnoreLayerCollision(Player, VisualEffect);
            Physics.IgnoreLayerCollision(Enemy, VisualEffect);
            Physics.IgnoreLayerCollision(IgnoreRaycast, VisualEffect);
            Physics.IgnoreLayerCollision(VisualEffect, VisualEffect);
            Physics.IgnoreLayerCollision(Player, Water);
            Physics.IgnoreLayerCollision(Player, CharacterCollider);
            Physics.IgnoreLayerCollision(Enemy, CharacterCollider);
            Physics.IgnoreLayerCollision(CharacterCollider, VisualEffect);
            Physics.IgnoreLayerCollision(CharacterCollider, Water);
        }

        /// <summary>
        /// Ignore the collision between the main collider and the other collider.
        /// </summary>
        /// <param name="mainCollider">The main collider collision to ignore.</param>
        /// <param name="otherCollider">The collider to ignore.</param>
        public static void IgnoreCollision(Collider mainCollider, Collider otherCollider)
        {
            Instance.IgnoreCollisionInternal(mainCollider, otherCollider);
        }

        /// <summary>
        /// Ignore the collision between the main collider and the other collider.
        /// </summary>
        /// <param name="mainCollider">The main collider collision to ignore.</param>
        /// <param name="otherCollider">The collider to ignore.</param>
        private void IgnoreCollisionInternal(Collider mainCollider, Collider otherCollider)
        {
            // Keep a mapping of the colliders that mainCollider is ignorning so the collision can easily be reverted.
            if (m_IgnoreCollisionMap == null) {
                m_IgnoreCollisionMap = new Dictionary<Collider, List<Collider>>();
            }

            // Add the collider to the list so it can be reverted.
            List<Collider> colliderList;
            if (!m_IgnoreCollisionMap.TryGetValue(mainCollider, out colliderList)) {
                colliderList = new List<Collider>();
                m_IgnoreCollisionMap.Add(mainCollider, colliderList);
            }
            colliderList.Add(otherCollider);

            // The otherCollider must also keep track of the mainCollder. This allows otherCollider to be removed before mainCollider.
            if (!m_IgnoreCollisionMap.TryGetValue(otherCollider, out colliderList)) {
                colliderList = new List<Collider>();
                m_IgnoreCollisionMap.Add(otherCollider, colliderList);
            }
            colliderList.Add(mainCollider);

            // Do the actual ignore.
            Physics.IgnoreCollision(mainCollider, otherCollider);
        }

        /// <summary>
        /// The main collider should no longer ignore any collisions.
        /// </summary>
        /// <param name="mainCollider">The collider to revert the collisions on.</param>
        public static void RevertCollision(Collider mainCollider)
        {
            Instance.RevertCollisionInternal(mainCollider);
        }

        /// <summary>
        /// The main collider should no longer ignore any collisions.
        /// </summary>
        /// <param name="mainCollider">The collider to revert the collisions on.</param>
        private void RevertCollisionInternal(Collider mainCollider)
        {
            List<Collider> colliderList;
            List<Collider> otherColliderList;
            // Revert the IgnoreCollision setting on all of the colliders that the object is currently ignoring.
            if (m_IgnoreCollisionMap != null && m_IgnoreCollisionMap.TryGetValue(mainCollider, out colliderList)) {
                for (int i = 0; i < colliderList.Count; ++i) {
                    if (!mainCollider.enabled || !mainCollider.gameObject.activeInHierarchy || !colliderList[i].enabled || !colliderList[i].gameObject.activeInHierarchy) {
                        continue;
                    }

                    Physics.IgnoreCollision(mainCollider, colliderList[i], false);

                    // A two way map was added when the initial IgnoreCollision was added. Remove that second map because the IgnoreCollision has been removed.
                    if (m_IgnoreCollisionMap.TryGetValue(colliderList[i], out otherColliderList)) {
                        for (int j = 0; j < otherColliderList.Count; ++j) {
                            if (otherColliderList[j].Equals(mainCollider)) {
                                otherColliderList.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
                colliderList.Clear();
            }
        }
    }
}