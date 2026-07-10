using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            lock (_events)
            {
                if (_events.TryGetValue(type, out Delegate del))
                    _events[type] = Delegate.Combine(del, handler);
                else
                    _events[type] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            lock (_events)
            {
                if (_events.TryGetValue(type, out Delegate del))
                {
                    Delegate result = Delegate.Remove(del, handler);
                    if (result == null)
                        _events.Remove(type);
                    else
                        _events[type] = result;
                }
            }
        }

        public static void Publish<T>(T eventData) where T : struct
        {
            Type type = typeof(T);
            Delegate del;
            lock (_events)
            {
                if (!_events.TryGetValue(type, out del))
                    return;
            }
            (del as Action<T>)?.Invoke(eventData);
        }

        public static void Clear()
        {
            lock (_events)
            {
                _events.Clear();
            }
        }
    }

    public struct ElementCreatedEvent
    {
        public KitchenElement element;
    }

    public struct ElementMovedEvent
    {
        public KitchenElement element;
        public Vector3Serializer previousPosition;
    }

    public struct ProjectLoadedEvent { }

    public struct SelectionChangedEvent
    {
        public KitchenElement previous;
        public KitchenElement current;
    }

    public struct BoardRemovedEvent
    {
        public KitchenElement element;
    }

    public struct Vector3Serializer
    {
        public float x, y, z;
        public Vector3Serializer(UnityEngine.Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x, y, z);
    }
}
