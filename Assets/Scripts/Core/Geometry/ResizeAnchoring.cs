using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ResizeAnchoring
    {
        public const float ContactUnits = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

        public static Vector3 ShiftUnits(in ElementGeometry before, in ElementGeometry after,
            IReadOnlyList<ElementGeometry>? neighbours)
        {
            if (!IsBox(before) || !IsBox(after) || neighbours == null) return Vector3.zero;
            if (OverlapsAnyNeighbour(before, neighbours)) return Vector3.zero;

            var shift = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                int side = SideToGrowFrom(before, after, neighbours, axis);
                if (side < 0) continue;
                shift += before.Faces[side].normal * (Mathf.Abs(DeltaOn(before, after, axis)) * 0.5f);
            }
            return shift;
        }

        public static ResizeContactRank ContactOn(in ElementGeometry box, int faceIndex,
            IReadOnlyList<ElementGeometry>? neighbours)
        {
            if (!IsBox(box) || neighbours == null) return ResizeContactRank.None;
            if (faceIndex < 0 || faceIndex >= Face.BoxFaceCount) return ResizeContactRank.None;

            Describe(box, out var centre, out var half);
            return RankOnSide(box, centre, half, neighbours, faceIndex / 2, faceIndex % 2 == 0);
        }

        private static int SideToGrowFrom(in ElementGeometry before, in ElementGeometry after,
            IReadOnlyList<ElementGeometry> neighbours, int axis)
        {
            float delta = DeltaOn(before, after, axis);
            if (Mathf.Abs(delta) <= Tolerance.EpsilonUnits) return -1;
            return delta > 0f
                ? FreeSideForGrowth(before, after, neighbours, axis, delta)
                : ContactSideForShrink(before, neighbours, axis);
        }

        private static int FreeSideForGrowth(in ElementGeometry before, in ElementGeometry after,
            IReadOnlyList<ElementGeometry> neighbours, int axis, float delta)
        {
            Describe(before, out var centre, out var halfBefore);
            Describe(after, out _, out var halfAfter);
            var half = LateralFrom(halfBefore, halfAfter, axis);

            float freePlus = FreeUnitsOnSide(before, centre, half, neighbours, axis, true);
            float freeMinus = FreeUnitsOnSide(before, centre, half, neighbours, axis, false);

            float grownFromCentre = delta * 0.5f;
            bool blockedPlus = freePlus < grownFromCentre - Tolerance.EpsilonUnits;
            bool blockedMinus = freeMinus < grownFromCentre - Tolerance.EpsilonUnits;
            if (blockedPlus == blockedMinus) return -1;

            float freeOnTheOpenSide = blockedPlus ? freeMinus : freePlus;
            if (freeOnTheOpenSide < delta - Tolerance.EpsilonUnits) return -1;
            return blockedPlus ? axis * 2 + 1 : axis * 2;
        }

        private static int ContactSideForShrink(in ElementGeometry before,
            IReadOnlyList<ElementGeometry> neighbours, int axis)
        {
            Describe(before, out var centre, out var half);
            var plus = RankOnSide(before, centre, half, neighbours, axis, true);
            var minus = RankOnSide(before, centre, half, neighbours, axis, false);
            if (plus == minus) return -1;
            return plus > minus ? axis * 2 : axis * 2 + 1;
        }

        private static float FreeUnitsOnSide(in ElementGeometry before, Vector3 centre, Vector3 half,
            IReadOnlyList<ElementGeometry> neighbours, int axis, bool plusSide)
        {
            float free = float.MaxValue;
            for (int i = 0; i < neighbours.Count; i++)
            {
                var other = neighbours[i];
                if (!Counts(before, other)) continue;
                var probe = Probe(before, centre, half, other, axis);
                if (!probe.Valid || probe.OnPlusSide != plusSide) continue;
                if (probe.LateralA <= ContactUnits || probe.LateralB <= ContactUnits) continue;
                float gap = Mathf.Max(probe.Gap, 0f);
                if (gap < free) free = gap;
            }
            return free;
        }

        private static ResizeContactRank RankOnSide(in ElementGeometry box, Vector3 centre, Vector3 half,
            IReadOnlyList<ElementGeometry> neighbours, int axis, bool plusSide)
        {
            var best = ResizeContactRank.None;
            for (int i = 0; i < neighbours.Count; i++)
            {
                var other = neighbours[i];
                if (!Counts(box, other)) continue;
                var probe = Probe(box, centre, half, other, axis);
                if (!probe.Valid || probe.OnPlusSide != plusSide) continue;
                if (Mathf.Abs(probe.Gap) > ContactUnits) continue;
                var rank = RankOf(probe);
                if (rank > best) best = rank;
            }
            return best;
        }

        private static ResizeContactRank RankOf(in SideProbe probe)
        {
            if (probe.LateralA < -ContactUnits || probe.LateralB < -ContactUnits)
                return ResizeContactRank.None;
            int spread = (probe.LateralA > ContactUnits ? 1 : 0) + (probe.LateralB > ContactUnits ? 1 : 0);
            return spread == 2 ? ResizeContactRank.Plane
                : spread == 1 ? ResizeContactRank.Edge
                : ResizeContactRank.Vertex;
        }

        private static SideProbe Probe(in ElementGeometry self, Vector3 centre, Vector3 half,
            in ElementGeometry other, int axis)
        {
            if (!IsBox(other)) return default;

            Describe(other, out var otherCentre, out var otherHalf);
            var offset = otherCentre - centre;

            float alongAxis = Vector3.Dot(offset, self.Faces[axis * 2].normal);
            float reach = RadiusAlong(other, otherHalf, self.Faces[axis * 2].normal);
            float gap = Mathf.Abs(alongAxis) - reach - Axis(half, axis);

            return new SideProbe(alongAxis >= 0f, gap,
                LateralOverlap(self, offset, half, other, otherHalf, (axis + 1) % 3),
                LateralOverlap(self, offset, half, other, otherHalf, (axis + 2) % 3));
        }

        private static float LateralOverlap(in ElementGeometry self, Vector3 offset, Vector3 half,
            in ElementGeometry other, Vector3 otherHalf, int axis)
        {
            var normal = self.Faces[axis * 2].normal;
            return Axis(half, axis) + RadiusAlong(other, otherHalf, normal)
                - Mathf.Abs(Vector3.Dot(offset, normal));
        }

        private static bool OverlapsAnyNeighbour(in ElementGeometry before,
            IReadOnlyList<ElementGeometry> neighbours)
        {
            for (int i = 0; i < neighbours.Count; i++)
            {
                var other = neighbours[i];
                if (!Counts(before, other)) continue;
                if (FaceContacts.AABBsIntersect(before, other, ContactUnits)) return true;
            }
            return false;
        }

        private static float DeltaOn(in ElementGeometry before, in ElementGeometry after, int axis)
        {
            Describe(before, out _, out var halfBefore);
            Describe(after, out _, out var halfAfter);
            return (Axis(halfAfter, axis) - Axis(halfBefore, axis)) * 2f;
        }

        private static void Describe(in ElementGeometry box, out Vector3 centre, out Vector3 half)
        {
            centre = (box.Faces[0].center + box.Faces[1].center) * 0.5f;
            half = new Vector3(
                Mathf.Abs(Vector3.Dot(box.Faces[0].center - centre, box.Faces[0].normal)),
                Mathf.Abs(Vector3.Dot(box.Faces[2].center - centre, box.Faces[2].normal)),
                Mathf.Abs(Vector3.Dot(box.Faces[4].center - centre, box.Faces[4].normal)));
        }

        private static float RadiusAlong(in ElementGeometry box, Vector3 half, Vector3 axis) =>
            half.x * Mathf.Abs(Vector3.Dot(axis, box.Faces[0].normal))
            + half.y * Mathf.Abs(Vector3.Dot(axis, box.Faces[2].normal))
            + half.z * Mathf.Abs(Vector3.Dot(axis, box.Faces[4].normal));

        private static Vector3 LateralFrom(Vector3 half, Vector3 lateral, int axis) => new Vector3(
            axis == 0 ? half.x : lateral.x,
            axis == 1 ? half.y : lateral.y,
            axis == 2 ? half.z : lateral.z);

        private static float Axis(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        private static bool IsBox(in ElementGeometry box) =>
            !box.IsEmpty && box.Faces.Length >= Face.BoxFaceCount;

        private static bool Counts(in ElementGeometry self, in ElementGeometry other) =>
            !other.IsEmpty && !ReferenceEquals(other.Faces, self.Faces) && other.Id != self.Id;

        private readonly struct SideProbe
        {
            public readonly bool Valid;
            public readonly bool OnPlusSide;
            public readonly float Gap;
            public readonly float LateralA;
            public readonly float LateralB;

            public SideProbe(bool onPlusSide, float gap, float lateralA, float lateralB)
            {
                Valid = true;
                OnPlusSide = onPlusSide;
                Gap = gap;
                LateralA = lateralA;
                LateralB = lateralB;
            }
        }
    }
}
