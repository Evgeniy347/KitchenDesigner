using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PillarAutoFit
    {
        public const float OverlapMarginUnits = 0.05f;
        public const int MinGapAboveMM = 80;
        public const int MaxGapAboveMM = 130;
        public const float NoFloorFound = -1000f;
        private const float FloorSearchDepthUnits = 1f;
        private const float BelowCentreUnits = 0.01f;

        public static void Seat(PillarElement pillar, IReadOnlyList<KitchenElement> scene)
        {
            if (pillar == null || scene == null) return;

            float floorY = FloorUnder(pillar.transform.position, scene);
            if (floorY <= NoFloorFound + 1f) return;

            StandOn(pillar, floorY);

            var above = NearestBoardAbove(pillar, floorY, scene);
            if (above == null) return;

            int gapMM = GapMM(above.Value - floorY);
            pillar.MidHeightMM = MidHeightForGapMM(gapMM);
            StandOn(pillar, floorY);
        }

        public static float FloorUnder(Vector3 pillarCenter, IReadOnlyList<KitchenElement> scene)
        {
            float bestY = float.MinValue;
            foreach (var el in scene)
            {
                if (el == null) continue;
                var aabb = ElementAabb.Of(el);
                if (aabb.maxY > pillarCenter.y - BelowCentreUnits) continue;
                if (OverlapsInXZ(pillarCenter, aabb, OverlapMarginUnits) && aabb.maxY > bestY)
                    bestY = aabb.maxY;
            }
            return bestY >= pillarCenter.y - FloorSearchDepthUnits ? bestY : NoFloorFound;
        }

        public static int GapMM(float gapUnits) =>
            Mathf.FloorToInt(gapUnits / AppConstants.MM_TO_UNITS + Tolerance.ClearanceMm);

        public static int MidHeightForGapMM(int gapMM) => Mathf.Clamp(
            gapMM - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
            PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);

        private static void StandOn(PillarElement pillar, float floorY)
        {
            var p = pillar.transform.position;
            pillar.transform.position = new Vector3(p.x,
                floorY + AppConstants.HalfHeightUnits(pillar.TotalHeightMM), p.z);
        }

        private static float? NearestBoardAbove(PillarElement pillar, float floorY,
            IReadOnlyList<KitchenElement> scene)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float minAbove = floorY + MinGapAboveMM * toU;
            float maxAbove = floorY + MaxGapAboveMM * toU;
            var center = pillar.transform.position;

            float best = float.MaxValue;
            bool found = false;
            foreach (var el in scene)
            {
                if (el == null || el == (KitchenElement)pillar) continue;
                var aabb = ElementAabb.Of(el);
                if (aabb.minY < minAbove || aabb.minY > maxAbove) continue;
                if (!OverlapsInXZ(center, aabb, OverlapMarginUnits)) continue;
                if (aabb.minY >= best) continue;
                best = aabb.minY;
                found = true;
            }
            return found ? best : (float?)null;
        }

        private static bool OverlapsInXZ(Vector3 point, ElementAabb aabb, float margin) =>
            aabb.CoversInXZ(point, margin);
    }
}
