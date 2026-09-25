using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core.Analysis
{
    public static class SceneSlabSnapshot
    {
        private readonly struct LoadBearingWallTop
        {
            public readonly float MinXMm, MaxXMm, MinZMm, MaxZMm, TopYMm;

            public LoadBearingWallTop(float minXMm, float maxXMm, float minZMm, float maxZMm,
                float topYMm)
            {
                MinXMm = minXMm;
                MaxXMm = maxXMm;
                MinZMm = minZMm;
                MaxZMm = maxZMm;
                TopYMm = topYMm;
            }

            public bool OverlapsXZ(float minXMm, float maxXMm, float minZMm, float maxZMm) =>
                MaxXMm > minXMm && MinXMm < maxXMm && MaxZMm > minZMm && MinZMm < maxZMm;
        }

        public static List<FloorSlabSurvey> Slabs(IReadOnlyList<KitchenElement> all)
        {
            var result = new List<FloorSlabSurvey>();
            if (all == null) return result;

            var walls = LoadBearingWallTops(all);
            foreach (var e in all)
            {
                if (!(e is FloorSlabElement slab)) continue;
                if (TryWorstGapToASupportingWallMm(slab, walls, out float gapMm))
                    result.Add(new FloorSlabSurvey(slab.PartName, gapMm));
            }

            return result;
        }

        private static List<LoadBearingWallTop> LoadBearingWallTops(IReadOnlyList<KitchenElement> all)
        {
            var tops = new List<LoadBearingWallTop>();
            foreach (var e in all)
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null || !wall.LoadBearing) continue;

                var centreline = WallCentreline.Of(wall.FullPosition, e.transform.rotation, e.DimensionsMM);
                if (!centreline.IsDefined) continue;

                float thicknessMm = WallCentreline.ThicknessMM(e.DimensionsMM);
                float halfThicknessUnits = thicknessMm * 0.5f * AppConstants.MM_TO_UNITS;
                float minXUnits = Mathf.Min(centreline.Start.x, centreline.End.x) - halfThicknessUnits;
                float maxXUnits = Mathf.Max(centreline.Start.x, centreline.End.x) + halfThicknessUnits;
                float minZUnits = Mathf.Min(centreline.Start.z, centreline.End.z) - halfThicknessUnits;
                float maxZUnits = Mathf.Max(centreline.Start.z, centreline.End.z) + halfThicknessUnits;

                float topYMm = wall.FullPosition.y / AppConstants.MM_TO_UNITS + e.DimensionsMM.y * 0.5f;
                tops.Add(new LoadBearingWallTop(minXUnits / AppConstants.MM_TO_UNITS,
                    maxXUnits / AppConstants.MM_TO_UNITS, minZUnits / AppConstants.MM_TO_UNITS,
                    maxZUnits / AppConstants.MM_TO_UNITS, topYMm));
            }

            return tops;
        }

        private static bool TryWorstGapToASupportingWallMm(FloorSlabElement slab,
            List<LoadBearingWallTop> walls, out float gapMm)
        {
            gapMm = 0f;

            var pos = slab.transform.position;
            float halfXMm = slab.DimensionsMM.x * 0.5f;
            float halfZMm = slab.DimensionsMM.z * 0.5f;
            float centreXMm = pos.x / AppConstants.MM_TO_UNITS;
            float centreZMm = pos.z / AppConstants.MM_TO_UNITS;
            float minXMm = centreXMm - halfXMm;
            float maxXMm = centreXMm + halfXMm;
            float minZMm = centreZMm - halfZMm;
            float maxZMm = centreZMm + halfZMm;
            float slabBottomYMm = pos.y / AppConstants.MM_TO_UNITS - slab.ThicknessMm * 0.5f;

            bool found = false;
            float worst = float.NegativeInfinity;
            foreach (var wall in walls)
            {
                if (!wall.OverlapsXZ(minXMm, maxXMm, minZMm, maxZMm)) continue;
                found = true;
                float gap = slabBottomYMm - wall.TopYMm;
                if (gap > worst) worst = gap;
            }

            if (!found) return false;
            gapMm = worst;
            return true;
        }
    }
}
