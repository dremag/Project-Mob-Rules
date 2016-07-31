using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Determines which object to spawn based off of a dictionary in additioning to managing the active state of interested objects. 
    /// Will destroy the oldest object as soon as too many of the same objects have been instantiated.
    /// </summary>
    public class ObjectManager : MonoBehaviour
    {
        /// <summary>
        /// The category of objects that the ObjectManager manages.
        /// </summary>
        public enum ObjectCategory { Audio, Decal, Dust, Spark }

        /// <summary>
        /// Maps an ItemType to the array of spawnable objects.
        /// </summary>
        [System.Serializable]
        protected class ItemObjectCategoryArray
        {
            [Tooltip("The tag that the objects can spawn above")]
            [SerializeField] ItemType m_ItemType;
            [Tooltip("An array of the audio objects")]
            [SerializeField] protected Object[] m_Audio;
            [Tooltip("An array of the decal objects")]
            [SerializeField] protected Object[] m_Decals;
            [Tooltip("An array of the dust objects")]
            [SerializeField] protected Object[] m_Dust;
            [Tooltip("An array of the spark objects")]
            [SerializeField] protected Object[] m_Sparks;

            // Exposed properties
            public ItemType ItemType { get { return m_ItemType; } }

            /// <summary>
            /// Returns a random object from the Objects array.
            /// </summary>
            /// <param name="category">The category used to get the object.</param>
            /// <returns>A random object from the Objects array.</returns>
            public Object RandomObject(ObjectCategory category)
            {
                Object[] objects = null;
                switch (category) {
                    case ObjectCategory.Audio:
                        objects = m_Audio;
                        break;
                    case ObjectCategory.Decal:
                        objects = m_Decals;
                        break;
                    case ObjectCategory.Dust:
                        objects = m_Dust;
                        break;
                    case ObjectCategory.Spark:
                        objects = m_Sparks;
                        break;
                }

                if (objects != null && objects.Length > 0) {
                    return objects[Random.Range(0, objects.Length)];
                }
                return null;
            }
        }

        /// <summary>
        /// Maps a Transform tag to the list of objects.
        /// </summary>
        [System.Serializable]
        protected class TagObjectArray
        {
            // The tag that can spawn based on the objects
            [SerializeField] protected string m_Tag;
            // An array of all of the objects for the specified tag
            [SerializeField] protected ItemObjectCategoryArray[] m_Objects;

            // Exposed properties
            public string Tag { get { return m_Tag; } }

            // Internal variables
            private Dictionary<ItemType, ItemObjectCategoryArray> m_ItemObjectMap;
            private ItemObjectCategoryArray m_NullItemObjects;

            /// <summary>
            /// Initializes the tag object dictionary.
            /// </summary>
            public void Initialize()
            {
                m_ItemObjectMap = new Dictionary<ItemType, ItemObjectCategoryArray>();
                if (m_Objects != null && m_Objects.Length > 0) {
                    for (int i = 0; i < m_Objects.Length; ++i) {
                        if (m_Objects[i].ItemType == null) {
                            m_NullItemObjects = m_Objects[i];
                        } else {
                            m_ItemObjectMap.Add(m_Objects[i].ItemType, m_Objects[i]);
                        }
                    }
                }
            }

            /// <summary>
            /// Returns the object for the specified tag and category.
            /// </summary>
            /// <param name="tag">The tag used to get the object.</param>
            /// <param name="category">The category used to get the object.</param>
            /// <returns>The object to spawn based on the tag and category.</returns>
            public Object ObjectForItem(ItemType itemType, ObjectCategory category)
            {
                if (itemType != null) {
                    ItemObjectCategoryArray objectArray;
                    if (m_ItemObjectMap.TryGetValue(itemType, out objectArray)) {
                        // Randomly choose an object.
                        return objectArray.RandomObject(category);
                    }
                }

                // Fallback to NullItemObjects if an object doesn't exist for the specified category.
                if (m_NullItemObjects != null) {
                    return m_NullItemObjects.RandomObject(category);
                }
                return null;
            }
        }

        // Static variables
        private static ObjectManager s_Instance;
        private static ObjectManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!m_Initialized) {
                    Debug.LogWarning("Warning: ObjectManager is null. A GameObject has been created with the component automatically added. Please run Scene Setup from the Start Window.");
                    s_Instance = new GameObject("ObjectManager").AddComponent<ObjectManager>();
                }
#endif
                return s_Instance;
            }
        }

        // The maximum number of objects before the objects are placed back in the ObjectPool
        [SerializeField] protected int m_MaxObjectsOfType = 20;
        [SerializeField] protected TagObjectArray[] m_ObjectsPerTag;

        private Dictionary<string, TagObjectArray> m_ObjectsPerTagMap;

        // Internal variables
#if UNITY_EDITOR
        private static bool m_Initialized;
#endif
        private Dictionary<int, List<GameObject>> m_Objects = new Dictionary<int, List<GameObject>>();

        /// <summary>
        /// Initialize default values.
        /// </summary>
        private void Awake()
        {
            m_ObjectsPerTagMap = new Dictionary<string, TagObjectArray>();
            if (m_ObjectsPerTag != null && m_ObjectsPerTag.Length > 0) {
                for (int i = 0; i < m_ObjectsPerTag.Length; ++i) {
                    m_ObjectsPerTagMap.Add(m_ObjectsPerTag[i].Tag, m_ObjectsPerTag[i]);
                    m_ObjectsPerTag[i].Initialize();
                }
            }
        }

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
        /// Add an object to the managed object list.
        /// </summary>
        /// <param name="instantiatedObject">The object to manage.</param>
        public static void AddObject(GameObject instantiatedObject)
        {
            Instance.AddObjectInternal(instantiatedObject);
        }

        /// <summary>
        /// Internal method to add an object to the managed object list.
        /// </summary>
        /// <param name="instantiatedObject">The object to manage.</param>
        private void AddObjectInternal(GameObject instantiatedObject)
        {
            // The ObjectPool will know the original instance ID because the object hasn't been destroyed yet.
            var originalInstanceID = ObjectPool.OriginalInstanceID(instantiatedObject);

            List<GameObject> objectList;
            if (!m_Objects.TryGetValue(originalInstanceID, out objectList)) {
                objectList = new List<GameObject>();
                m_Objects.Add(originalInstanceID, objectList);
            }

            // If the number of objects is getting too big then remove the first object and place it back in the ObjectPool.
            if (objectList.Count > m_MaxObjectsOfType - 1) {
                var firstInstantaitedObject = objectList[0];
                ObjectPool.Destroy(firstInstantaitedObject);
                objectList.RemoveAt(0);
            }

            // Add the instantiated object to the end of the list.
            objectList.Add(instantiatedObject);
        }

        /// <summary>
        /// Return the object which should spawn based on the item type, layer, and cateogry.
        /// </summary>
        /// <param name="tag">The tag which the object is spawning above.</param>
        /// <param name="itemType">The type of item.</param>
        /// <param name="objectCategory">The category of the object to spawn.</param>
        /// <returns>The object to spawn. Null will be returned if there are no objects with the specified item type, layer, and category.</returns>
        public static Object ObjectForItem(string tag, ItemType itemType, ObjectCategory objectCategory)
        {
            return Instance.ObjectForItemInternal(tag, itemType, objectCategory);
        }

        /// <summary>
        /// Internal method to return the object which should spawn based on the item type, layer, and cateogry.
        /// </summary>
        /// <param name="tag">The tag which the object is spawning above.</param>
        /// <param name="itemType">The type of item.</param>
        /// <param name="objectCategory">The category of the object to spawn.</param>
        /// <returns>The object to spawn. Null will be returned if there are no objects with the specified item type, layer, and category.</returns>
        private Object ObjectForItemInternal(string tag, ItemType itemType, ObjectCategory objectCategory)
        {
            TagObjectArray tagObject = null;
            if (m_ObjectsPerTagMap.TryGetValue(tag, out tagObject)) {
                return tagObject.ObjectForItem(itemType, objectCategory);
            }
            return null;
        }
    }
}