using System;
using System.Reflection;
#if NETFX_CORE && !UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace Opsive.ThirdPersonController
{
#if NETFX_CORE && !UNITY_EDITOR
    [Flags]
    public enum BindingFlags
    {
        Default,
        Public,
        Instance,
        InvokeMethod,
        NonPublic,
        Static,
        FlattenHierarchy,
        DeclaredOnly
    }
#endif

    /// <summary>
    /// Extension methods to allow Windows Store Apps to build.
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Returns the type from which the type variable directly inherits.
        /// </summary>
        public static Type GetBaseType(Type type)
        {
#if NETFX_CORE && !UNITY_EDITOR
            return type.GetTypeInfo().BaseType;
#else
            return type.BaseType;
#endif
        }

        /// <summary>
        /// Creates a delegate of the specified type that represents the instance method.
        /// </summary>
        public static Delegate CreateDelegate(Type type, MethodInfo method, object owner)
        {
#if NETFX_CORE && !UNITY_EDITOR
            return method.CreateDelegate(type, owner);
#else
            return Delegate.CreateDelegate(type, owner, method);
#endif
        }

        /// <summary>
        /// Returns the get accessor for the property.
        /// </summary>
        public static MethodInfo GetGetMethod(PropertyInfo property)
        {
#if NETFX_CORE && !UNITY_EDITOR
            return property.GetMethod;
#else
            return property.GetGetMethod(true);
#endif
        }

        /// <summary>
        /// Returns the set accessor for the property.
        /// </summary>
        public static MethodInfo GetSetMethod(PropertyInfo property)
        {
#if NETFX_CORE && !UNITY_EDITOR
            return property.SetMethod;
#else
            return property.GetSetMethod(true);
#endif
        }

#if NETFX_CORE && !UNITY_EDITOR
        /// <summary>
        /// Returns all of the public fields of the specified type.
        /// </summary>
        public static FieldInfo[] GetFields(this Type t, BindingFlags flags)
        {
            if (!flags.HasFlag(BindingFlags.Instance) && !flags.HasFlag(BindingFlags.Static)) return null;

            var ti = t.GetTypeInfo();
            var origFields = ti.DeclaredFields;
            var results = new List<FieldInfo>();
            foreach (var field in origFields)
            {
                var isValid = (flags.HasFlag(BindingFlags.Public) && field.IsPublic) || (flags.HasFlag(BindingFlags.NonPublic) && !field.IsPublic);
                isValid &= (flags.HasFlag(BindingFlags.Static) && field.IsStatic) || (flags.HasFlag(BindingFlags.Instance) && !field.IsStatic);
                if (flags.HasFlag(BindingFlags.DeclaredOnly))
                    isValid &= field.DeclaringType == t;

                results.Add(field);
            }
            return results.ToArray();
        }
        
        /// <summary>
        /// Returns the specified method for the specified type.
        /// </summary>
        public static MethodInfo GetMethod(this Type type, string name, BindingFlags flags)
        {
            return type.GetTypeInfo().GetDeclaredMethod(name);
        }
        
        /// <summary>
        /// Returns the specified property for the specified type.
        /// </summary>
        public static PropertyInfo GetProperty(this Type type, string propertyName, BindingFlags flags)
        {
            return type.GetTypeInfo().GetDeclaredProperty(propertyName);
        }

        public static bool IsAssignableFrom(this Type type, Type toCompare)
        {
            return type.GetTypeInfo().IsAssignableFrom(toCompare.GetTypeInfo());
        }
#endif
    }
}