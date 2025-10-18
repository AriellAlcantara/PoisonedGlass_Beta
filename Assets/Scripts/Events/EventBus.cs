using System;
using System.Collections.Generic;
using UnityEngine;

namespace GNW2.Events
{
    /// <summary>
    /// Simple event bus for decoupled communication between systems
    /// </summary>
    public static class EventBus
    {
        private static Dictionary<Type, Delegate> _eventDictionary = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Subscribe to an event
        /// </summary>
        public static void Subscribe<T>(Action<T> listener) where T : IGameEvent
        {
            Type eventType = typeof(T);

            if (_eventDictionary.TryGetValue(eventType, out Delegate existingDelegate))
            {
                _eventDictionary[eventType] = Delegate.Combine(existingDelegate, listener);
            }
            else
            {
                _eventDictionary[eventType] = listener;
            }
        }

        /// <summary>
        /// Unsubscribe from an event
        /// </summary>
        public static void Unsubscribe<T>(Action<T> listener) where T : IGameEvent
        {
            Type eventType = typeof(T);

            if (_eventDictionary.TryGetValue(eventType, out Delegate existingDelegate))
            {
                Delegate newDelegate = Delegate.Remove(existingDelegate, listener);

                if (newDelegate == null)
                {
                    _eventDictionary.Remove(eventType);
                }
                else
                {
                    _eventDictionary[eventType] = newDelegate;
                }
            }
        }

        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        public static void Publish<T>(T gameEvent) where T : IGameEvent
        {
            Type eventType = typeof(T);

            if (_eventDictionary.TryGetValue(eventType, out Delegate eventDelegate))
            {
                Action<T> callback = eventDelegate as Action<T>;
                callback?.Invoke(gameEvent);
            }
        }

        /// <summary>
        /// Clear all event subscriptions (useful for scene transitions)
        /// </summary>
        public static void Clear()
        {
            _eventDictionary.Clear();
        }

        /// <summary>
        /// Clear subscriptions for a specific event type
        /// </summary>
        public static void Clear<T>() where T : IGameEvent
        {
            Type eventType = typeof(T);
            if (_eventDictionary.ContainsKey(eventType))
            {
                _eventDictionary.Remove(eventType);
            }
        }
    }
}
