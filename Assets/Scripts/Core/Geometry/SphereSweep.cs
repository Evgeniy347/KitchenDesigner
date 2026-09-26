using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SphereSweep
    {
        public static void AddOverlappingPairs(int count,
            Func<int, (Vector3 center, float radius)> sphereOf, float slack,
            Action<int, int> onPair)
        {
            if (count < 2) return;

            var order = new int[count];
            var lo = new float[count];
            var hi = new float[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                var (center, radius) = sphereOf(i);
                float expanded = radius + slack;
                lo[i] = center.x - expanded;
                hi[i] = center.x + expanded;
            }

            Array.Sort(order, (a, b) => lo[a].CompareTo(lo[b]));

            var active = new List<int>();
            foreach (int i in order)
            {
                for (int a = active.Count - 1; a >= 0; a--)
                    if (hi[active[a]] < lo[i]) active.RemoveAt(a);

                for (int a = 0; a < active.Count; a++)
                    onPair(active[a], i);

                active.Add(i);
            }
        }
    }
}
