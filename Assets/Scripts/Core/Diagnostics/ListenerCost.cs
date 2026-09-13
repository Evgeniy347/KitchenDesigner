using System;
using System.Diagnostics;

namespace KitchenDesigner.Core
{
    public static class ListenerCost
    {
        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        public static void Dispatch<T>(Action<T?>? listeners, T? arg) where T : class
        {
            if (listeners == null) return;

            if (!PerfMarkers.Measuring)
            {
                listeners(arg);
                return;
            }

            var each = listeners.GetInvocationList();
            for (int i = 0; i < each.Length; i++)
            {
                var one = (Action<T?>)each[i];
                long started = Stopwatch.GetTimestamp();
                one(arg);
                ListenerCostLog.Note(NameOf(one),
                    (float)((Stopwatch.GetTimestamp() - started) * MillisecondsPerTick));
            }
        }

        public static string NameOf(Delegate one)
        {
            var owner = one.Target != null ? one.Target.GetType() : one.Method.DeclaringType;
            return owner != null ? owner.Name : "?";
        }
    }
}
