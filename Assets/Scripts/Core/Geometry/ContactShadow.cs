using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ContactShadow
    {
        public static void ActivePieces(Bounds closed, Bounds neighbour, float touchGapUnits,
            List<Bounds> into)
        {
            if (!Touches(closed, neighbour, touchGapUnits, out int contactAxis))
            {
                into.Add(neighbour);
                return;
            }

            if (Surrounds(neighbour, closed, touchGapUnits)) return;

            int first = contactAxis == 0 ? 1 : 0;
            int second = contactAxis == 2 ? 1 : 2;

            Beside.Clear();
            if (SplitOutside(neighbour, closed, first, Beside, out var middle))
                SplitOutside(middle, closed, second, Beside, out _);

            for (int i = 0; i < Beside.Count; i++)
                SplitOutside(Beside[i], closed, contactAxis, into, out _);
        }

        [ThreadStatic] private static List<Bounds>? _besidePerThread;

        private static List<Bounds> Beside => _besidePerThread ??= new List<Bounds>();

        public static bool Touches(Bounds a, Bounds b, float touchGapUnits, out int contactAxis)
        {
            contactAxis = 0;
            float widest = float.MinValue;
            bool touching = true;
            for (int i = 0; i < 3; i++)
            {
                float gap = Mathf.Max(a.min[i] - b.max[i], b.min[i] - a.max[i]);
                if (gap > touchGapUnits) touching = false;
                if (gap > widest) { widest = gap; contactAxis = i; }
            }
            return touching;
        }

        public static bool Surrounds(Bounds outer, Bounds inner, float touchGapUnits)
        {
            for (int i = 0; i < 3; i++)
                if (outer.min[i] > inner.min[i] + touchGapUnits
                    || outer.max[i] < inner.max[i] - touchGapUnits)
                    return false;
            return true;
        }

        private static bool SplitOutside(Bounds box, Bounds closed, int axis, List<Bounds> into,
            out Bounds middle)
        {
            var min = box.min;
            var max = box.max;
            float low = closed.min[axis];
            float high = closed.max[axis];

            if (min[axis] < low - Tolerance.EpsilonUnits)
            {
                var pieceMax = max;
                pieceMax[axis] = Mathf.Min(max[axis], low);
                into.Add(FromMinMax(min, pieceMax));
            }
            if (max[axis] > high + Tolerance.EpsilonUnits)
            {
                var pieceMin = min;
                pieceMin[axis] = Mathf.Max(min[axis], high);
                into.Add(FromMinMax(pieceMin, max));
            }

            var middleMin = min;
            middleMin[axis] = Mathf.Max(min[axis], low);
            var middleMax = max;
            middleMax[axis] = Mathf.Min(max[axis], high);
            middle = FromMinMax(middleMin, middleMax);
            return middleMax[axis] - middleMin[axis] > Tolerance.EpsilonUnits;
        }

        private static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
