using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal static class PlacementRelations
    {
        public const float GapReachMm = 500f;

        public const int MaxTouchesPerFace = 3;

        private const int BottomFace = 2;

        private static readonly string[] FaceNames = { "left", "right", "bottom", "top", "back", "front" };

        public static PlacementRelationSet Of(BoxMm subject, IReadOnlyList<NeighbourBox> others)
        {
            var set = new PlacementRelationSet();
            var candidates = new List<NeighbourBox>(others.Count);
            foreach (var other in others)
            {
                float depth = PenetrationMm(subject, other.Box);
                if (depth > 0f && !Tolerance.IsNoiseMm(depth))
                {
                    set.Overlaps.Add(new PlacementOverlap { n = other.Name, depthMm = depth });
                    continue;
                }
                candidates.Add(other);
            }
            set.Overlaps.Sort((a, b) => b.depthMm.CompareTo(a.depthMm));

            for (int face = 0; face < FaceNames.Length; face++)
                CollectFace(subject, candidates, face, set);
            return set;
        }

        public static float PenetrationMm(BoxMm a, BoxMm b)
        {
            float depth = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float overlap = Mathf.Min(a.Max[axis], b.Max[axis]) - Mathf.Max(a.Min[axis], b.Min[axis]);
                if (overlap <= 0f) return 0f;
                depth = Mathf.Min(depth, overlap);
            }
            return depth;
        }

        private static void CollectFace(BoxMm subject, List<NeighbourBox> candidates, int face, PlacementRelationSet set)
        {
            int axis = face / 2;
            bool maxSide = face % 2 == 1;
            string name = FaceNames[face];
            int touching = 0;
            float bestAreaMm2 = 0f;
            NeighbourBox? nearest = null;
            float nearestGap = float.MaxValue;

            foreach (var other in candidates)
            {
                if (!ProjectionsOverlapExcept(subject, other.Box, axis)) continue;
                float gap = maxSide
                    ? other.Box.Min[axis] - subject.Max[axis]
                    : subject.Min[axis] - other.Box.Max[axis];

                if (Tolerance.IsNoiseMm(gap))
                {
                    if (touching++ < MaxTouchesPerFace)
                        set.Touches.Add(new PlacementContact { n = other.Name, face = name });
                    float area = ProjectedAreaMm2(subject, other.Box, axis);
                    if (face == BottomFace && area > bestAreaMm2) { bestAreaMm2 = area; set.On = other.Name; }
                }
                else if (gap > 0f && gap < nearestGap)
                {
                    nearestGap = gap;
                    nearest = other;
                }
            }

            if (touching == 0 && nearest.HasValue && nearestGap <= GapReachMm)
                set.Gaps.Add(new PlacementGap { n = nearest.Value.Name, face = name, gapMm = nearestGap });
        }

        private static bool ProjectionsOverlapExcept(BoxMm a, BoxMm b, int axis)
        {
            for (int other = 0; other < 3; other++)
                if (other != axis
                    && !Tolerance.IntervalsOverlap(a.Min[other], a.Max[other], b.Min[other], b.Max[other], Tolerance.ClearanceMm))
                    return false;
            return true;
        }

        private static float ProjectedAreaMm2(BoxMm a, BoxMm b, int axis)
        {
            float area = 1f;
            for (int other = 0; other < 3; other++)
            {
                if (other == axis) continue;
                area *= Mathf.Max(0f, Mathf.Min(a.Max[other], b.Max[other]) - Mathf.Max(a.Min[other], b.Min[other]));
            }
            return area;
        }
    }
}
