using System.Globalization;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationFindings
    {
        public const string CodeDepthBelowFrost = "FND-01";
        public const string CodeSoleTooNarrow = "FND-02";
        public const string CodeCushionTooThin = "FND-03";
        public const string CodeRebarProtection = "FND-04";
        public const string CodeWallNotCovered = "FND-05";

        public static ConstructionFinding DepthBelowFrost(string elementId, SoilKind soil,
            float frostDepthMm, float actualDepthMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeDepthBelowFrost, elementId,
                Loc.F("issue.fnd01.message", Mm(actualDepthMm), Mm(frostDepthMm), SoilKindTitles.Of(soil)));

        public static ConstructionFinding SoleTooNarrow(string elementId, SoilKind soil,
            float wallThicknessMm, float minWidthMm, float actualWidthMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeSoleTooNarrow, elementId,
                Loc.F("issue.fnd02.message", Mm(actualWidthMm), Mm(minWidthMm), Mm(wallThicknessMm), SoilKindTitles.Of(soil)));

        public static ConstructionFinding CushionTooThin(string elementId, float sandMm,
            float gravelMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeCushionTooThin, elementId,
                Loc.F("issue.fnd03.message", Mm(FoundationRules.MinCushionLayerMm), Mm(sandMm), Mm(gravelMm)));

        public static ConstructionFinding RebarCoverTooThin(string elementId, float coverMm,
            float diameterMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeRebarProtection, elementId,
                Loc.F("issue.fnd04.coverThin", Mm(coverMm), Mm(2f * diameterMm), Mm(diameterMm)));

        public static ConstructionFinding RebarStepTooWide(string elementId, float stepMm,
            float widthMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeRebarProtection, elementId,
                Loc.F("issue.fnd04.stepWide", Mm(stepMm), Mm(widthMm)));

        public static ConstructionFinding WallNotCovered(string elementId) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeWallNotCovered, elementId,
                Loc.T("issue.fnd05.message"));

        private static string Mm(float value) =>
            value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
