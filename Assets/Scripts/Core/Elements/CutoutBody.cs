using UnityEngine;

namespace KitchenDesigner.Core
{
    internal readonly struct CutoutBody
    {
        public readonly PartPlane Plane;
        public readonly int OffsetXMM;
        public readonly int OffsetYMM;
        public readonly int WidthMM;
        public readonly int DepthMM;
        public readonly int HeightMM;

        public CutoutBody(PartPlane plane, int offsetXMM, int offsetYMM,
            int widthMM, int depthMM, int heightMM)
        {
            Plane = plane;
            OffsetXMM = offsetXMM;
            OffsetYMM = offsetYMM;
            WidthMM = widthMM;
            DepthMM = depthMM;
            HeightMM = heightMM;
        }

        private (float x0, float x1, float y0, float y1, float z0, float z1) Extents
        {
            get
            {
                float toU = AppConstants.MM_TO_UNITS;
                float x0 = (OffsetXMM - WidthMM * 0.5f) * toU, x1 = (OffsetXMM + WidthMM * 0.5f) * toU;
                float y0 = (OffsetYMM - DepthMM * 0.5f) * toU, y1 = (OffsetYMM + DepthMM * 0.5f) * toU;
                float halfT = Plane.ThicknessMM * 0.5f * toU;
                float deep = HeightMM * toU;
                float z0 = Plane.UpSign > 0f ? halfT - deep : -halfT;
                float z1 = Plane.UpSign > 0f ? halfT : -halfT + deep;
                return (x0, x1, y0, y1, z0, z1);
            }
        }

        public bool Overlaps(KitchenElement other)
        {
            if (!Plane.BoundsOf(other, out var min, out var max)) return false;

            var box = Extents;
            float eps = Tolerance.EpsilonUnits;
            if (max[Plane.AxisA] <= box.x0 + eps || min[Plane.AxisA] >= box.x1 - eps) return false;
            if (max[Plane.AxisB] <= box.y0 + eps || min[Plane.AxisB] >= box.y1 - eps) return false;
            if (max[Plane.UpAxis] <= box.z0 + eps || min[Plane.UpAxis] >= box.z1 - eps) return false;
            return true;
        }

        public string? FirstBlocker(KitchenElement self)
        {
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == self || el == Plane.Part || !el.BlocksCutout) continue;
                if (Overlaps(el)) return el.PartName;
            }
            return null;
        }

        public (int offXMM, int offYMM) SnappedToNeighbours(KitchenElement self, int snapPlaneMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var box = Extents;

            int bestXOff = OffsetXMM, bestYOff = OffsetYMM;
            float bestXDist = snapPlaneMM + 1f, bestYDist = snapPlaneMM + 1f;

            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == self || el == Plane.Part || !el.AlignsCutout) continue;
                if (!Plane.BoundsOf(el, out var min, out var max)) continue;
                if (max[Plane.UpAxis] <= box.z0 || min[Plane.UpAxis] >= box.z1) continue;

                TrySnapAxis(min[Plane.AxisA], max[Plane.AxisA], WidthMM, OffsetXMM,
                    min[Plane.AxisB], max[Plane.AxisB], box.y0, box.y1, toU, ref bestXOff, ref bestXDist);
                TrySnapAxis(min[Plane.AxisB], max[Plane.AxisB], DepthMM, OffsetYMM,
                    min[Plane.AxisA], max[Plane.AxisA], box.x0, box.x1, toU, ref bestYOff, ref bestYDist);
            }

            return (bestXDist <= snapPlaneMM ? bestXOff : OffsetXMM,
                    bestYDist <= snapPlaneMM ? bestYOff : OffsetYMM);
        }

        private static void TrySnapAxis(float nearMin, float nearMax, int cutoutMM, int currentOff,
            float crossMin, float crossMax, float crossLo, float crossHi, float toU,
            ref int bestOff, ref float bestDist)
        {
            if (crossMax <= crossLo || crossMin >= crossHi) return;

            float half = cutoutMM * 0.5f;
            Consider(nearMax / toU + half, currentOff, ref bestOff, ref bestDist);
            Consider(nearMin / toU - half, currentOff, ref bestOff, ref bestDist);
        }

        private static void Consider(float candidateOff, int currentOff,
            ref int bestOff, ref float bestDist)
        {
            int rounded = Mathf.RoundToInt(candidateOff);
            float dist = Mathf.Abs(rounded - currentOff);
            if (dist >= bestDist) return;
            bestDist = dist;
            bestOff = rounded;
        }
    }
}
