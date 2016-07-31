using UnityEngine;
using System;
using System.Reflection;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// See the SharedManager for more information, but the SharedProperty class decreases the coupling between classes. It allows class A to get/set a property on class B
    /// class A directly knowing about class B.
    /// </summary>
    public abstract class SharedProperty
    {
        // The name of the property to get/set. Used for debugging.
        protected string propertyName;
    }

    /// <summary>
    /// A SharedProperty of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of SharedProperty.</typeparam>
    public class SharedProperty<T> : SharedProperty
    {
        // Internal variables
		private Func<T> m_Getter;
		private Action<T> m_Setter;
#if UNITY_IPHONE || UNITY_WIIU
        // AOT platforms require a defined handler otherwise it'll throw JIT errors.
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
		private event Func<T> Getter
		{
			add { m_Getter += value; }
			remove { m_Getter -= value; }
		}

		private event Action<T> Setter
		{
			add { m_Setter += value; }
			remove { m_Setter -= value; }
		}
#endif

        /// <summary>
        /// SharedProperty for SharedMethod. Creates the get and set delegete.
        /// </summary>
        /// <param name="method">The reflected PropertyInfo.</param>
        /// <param name="owner">The object that contains the property.</param>
        /// <param name="name">The method name (used for debugging).</param>
        public SharedProperty(PropertyInfo property, object owner, string name)
        {
            propertyName = name;

            MethodInfo method;
            if ((method = ReflectionExtensions.GetGetMethod(property)) != null)
                m_Getter = (Func<T>)ReflectionExtensions.CreateDelegate(typeof(Func<T>), method, owner);

            if ((method = ReflectionExtensions.GetSetMethod(property)) != null)
                m_Setter = (Action<T>)ReflectionExtensions.CreateDelegate(typeof(Action<T>), method, owner);
        }

        /// <summary>
        /// Gets the property value.
        /// </summary>
        /// <returns>The value of the property.</returns>
        public T Get()
        {
			if (m_Getter == null) {
                Debug.LogError("Unable to get " + propertyName + ": Getter does not exist");
                return default(T);
            }

			return m_Getter();
        }

        /// <summary>
        /// Sets the property value.
        /// </summary>
        /// <param name="value">The value of the property.</param>
        public void Set(T value)
        {
			if (m_Setter == null) {
                Debug.LogError("Unable to set " + propertyName + ": Setter does not exist");
                return;
            }

			m_Setter(value);
        }
    }
}