using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BoxOverlap
    {
        public const int SeparatingAxisCount = 15;

        public static float PenetrationUnits(in OrientedBox a, in OrientedBox b)
        {
            Span<Vector3> axes = stackalloc Vector3[SeparatingAxisCount];
            int count = 0;
            for (int i = 0; i < 3; i++) axes[count++] = a.Axis(i);
            for (int i = 0; i < 3; i++) axes[count++] = b.Axis(i);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var cross = Vector3.Cross(a.Axis(i), b.Axis(j));
                    if (cross.sqrMagnitude < Tolerance.EpsilonSqr) continue;
                    axes[count++] = cross;
                }

            var between = b.Center - a.Center;
            float smallest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var axis = axes[i].normalized;
                float overlap = a.RadiusAlong(axis) + b.RadiusAlong(axis)
                    - Mathf.Abs(Vector3.Dot(between, axis));
                if (overlap <= 0f) return 0f;
                if (overlap < smallest) smallest = overlap;
            }
            return smallest;
        }

        public static bool Overlap(in OrientedBox a, in OrientedBox b, float minPenetrationUnits) =>
            PenetrationUnits(a, b) > minPenetrationUnits;
    }
}
