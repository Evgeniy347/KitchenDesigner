using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    public sealed class MainThreadQueue : MonoBehaviour, IMainThread
    {
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public void Post(Action action)
        {
            if (action == null) return;
            _queue.Enqueue(action);
        }

        internal int Drain()
        {
            int ran = 0;
            while (_queue.TryDequeue(out var action))
            {
                ran++;
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            return ran;
        }

        private void Update() => Drain();
    }
}
