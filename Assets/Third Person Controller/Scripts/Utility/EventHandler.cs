using UnityEngine;
using System;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Adds a generic event system. The event system allows objects to register, unregister, and execute events on a particular object.
    /// </summary>
    public class EventHandler : MonoBehaviour
    {
        // Internal variables
        private static Dictionary<object, Dictionary<string, Delegate>> s_EventTable = new Dictionary<object, Dictionary<string, Delegate>>();
        private static Dictionary<string, Delegate> s_GlobalEventTable = new Dictionary<string, Delegate>();
        
#if !ENABLE_MULTIPLAYER
        /// <summary>
        /// Clear the event table when the component is started. This will prevent events from persisting across scenes. Some events within the 
        /// global event table do need to persist across scenes so instead of clearing the global event table just send a new event to those objects who should be removed.
        /// </summary>
        private void Awake()
        {
            ClearTable();
        }
#endif

        /// <summary>
        /// Clear the event table when the GameObject is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (gameObject != null && !gameObject.activeSelf) {
                return;
            }

            ClearTable();
        }

        /// <summary>
        /// Clear the event table when the GameObject is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            ClearTable();
        }

        /// <summary>
        /// Clears the actual events.
        /// </summary>
        private void ClearTable()
        {
            s_EventTable.Clear();
            EventHandler.ExecuteEvent("OnEventHandlerClear");
        }

        /// <summary>
        /// Registers a new global event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        private static void RegisterEvent(string eventName, Delegate handler)
        {
            Delegate prevHandlers;
            if (s_GlobalEventTable.TryGetValue(eventName, out prevHandlers)) {
                s_GlobalEventTable[eventName] = Delegate.Combine(prevHandlers, handler);
            } else {
                s_GlobalEventTable.Add(eventName, handler);
            }
        }

        /// <summary>
        /// Registers a new event.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        private static void RegisterEvent(object obj, string eventName, Delegate handler)
        {
#if UNITY_EDITOR
            if (obj == null) {
                Debug.LogError("EventHandler.RegisterEvent error: target object cannot be null.");
                return;
            }
#endif
            Dictionary<string, Delegate> handlers;
            if (!s_EventTable.TryGetValue(obj, out handlers)) {
                handlers = new Dictionary<string, Delegate>();
                s_EventTable.Add(obj, handlers);
            }

            Delegate prevHandlers;
            if (handlers.TryGetValue(eventName, out prevHandlers)) {
                handlers[eventName] = Delegate.Combine(prevHandlers, handler);
            } else {
                handlers.Add(eventName, handler);
            }
        }

        /// <summary>
        /// Returns the delegate for a particular global event.
        /// </summary>
        /// <param name="eventName">The interested event name.</param>
        /// <returns>The delegate for the interested event.</returns>
        private static Delegate GetDelegate(string eventName)
        {
            Delegate handler;
            if (s_GlobalEventTable.TryGetValue(eventName, out handler)) {
                return handler;
            }
            return null;
        }

        /// <summary>
        /// Returns the delegate for a particular event on a particular object.
        /// </summary>
        /// <param name="obj">The interested object.</param>
        /// <param name="eventName">The interested event name.</param>
        /// <returns>The delegate for the interested event.</returns>
        private static Delegate GetDelegate(object obj, string eventName)
        {
            Dictionary<string, Delegate> handlers;
            if (s_EventTable.TryGetValue(obj, out handlers)) {
                Delegate handler;
                if (handlers.TryGetValue(eventName, out handler)) {
                    return handler;
                }
            }
            return null;
        }

        /// <summary>
        /// Unregisters the specified global event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        private static void UnregisterEvent(string eventName, Delegate handler)
        {
            Delegate prevHandlers;
            if (s_GlobalEventTable.TryGetValue(eventName, out prevHandlers)) {
                s_GlobalEventTable[eventName] = Delegate.Remove(prevHandlers, handler);
            }
        }

        /// <summary>
        /// Unregisters the specified event.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        private static void UnregisterEvent(object obj, string eventName, Delegate handler)
        {
#if UNITY_EDITOR
            if (obj == null) {
                Debug.LogError("EventHandler.UnregisterEvent error: target object cannot be null.");
                return;
            }
#endif
            Dictionary<string, Delegate> handlers;
            if (s_EventTable.TryGetValue(obj, out handlers)) {
                Delegate prevHandlers;
                if (handlers.TryGetValue(eventName, out prevHandlers)) {
                    handlers[eventName] = Delegate.Remove(prevHandlers, handler);
                }
            }
        }

        /// <summary>
        /// Register a new global event with no parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent(string eventName, Action handler)
        {
            RegisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new event with no parameters.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent(object obj, string eventName, Action handler)
        {
            RegisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new global event with one parameter.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T>(string eventName, Action<T> handler)
        {
            RegisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new event with one parameter.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T>(object obj, string eventName, Action<T> handler)
        {
            RegisterEvent(obj, eventName, (Delegate)handler);
        }


        /// <summary>
        /// Register a new global event with two parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T, U>(string eventName, Action<T, U> handler)
        {
            RegisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new event with two parameters.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T, U>(object obj, string eventName, Action<T, U> handler)
        {
            RegisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new global event with three parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T, U, V>(string eventName, Action<T, U, V> handler)
        {
            RegisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new event with three parameters.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T, U, V>(object obj, string eventName, Action<T, U, V> handler)
        {
            RegisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new global event with three parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T, U, V, W>(string eventName, Action<T, U, V, W> handler)
        {
            RegisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Register a new event with three parameters.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The function to call when the event executes.</param>
        public static void RegisterEvent<T, U, V, W>(object obj, string eventName, Action<T, U, V, W> handler)
        {
            RegisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Executes the global event with no parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        public static void ExecuteEvent(string eventName)
        {
            var handler = GetDelegate(eventName) as Action;
            if (handler != null) {
                handler();
            }
        }

        /// <summary>
        /// Executes the event with no parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        public static void ExecuteEvent(object obj, string eventName)
        {
            var handler = GetDelegate(obj, eventName) as Action;
            if (handler != null) {
                handler();
            }
        }

        /// <summary>
        /// Executes the global event with one parameter.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        public static void ExecuteEvent<T>(string eventName, T arg1)
        {
            var handler = GetDelegate(eventName) as Action<T>;
            if (handler != null) {
                handler(arg1);
            }
        }

        /// <summary>
        /// Executes the event with one parameter.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        public static void ExecuteEvent<T>(object obj, string eventName, T arg1)
        {
            var handler = GetDelegate(obj, eventName) as Action<T>;
            if (handler != null) {
                handler(arg1);
            }
        }

        /// <summary>
        /// Executes the global event with two parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        /// <param name="arg2">The second parameter.</param>
        public static void ExecuteEvent<T, U>(string eventName, T arg1, U arg2)
        {
            var handler = GetDelegate(eventName) as Action<T, U>;
            if (handler != null) {
                handler(arg1, arg2);
            }
        }

        /// <summary>
        /// Executes the event with two parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        /// <param name="arg2">The second parameter.</param>
        public static void ExecuteEvent<T, U>(object obj, string eventName, T arg1, U arg2)
        {
            var handler = GetDelegate(obj, eventName) as Action<T, U>;
            if (handler != null) {
                handler(arg1, arg2);
            }
        }

        /// <summary>
        /// Executes the global event with three parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        /// <param name="arg2">The second parameter.</param>
        /// <param name="arg3">The third parameter.</param>
        public static void ExecuteEvent<T, U, V>(string eventName, T arg1, U arg2, V arg3)
        {
            var handler = GetDelegate(eventName) as Action<T, U, V>;
            if (handler != null) {
                handler(arg1, arg2, arg3);
            }
        }

        /// <summary>
        /// Executes the event with three parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        /// <param name="arg2">The second parameter.</param>
        /// <param name="arg3">The third parameter.</param>
        public static void ExecuteEvent<T, U, V>(object obj, string eventName, T arg1, U arg2, V arg3)
        {
            var handler = GetDelegate(obj, eventName) as Action<T, U, V>;
            if (handler != null) {
                handler(arg1, arg2, arg3);
            }
        }

        /// <summary>
        /// Executes the global event with three parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        /// <param name="arg2">The second parameter.</param>
        /// <param name="arg3">The third parameter.</param>
        /// <param name="arg4">The fourth parameter.</param>
        public static void ExecuteEvent<T, U, V, W>(string eventName, T arg1, U arg2, V arg3, W arg4)
        {
            var handler = GetDelegate(eventName) as Action<T, U, V, W>;
            if (handler != null) {
                handler(arg1, arg2, arg3, arg4);
            }
        }

        /// <summary>
        /// Executes the event with three parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="arg1">The first parameter.</param>
        /// <param name="arg2">The second parameter.</param>
        /// <param name="arg3">The third parameter.</param>
        /// <param name="arg4">The fourth parameter.</param>
        public static void ExecuteEvent<T, U, V, W>(object obj, string eventName, T arg1, U arg2, V arg3, W arg4)
        {
            var handler = GetDelegate(obj, eventName) as Action<T, U, V, W>;
            if (handler != null) {
                handler(arg1, arg2, arg3, arg4);
            }
        }

        /// <summary>
        /// Unregisters the specified global event.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent(string eventName, Action handler)
        {
            UnregisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified event.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent(object obj, string eventName, Action handler)
        {
            UnregisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified global event with one parameter.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T>(string eventName, Action<T> handler)
        {
            UnregisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified event with one parameter.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T>(object obj, string eventName, Action<T> handler)
        {
            UnregisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified global event with two parameters.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T, U>(string eventName, Action<T, U> handler)
        {
            UnregisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified event with two parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T, U>(object obj, string eventName, Action<T, U> handler)
        {
            UnregisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified global event with three parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T, U, V>(string eventName, Action<T, U, V> handler)
        {
            UnregisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified event with three parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T, U, V>(object obj, string eventName, Action<T, U, V> handler)
        {
            UnregisterEvent(obj, eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified global event with three parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T, U, V, W>(string eventName, Action<T, U, V, W> handler)
        {
            UnregisterEvent(eventName, (Delegate)handler);
        }

        /// <summary>
        /// Unregisters the specified event with three parameters.
        /// </summary>
        /// <param name="obj">The object that the event is attached to.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="handler">The event delegate to remove.</param>
        public static void UnregisterEvent<T, U, V, W>(object obj, string eventName, Action<T, U, V, W> handler)
        {
            UnregisterEvent(obj, eventName, (Delegate)handler);
        }
    }
}