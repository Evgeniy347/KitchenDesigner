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
    }
}
