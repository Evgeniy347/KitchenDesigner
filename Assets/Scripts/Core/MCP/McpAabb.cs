using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpAabb
    {
        public const float MINOR_OVERLAP_MAX_MM = 2f;
        public const float DEEP_PENETRATION_MIN_MM = 10f;

        public static AabbInfo Of(Vector3[] vertices)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in vertices)
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
            }
            return new AabbInfo { minX = minX, minY = minY, minZ = minZ, maxX = maxX, maxY = maxY, maxZ = maxZ };
        }

        public static bool Overlap(AabbInfo a, AabbInfo b) =>
            a.minX < b.maxX && a.maxX > b.minX &&
            a.minY < b.maxY && a.maxY > b.minY &&
            a.minZ < b.maxZ && a.maxZ > b.minZ;

        public static float Side(AabbInfo aabb, int axis, bool maxSide)
        {
            if (axis == 0) return maxSide ? aabb.maxX : aabb.minX;
            if (axis == 1) return maxSide ? aabb.maxY : aabb.minY;
            return maxSide ? aabb.maxZ : aabb.minZ;
        }

        public static Vector3Int EffectiveDimMM(KitchenElement el)
        {
            if (!el.SupportsGaps || el.GapMM == 0) return el.DimensionsMM;
            var g = el.Gaps;
            return new Vector3Int(
                el.DimensionsMM.x + g.Left + g.Right,
                el.DimensionsMM.y + g.Top + g.Bottom,
                el.DimensionsMM.z + g.Front + g.Back);
        }

        public static string ClassifyOverlapMm(float mm) =>
            Tolerance.IsNoiseMm(mm) ? "touching"
            : mm < MINOR_OVERLAP_MAX_MM ? "minor_overlap"
            : mm < DEEP_PENETRATION_MIN_MM ? "overlap"
            : "deep_penetration";

        public static bool ProjectionsOverlapExceptAxis(AabbInfo a, AabbInfo b, int axis)
        {
            if (axis != 0 && !Tolerance.IntervalsOverlap(a.minX, a.maxX, b.minX, b.maxX)) return false;
            if (axis != 1 && !Tolerance.IntervalsOverlap(a.minY, a.maxY, b.minY, b.maxY)) return false;
            if (axis != 2 && !Tolerance.IntervalsOverlap(a.minZ, a.maxZ, b.minZ, b.maxZ)) return false;
            return true;
        }

        public static List<AxisGapInfo> AxisGaps(KitchenElement element, List<KitchenElement> allElements)
        {
            var elAabb = Of(element.GetVertices());
            var gaps = new List<AxisGapInfo>();
            string[] axisNames = { "x", "y", "z" };
            float[] aMin = { elAabb.minX, elAabb.minY, elAabb.minZ };
            float[] aMax = { elAabb.maxX, elAabb.maxY, elAabb.maxZ };

            var others = new List<(KitchenElement el, AabbInfo aabb)>(allElements.Count);
            foreach (var other in allElements)
                if (other != null && other != element)
                    others.Add((other, Of(other.GetVertices())));

            for (int axis = 0; axis < 3; axis++)
            {
                float bestGapUnits = float.MaxValue;
                string? bestNeighbor = null;
                float am = aMin[axis], ax = aMax[axis];

                foreach (var (other, oAabb) in others)
                {
                    if (!ProjectionsOverlapExceptAxis(elAabb, oAabb, axis)) continue;

                    float bMin = Side(oAabb, axis, false);
                    float bMax = Side(oAabb, axis, true);

                    float gap;
                    if (ax <= bMin) gap = bMin - ax;
                    else if (bMax <= am) gap = am - bMax;
                    else gap = -(Mathf.Min(ax, bMax) - Mathf.Max(am, bMin));

                    if (Mathf.Abs(gap) < Mathf.Abs(bestGapUnits))
                    {
                        bestGapUnits = gap;
                        bestNeighbor = other.PartName;
                    }
                }

                if (bestNeighbor == null) continue;

                float gapMM = bestGapUnits / AppConstants.MM_TO_UNITS;
                bool touching = Tolerance.IsNoiseMm(gapMM);
                gaps.Add(new AxisGapInfo
                {
                    axis = axisNames[axis],
                    neighbor = bestNeighbor,
                    gapMM = touching ? 0f : gapMM,
                    touching = touching,
                    isOverlap = !touching && gapMM < 0
                });
            }
            return gaps;
        }
    }
}
