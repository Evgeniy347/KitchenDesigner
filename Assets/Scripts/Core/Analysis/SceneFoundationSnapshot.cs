using System.Collections.Generic;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.Analysis
{
    public static class SceneFoundationSnapshot
    {
        public static List<FoundationSurvey> Foundations(IReadOnlyList<KitchenElement> all)
        {
            var result = new List<FoundationSurvey>();
            if (all == null) return result;

            var region = KitchenSettings.Instance.ConstructionRegion;
            foreach (var e in all)
            {
                if (!(e is FoundationElement f)) continue;

                bool frostDepthKnown = FrostDepth.TryNormativeMm(region, f.SoilKind, out float frostDepthMm);
                result.Add(new FoundationSurvey(f.PartName, f.SoilKind, f.DimensionsMM.x,
                    f.DimensionsMM.y, f.SandMm, f.GravelMm, f.Compacted, f.RebarDiameterMm,
                    f.RebarStepMm, f.CoverMm, frostDepthKnown, frostDepthMm, f.BuiltPolylines));
            }

            return result;
        }

        public static List<FoundationWallSpan> LoadBearingWalls(IReadOnlyList<KitchenElement> all)
        {
            var result = new List<FoundationWallSpan>();
            if (all == null) return result;

            foreach (var e in all)
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null || !wall.LoadBearing) continue;

                var centreline = WallCentreline.Of(wall.FullPosition, e.transform.rotation, e.DimensionsMM);
                if (!centreline.IsDefined) continue;

                float thicknessMm = WallCentreline.ThicknessMM(e.DimensionsMM);
                result.Add(new FoundationWallSpan(e.PartName, centreline, thicknessMm));
            }

            return result;
        }
    }
}
