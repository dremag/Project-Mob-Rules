using UnityEngine;
using System;
using System.Reflection;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// See the SharedManager for more information, but the SharedMethod class decreases the coupling between classes. It allows class A to invoke a method within class B without
    /// class A directly knowing about class B.
    /// </summary>
    public abstract class SharedMethod
    {
        // The name of the method to invoke. Used for debugging.
        protected string m_MethodName;
    }

    /// <summary>
    /// A SharedMethod which returns a value.
    /// </summary>
    /// <typeparam name="R">The type of value to return.</typeparam>
    public class SharedMethod<R> : SharedMethod
    {
        // Internal variables
        private Func<R> m_Invoker;
#if UNITY_IPHONE || UNITY_WIIU
        // AOT platforms require a defined handler otherwise they'll throw JIT errors.
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
		private event Func<R> Invoker
        {
            add { m_Invoker += value; }
            remove { m_Invoker -= value; }
        }
#endif

        /// <summary>
        /// Constructor for SharedMethod. Creates the delegete.
        /// </summary>
        /// <param name="method">The reflected MethodInfo.</param>
        /// <param name="owner">The object that contains the method.</param>
        /// <param name="name">The method name (used for debugging).</param>
        public SharedMethod(MethodInfo method, object owner, string name)
        {
            m_MethodName = name;
            m_Invoker = (Func<R>)ReflectionExtensions.CreateDelegate(typeof(Func<R>), method, owner);
        }

        /// <summary>
        /// Invoke the method.
        /// </summary>
        /// <returns>The returned value.</returns>
        public R Invoke()
        {
            if (m_Invoker == null) {
                Debug.LogError("Unable to invoke " + m_MethodName + ": Method does not exist");
                return default(R);
            }

            return m_Invoker();
        }
    }

    /// <summary>
    /// A SharedMethod which has one parameter.
    /// </summary>
    /// <typeparam name="T">The type of parameter.</typeparam>
    public class SharedMethodArg<T> : SharedMethod
    {
        // Internal variables
        private Action<T> m_Invoker;
#if UNITY_IPHONE || UNITY_WIIU
        // AOT platforms require a defined handler otherwise they'll throw JIT errors.
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
		private event Action<T> Invoker
        {
            add { m_Invoker += value; }
            remove { m_Invoker -= value; }
        }
#endif

        /// <summary>
        /// Constructor for SharedMethodArg. Creates the delegete.
        /// </summary>
        /// <param name="method">The reflected MethodInfo.</param>
        /// <param name="owner">The object that contains the method.</param>
        /// <param name="name">The method name (used for debugging).</param>
        public SharedMethodArg(MethodInfo method, object owner, string name)
        {
            m_MethodName = name;
            m_Invoker = (Action<T>)ReflectionExtensions.CreateDelegate(typeof(Action<T>), method, owner);
        }

        /// <summary>
        /// Invoke the method.
        /// </summary>
        /// <param name="value">The first parameter.</param>
        public void Invoke(T value)
        {
            if (m_Invoker == null) {
                Debug.LogError("Unable to invoke " + m_MethodName + ": Method does not exist");
                return;
            }

            m_Invoker(value);
        }
    }

    /// <summary>
    /// A SharedMethod which has two parameters.
    /// </summary>
    /// <typeparam name="T">The type of the first parameter.</typeparam>
    /// <typeparam name="T">The type of the second parameter.</typeparam>
    public class SharedMethodArg<T, U> : SharedMethod
    {
        // Internal variables
        private Action<T, U> m_Invoker;
#if UNITY_IPHONE || UNITY_WIIU
        // AOT platforms require a defined handler otherwise they'll throw JIT errors.
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
		private event Action<T, U> Invoker
        {
            add { m_Invoker += value; }
            remove { m_Invoker -= value; }
        }
#endif

        /// <summary>
        /// Constructor for SharedMethodArg. Creates the delegete.
        /// </summary>
        /// <param name="method">The reflected MethodInfo.</param>
        /// <param name="owner">The object that contains the method.</param>
        /// <param name="name">The method name (used for debugging).</param>
        public SharedMethodArg(MethodInfo method, object owner, string name)
        {
            m_MethodName = name;
            m_Invoker = (Action<T, U>)ReflectionExtensions.CreateDelegate(typeof(Action<T, U>), method, owner);
        }

        /// <summary>
        /// Invoke the method.
        /// </summary>
        /// <param name="value1">The first parameter.</param>
        /// <param name="value2">The second parameter.</param>
        public void Invoke(T value1, U value2)
        {
            if (m_Invoker == null) {
                Debug.LogError("Unable to invoke " + m_MethodName + ": Method does not exist");
                return;
            }

            m_Invoker(value1, value2);
        }
    }

    /// <summary>
    /// A SharedMethod which has one parameter and returns a value.
    /// </summary>
    /// <typeparam name="T">The type of parameter.</typeparam>
    /// <typeparam name="R">The type of value to return.</typeparam>
    public class SharedMethod<T, R> : SharedMethod
    {
        // Internal variables
		private Func<T, R> m_Invoker;
#if UNITY_IPHONE || UNITY_WIIU
        // AOT platforms require a defined handler otherwise they'll throw JIT errors.
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
        private event Func<T, R> Invoker
        {
            add { m_Invoker += value; }
            remove { m_Invoker -= value; }
        }
#endif

        /// <summary>
        /// Constructor for SharedMethod. Creates the delegete.
        /// </summary>
        /// <param name="method">The reflected MethodInfo.</param>
        /// <param name="owner">The object that contains the method.</param>
        /// <param name="name">The method name (used for debugging).</param>
        public SharedMethod(MethodInfo method, object owner, string name)
        {
            m_MethodName = name;
            m_Invoker = (Func<T, R>)ReflectionExtensions.CreateDelegate(typeof(Func<T, R>), method, owner);
        }
        
        /// <summary>
        /// Invoke the method.
        /// </summary>
        /// <param name="value">The first parameter.</param>
        /// <returns>The returned value.</returns>
        public R Invoke(T value)
        {
            if (m_Invoker == null) {
                Debug.LogError("Unable to invoke " + m_MethodName + ": Method does not exist");
                return default(R);
            }

            return m_Invoker(value);
        }
    }

    /// <summary>
    /// A SharedMethod which has two parameters and returns a value.
    /// </summary>
    /// <typeparam name="T">The type of the first parameter.</typeparam>
    /// <typeparam name="U">The type of the second parameter.</typeparam>
    /// <typeparam name="R">The type of value to return.</typeparam>
    public class SharedMethod<T, U, R> : SharedMethod
    {
        // Internal variables
        private Func<T, U, R> m_Invoker;
#if UNITY_IPHONE || UNITY_WIIU
        // AOT platforms require a defined handler otherwise they'll throw JIT errors.
        // http://forum.unity3d.com/threads/executionengineexception-on-ios-only.113750/
        private event Func<T, U, R> Invoker
        {
            add { m_Invoker += value; }
            remove { m_Invoker -= value; }
        }
#endif

        /// <summary>
        /// Constructor for SharedMethod. Creates the delegete.
        /// </summary>
        /// <param name="method">The reflected MethodInfo.</param>
        /// <param name="owner">The object that contains the method.</param>
        /// <param name="name">The method name (used for debugging).</param>
        public SharedMethod(MethodInfo method, object owner, string name)
        {
            m_MethodName = name;
            m_Invoker = (Func<T, U, R>)ReflectionExtensions.CreateDelegate(typeof(Func<T, U, R>), method, owner);
        }

        /// <summary>
        /// Invoke the method.
        /// </summary>
        /// <param name="value1">The first parameter.</param>
        /// <param name="value2">The second parameter.</param>
        /// <returns>The returned value.</returns>
        public R Invoke(T value1, U value2)
        {
            if (m_Invoker == null) {
                Debug.LogError("Unable to invoke " + m_MethodName + ": Method does not exist");
                return default(R);
            }

            return m_Invoker(value1, value2);
        }
    }
}