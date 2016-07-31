using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
#if (UNITY_METRO || UNITY_WINRT) && !UNITY_EDITOR
using BindingFlags = Opsive.ThirdPersonController.BindingFlags;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Manages the SharedProperty and SharedMethod classes. These classes reduce the coupling between classes by allowing access to class A from class B without class B
    /// directly knowing about class A. The SharedManager makes this all possible by assigning the properties and methods of the fields.
    /// </summary>
    public static class SharedManager
    {
        private static Dictionary<GameObject, List<Component>> ownerList = new Dictionary<GameObject, List<Component>>();

        private const string SharedPropertyString = "SharedProperty_";
        private const string SharedMethodString = "SharedMethod_";
        private const string ConstantString = "m_";

        /// <summary>
        /// A component will register itself to make other components aware that it has a SharedProperty or SharedMethod that other components should have access to.
        /// This method doesn't do any of the actual work, it just makes it known that the component has fields that can be shared. Register must be called from Awake.
        /// </summary>
        /// <param name="ownerComponent">The component that is being registered.</param>
        public static void Register(Component ownerComponent)
        {
            var ownerGameObject = ownerComponent.gameObject;
            List<Component> componentList = null;
            ownerList.TryGetValue(ownerGameObject, out componentList);
            if (componentList == null) {
                componentList = new List<Component>();
            }
            componentList.Add(ownerComponent);
            if (!ownerList.ContainsKey(ownerGameObject)) {
                ownerList.Add(ownerGameObject, componentList);
            }
        }

        /// <summary>
        /// Initailizes the SharedProperty and SharedMethod fields of the targetObject. The targetGameObject must have already been registered in order for the SharedFields to be found.
        /// The SharedProperties and SharedMethods must start with SharedPropertyString or SharedMethodString otherwise they will not be found. InitializeSharedFields must be called from Start.
        /// </summary>
        /// <param name="targetGameObject">The registered GameObject that contains SharedFields.</param>
        /// <param name="targetObject">The object which has SharedFields that need to be initialized.</param>
        public static void InitializeSharedFields(GameObject targetGameObject, object targetObject)
        {
            List<Component> componentList;
            ownerList.TryGetValue(targetGameObject, out componentList);
            if (componentList == null) {
                return;
            }

            for (int i = 0; i < componentList.Count; ++i) {
                var ownerComponent = componentList[i];

                // Loop through all of the fields and search for any fields that have a valid field type. When a field is found,
                // create a new instance of that field with the field name as its only argument in the constructor.
                var fields = GetAllFields(targetObject.GetType());
                for (int j = 0; j < fields.Length; ++j) {
                    var baseType = ReflectionExtensions.GetBaseType(fields[j].FieldType);
                    if (baseType == null) {
                        continue;
                    }
                    if (baseType.Equals(typeof(SharedProperty))) {
                        var fieldName = fields[j].Name;
                        if (fieldName.StartsWith(ConstantString)) {
                            fieldName = fieldName.Substring(2); // Remove "m_".
                        }
                        var propertyName = SharedPropertyString + fieldName;
                        PropertyInfo property;
                        if ((property = GetProperty(ownerComponent.GetType(), propertyName)) != null) {
                            fields[j].SetValue(targetObject, Activator.CreateInstance(fields[j].FieldType, property, ownerComponent, propertyName));
                        }
                    } else if (baseType.Equals(typeof(SharedMethod))) {
                        var fieldName = fields[j].Name;
                        if (fieldName.StartsWith(ConstantString)) {
                            fieldName = fieldName.Substring(2); // Remove "m_".
                        }
                        var methodName = SharedMethodString + fieldName;
                        MethodInfo method;
                        if ((method = GetMethod(ownerComponent.GetType(), methodName)) != null) {
                            fields[j].SetValue(targetObject, Activator.CreateInstance(fields[j].FieldType, method, ownerComponent, methodName));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively get all of the fields from the specified type and all of its base types.
        /// </summary>
        /// <param name="type">The object type.</param>
        /// <param name="fieldList">An array of fields.</param>
        private static FieldInfo[] GetAllFields(Type type)
        {
            var fieldList = new List<FieldInfo>();
            GetAllFields(type, ref fieldList);
            return fieldList.ToArray();
        }

        /// <summary>
        /// Recursively get all of the fields from the specified type and all of its base types.
        /// </summary>
        /// <param name="type">The object type.</param>
        /// <param name="fieldList">A list of fields.</param>
        private static void GetAllFields(Type type, ref List<FieldInfo> fieldList)
        {
            if (type == null) {
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; ++i) {
                fieldList.Add(fields[i]);
            }
            GetAllFields(ReflectionExtensions.GetBaseType(type), ref fieldList);
        }

        /// <summary>
        /// Recursively gets the property on the specified type. If the property is not found the base class will recursively be searched.
        /// </summary>
        /// <param name="type">The object type.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The PropertyInfo if found.</returns>
        private static PropertyInfo GetProperty(Type type, string propertyName)
        {
            if (type == null) {
                return null;
            }

            PropertyInfo propertyInfo = null;
            if ((propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) != null) {
                return propertyInfo;
            }

            return GetProperty(ReflectionExtensions.GetBaseType(type), propertyName);
        }

        /// <summary>
        /// Recursively gets the method on the specified type. If the method is not found the base class will recursively be searched.
        /// </summary>
        /// <param name="type">The object type.</param>
        /// <param name="propertyName">The name of the method.</param>
        /// <returns>The MethodInfo if found.</returns>
        private static MethodInfo GetMethod(Type type, string methodName)
        {
            if (type == null) {
                return null;
            }

            MethodInfo methodInfo = null;
            if ((methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) != null) {
                return methodInfo;
            }

            return GetMethod(ReflectionExtensions.GetBaseType(type), methodName);
        }
    }
}