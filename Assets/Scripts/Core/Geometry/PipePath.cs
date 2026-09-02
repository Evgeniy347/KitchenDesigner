using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PipePath
    {
        public const int DefaultArcSegments = 12;

        public static Vector3[] Arc(Vector3 centreMM, Vector3 startAxis, Vector3 sweepAxis,
            float radiusMM, float sweepDeg, int segments)
        {
            int count = Mathf.Max(1, segments);
            var start = startAxis.normalized;
            var sweep = sweepAxis.normalized;
            var points = new Vector3[count + 1];

            for (int i = 0; i <= count; i++)
            {
                float angle = sweepDeg * Mathf.Deg2Rad * i / count;
                points[i] = centreMM
                    + start * (radiusMM * Mathf.Cos(angle))
                    + sweep * (radiusMM * Mathf.Sin(angle));
            }

            return points;
        }

        public static Vector3[] Chain(params Vector3[][] parts)
        {
            var chained = new List<Vector3>();

            foreach (var part in parts)
            {
                if (part == null) continue;
                foreach (var point in part)
                {
                    if (chained.Count > 0 &&
                        (point - chained[chained.Count - 1]).sqrMagnitude <= Tolerance.EpsilonSqr)
                        continue;
                    chained.Add(point);
                }
            }

            return chained.ToArray();
        }

        public static float LengthMM(IReadOnlyList<Vector3> pointsMM)
        {
            float total = 0f;
            for (int i = 1; i < pointsMM.Count; i++)
                total += (pointsMM[i] - pointsMM[i - 1]).magnitude;
            return total;
        }

        public static Vector3 LowestPoint(IReadOnlyList<Vector3> pointsMM)
        {
            var lowest = pointsMM[0];
            for (int i = 1; i < pointsMM.Count; i++)
                if (pointsMM[i].y < lowest.y) lowest = pointsMM[i];
            return lowest;
        }
    }
}
