using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationRules
    {
        public const float MinCushionLayerMm = 100f;

        public static bool DepthMeetsFrostRule(SoilKind soil, bool frostDepthKnown,
            float frostDepthMm, float actualDepthMm)
        {
            if (soil == SoilKind.Sand) return true;
            if (!frostDepthKnown) return true;
            return actualDepthMm >= frostDepthMm;
        }

        public static bool SoleWidthMeetsMinimum(SoilKind soil, float wallThicknessMm,
            float actualWidthMm)
        {
            if (!FoundationSoleWidth.TryMinimumWidthMm(soil, wallThicknessMm, out float minWidthMm))
                return true;
            return actualWidthMm >= minWidthMm;
        }

        public static bool CushionMeetsMinimum(bool compacted, float sandMm, float gravelMm)
        {
            if (!compacted) return true;
            return sandMm >= MinCushionLayerMm && gravelMm >= MinCushionLayerMm;
        }

        public static bool RebarCoverOk(float coverMm, float diameterMm) =>
            coverMm >= 2f * diameterMm;

        public static bool RebarStepOk(float stepMm, float widthMm) =>
            stepMm <= widthMm;

        public static bool CentrelineCoveredByFoundation(WallCentreline wall,
            IReadOnlyList<FoundationPolyline> polylines) =>
            FoundationCoverage.IsSegmentCovered(wall, polylines);

        public static IReadOnlyList<ConstructionFinding> Collect(
            IReadOnlyList<FoundationSurvey>? foundations,
            IReadOnlyList<FoundationWallSpan>? loadBearingWalls)
        {
            var findings = new List<ConstructionFinding>();
            if (foundations == null) return findings;

            foreach (var f in foundations)
            {
                if (!DepthMeetsFrostRule(f.Soil, f.FrostDepthKnown, f.FrostDepthMm, f.DepthMm))
                    findings.Add(FoundationFindings.DepthBelowFrost(f.ElementId, f.Soil,
                        f.FrostDepthMm, f.DepthMm));

                if (!CushionMeetsMinimum(f.Compacted, f.SandMm, f.GravelMm))
                    findings.Add(FoundationFindings.CushionTooThin(f.ElementId, f.SandMm, f.GravelMm));

                if (!RebarCoverOk(f.CoverMm, f.RebarDiameterMm))
                    findings.Add(FoundationFindings.RebarCoverTooThin(f.ElementId, f.CoverMm,
                        f.RebarDiameterMm));

                if (!RebarStepOk(f.RebarStepMm, f.WidthMm))
                    findings.Add(FoundationFindings.RebarStepTooWide(f.ElementId, f.RebarStepMm,
                        f.WidthMm));
            }

            if (loadBearingWalls == null || foundations.Count == 0) return findings;

            foreach (var wall in loadBearingWalls)
            {
                FoundationSurvey? covering = null;
                foreach (var f in foundations)
                {
                    if (!CentrelineCoveredByFoundation(wall.Centreline, f.Polylines)) continue;
                    covering = f;
                    break;
                }

                if (covering == null)
                {
                    findings.Add(FoundationFindings.WallNotCovered(wall.ElementId));
                    continue;
                }

                var foundation = covering.Value;
                if (FoundationSoleWidth.TryMinimumWidthMm(foundation.Soil, wall.ThicknessMm,
                        out float minWidthMm)
                    && !SoleWidthMeetsMinimum(foundation.Soil, wall.ThicknessMm, foundation.WidthMm))
                    findings.Add(FoundationFindings.SoleTooNarrow(wall.ElementId, foundation.Soil,
                        wall.ThicknessMm, minWidthMm, foundation.WidthMm));
            }

            return findings;
        }
    }
}
