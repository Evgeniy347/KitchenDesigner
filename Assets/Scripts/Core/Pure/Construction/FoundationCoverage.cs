using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationCoverage
    {
        public static bool IsSegmentCovered(WallCentreline wall,
            IReadOnlyList<FoundationPolyline> polylines)
        {
            if (!wall.IsDefined || polylines == null) return false;

            foreach (var polyline in polylines)
            {
                var points = polyline.Points;
                if (points == null) continue;

                for (int i = 0; i + 1 < points.Count; i++)
                    if (IsSameSegment(wall.Start, wall.End, points[i], points[i + 1]))
                        return true;
            }

            return false;
        }

        private static bool IsSameSegment(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1) =>
            (SamePointInPlan(a0, b0) && SamePointInPlan(a1, b1))
            || (SamePointInPlan(a0, b1) && SamePointInPlan(a1, b0));

        private static bool SamePointInPlan(Vector3 a, Vector3 b)
        {
            float limit = WallCentreline.CornerToleranceMm * AppConstants.MM_TO_UNITS;
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz <= limit * limit;
        }
    }
}
