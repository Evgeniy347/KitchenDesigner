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

            var centers = new Vector3[count];
            var radii = new float[count];
            Vector3 min = default, max = default;
            for (int i = 0; i < count; i++)
            {
                var (center, radius) = sphereOf(i);
                centers[i] = center;
                radii[i] = radius;
                if (i == 0) { min = center; max = center; }
                else { min = Vector3.Min(min, center); max = Vector3.Max(max, center); }
            }

            int axis = WidestSpreadAxis(max - min);

            var order = new int[count];
            var lo = new float[count];
            var hi = new float[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                float expanded = radii[i] + slack;
                float c = centers[i][axis];
                lo[i] = c - expanded;
                hi[i] = c + expanded;
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

        private static int WidestSpreadAxis(Vector3 spread)
        {
            int axis = 0;
            if (spread.y > spread[axis]) axis = 1;
            if (spread.z > spread[axis]) axis = 2;
            return axis;
        }
    }
}
