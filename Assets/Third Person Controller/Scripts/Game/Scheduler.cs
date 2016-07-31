using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Used by the Scheduler, the ScheduledEvent contains the delegate and arguments for the event that should execute at a time in the future.
    /// </summary>
    public class ScheduledEvent
    {
        /// <summary>
        /// Returns and Sets the callback for the ScheduledEvent.
        /// </summary>
        public Action Callback { get { return m_Callback; } set { m_Callback = value; } }
        /// <summary>
        /// Returns and Sets the callback for the ScheduledEvent.
        /// </summary>
        public Action<object> CallbackArg { get { return m_CallbackArg; } set { m_CallbackArg = value; } }
        /// <summary>
        /// Optional argument to use as a parameter for the executed event.
        /// </summary>
        public object Argument { get { return m_Argument; } set { m_Argument = value; } }
        /// <summary>
        /// The time that the event should execute.
        /// </summary>
        public float EndTime { get { return m_EndTime; } set { m_EndTime = value; } }

        // Internal variables.
        private Action m_Callback = null;
        private Action<object> m_CallbackArg = null;
        private object m_Argument;
        private float m_EndTime;

        /// <summary>
        /// ScheduledEvents are pooled so reset the variables back to their default values.
        /// </summary>
        public void Reset()
        {
            m_Callback = null;
            m_CallbackArg = null;
            m_Argument = null;
        }
    }

    /// <summary>
    /// The scheduler will execute a ScheduledEvent after the specified amount of time.
    /// </summary>
    public class Scheduler : MonoBehaviour
    {
        // Static variables
        private static Scheduler s_Instance;
        private static Scheduler Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!m_Initialized) {
                    Debug.LogWarning("Warning: Scheduler is null. A GameObject has been created with the component automatically added. Please run Scene Setup from the Start Window.");
                    s_Instance = new GameObject("Scheduler").AddComponent<Scheduler>();
                }
#endif
                return s_Instance;
            }
        }

        /// <summary>
        /// Gets the list of active events. Used by the Scheduler editor class.
        /// </summary>
        public List<ScheduledEvent> ActiveEvents { get { return m_ActiveEvents; } }

        // Internal variables
#if UNITY_EDITOR
        private static bool m_Initialized;
#endif
        private List<ScheduledEvent> m_ActiveEvents = new List<ScheduledEvent>();

        /// <summary>
        /// Assign the static variables and register for any events.
        /// </summary>
        private void OnEnable()
        {
            s_Instance = this;
#if UNITY_EDITOR
            m_Initialized = true;
#endif
#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
            EventHandler.RegisterEvent("OnNetworkStopClient", OnStopClient);
#endif
        }

        /// <summary>
        /// Remove all active events and unregister for any events when disabled.
        /// </summary>
        private void OnDisable()
        {
            m_ActiveEvents.Clear();
#if ENABLE_MULTIPLAYER
            EventHandler.UnregisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
            EventHandler.UnregisterEvent("OnNetworkStopClient", OnStopClient);
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// A new client has just joined the server. The Scheduler may be disabled so enable it.
        /// </summary>
        /// <param name="netConn">The client connection.</param>
        private void OnServerReady(NetworkConnection netConn)
        {
            enabled = true;
        }

        /// <summary>
        /// The client has left the network game. Disable the scheduler so no more events will be called.
        /// </summary>
        private void OnStopClient()
        {
            m_ActiveEvents.Clear();
            enabled = false;
        }
#endif

        /// <summary>
        /// Loop through the ScheduledEvents and execute the event when the current time is greater than or equal to the end time of the ScheduledEvent.
        /// </summary>
        private void Update()
        {
            for (int i = m_ActiveEvents.Count - 1; i > -1; --i) {
                if (m_ActiveEvents[i].EndTime <= Time.time) {
                    Execute(i);
                }
            }
        }

        /// <summary>
        /// Schedule a new event to occur after the specified delay.
        /// </summary>
        /// <param name="delay">The time to wait before triggering the event.</param>
        /// <param name="callback">The event to occur.</param>
        /// <returns>The ScheduledEvent, allows for cancelling.</returns>
        public static ScheduledEvent Schedule(float delay, Action callback)
        {
            if (Instance == null) {
                return null;
            }

            return Instance.AddEventInternal(delay, callback);
        }

        /// <summary>
        /// Internal method to add a new event to be executed in the future.
        /// </summary>
        /// <param name="delay">The delay from the current time to execute the event.</param>
        /// <param name="callback">The delegate to execute after the specified delay.</param>
        /// <returns>The ScheduledEvent instance, useful if the event should be cancelled.</returns>
        private ScheduledEvent AddEventInternal(float delay, Action callback)
        {
            // Don't add the event if the game hasn't started.
            if (enabled == false) {
                return null;
            }

            if (delay == 0) {
                callback();
                return null;
            } else {
                var scheduledEvent = ObjectPool.Get<ScheduledEvent>();
                scheduledEvent.Reset();
                scheduledEvent.EndTime = Time.time + delay;
                scheduledEvent.Callback = callback;
                m_ActiveEvents.Add(scheduledEvent);
                return scheduledEvent;
            }
        }

        /// <summary>
        /// Add a new event with an argumentto be executed in the future.
        /// </summary>
        /// <param name="delay">The delay from the current time to execute the event.</param>
        /// <param name="callback">The delegate to execute after the specified delay.</param>
        /// <param name="arg">The argument of the delegate.</param>
        /// <returns>The ScheduledEvent instance, useful if the event should be cancelled.</returns>
        public static ScheduledEvent Schedule(float delay, Action<object> callback, object arg)
        {
            if (Instance == null) {
                return null;
            }

            return Instance.AddEventInternal(delay, callback, arg);
        }

        /// <summary>
        /// Internal event to add a new event with an argumentto be executed in the future.
        /// </summary>
        /// <param name="delay">The delay from the current time to execute the event.</param>
        /// <param name="callback">The delegate to execute after the specified delay.</param>
        /// <param name="arg">The argument of the delegate.</param>
        /// <returns>The ScheduledEvent instance, useful if the event should be cancelled.</returns>
        private ScheduledEvent AddEventInternal(float delay, Action<object> callbackArg, object arg)
        {
            if (delay == 0) {
                callbackArg(arg);
                return null;
            } else {
                var scheduledEvent = ObjectPool.Get<ScheduledEvent>();
                scheduledEvent.Reset();
                scheduledEvent.EndTime = Time.time + delay;
                scheduledEvent.CallbackArg = callbackArg;
                scheduledEvent.Argument = arg;
                m_ActiveEvents.Add(scheduledEvent);
                return scheduledEvent;
            }
        }

        /// <summary>
        /// Cancels an event.
        /// </summary>
        /// <param name="scheduledEvent">The event to cancel.</param>
        public static void Cancel(ref ScheduledEvent scheduledEvent)
        {
            Instance.CancelEventInternal(ref scheduledEvent);
        }

        /// <summary>
        /// Internal method to cancel an event.
        /// </summary>
        /// <param name="scheduledEvent">The event to cancel.</param>
        private void CancelEventInternal(ref ScheduledEvent scheduledEvent)
        {
            if (scheduledEvent != null && m_ActiveEvents.Contains(scheduledEvent)) {
                m_ActiveEvents.Remove(scheduledEvent);
                ObjectPool.Return(scheduledEvent);
                scheduledEvent = null;
            }
        }

        /// <summary>
        /// Executes an event with the specified index.
        /// </summary>
        /// <param name="index">The index of the event to execute.</param>
        private void Execute(int index)
        {
            var activeEvent = m_ActiveEvents[index];
            // Remove the event from the list before the callback to prevent the callback from adding a new event and changing the order.
            m_ActiveEvents.RemoveAt(index);
            if (activeEvent.Callback != null) {
                activeEvent.Callback();
            } else {
                activeEvent.CallbackArg(activeEvent.Argument);
            }
            ObjectPool.Return(activeEvent);
        }
    }
}