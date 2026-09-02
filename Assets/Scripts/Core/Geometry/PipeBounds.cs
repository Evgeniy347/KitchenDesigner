using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PipeBounds
    {
        public static Bounds Of(IReadOnlyList<PipeSegment> segments)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var direction = segment.Direction;
                Grow(segment.FromMM, DiscExtentMM(direction, segment.FromRadiusMM), ref min, ref max);
                Grow(segment.ToMM, DiscExtentMM(direction, segment.ToRadiusMM), ref min, ref max);
            }

            return FromMinMax(min, max);
        }

        public static Bounds OfPolyline(IReadOnlyList<Vector3> pointsMM, float radiusMM)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var extent = new Vector3(radiusMM, radiusMM, radiusMM);

            for (int i = 0; i < pointsMM.Count; i++) Grow(pointsMM[i], extent, ref min, ref max);

            return FromMinMax(min, max);
        }

        public static Vector3 DiscExtentMM(Vector3 direction, float radiusMM) =>
            new Vector3(
                radiusMM * PerpendicularFactor(direction.x),
                radiusMM * PerpendicularFactor(direction.y),
                radiusMM * PerpendicularFactor(direction.z));

        private static float PerpendicularFactor(float axisComponent)
        {
            float remainder = 1f - axisComponent * axisComponent;
            return remainder <= 0f ? 0f : Mathf.Sqrt(remainder);
        }

        private static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            if (min.x > max.x) return new Bounds(Vector3.zero, Vector3.zero);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static void Grow(Vector3 centreMM, Vector3 extentMM, ref Vector3 min, ref Vector3 max)
        {
            min = Vector3.Min(min, centreMM - extentMM);
            max = Vector3.Max(max, centreMM + extentMM);
        }
    }
}
