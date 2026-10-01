using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.Construction
{
    public static class FloorSlabRules
    {
        public const string CodeGapToSupportingWall = "FLR-01";

        public static bool RestsOnTheWall(float gapToWallMm) => gapToWallMm <= 0f;

        public static ConstructionFinding GapToSupportingWall(string elementId, float gapToWallMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeGapToSupportingWall,
                elementId,
                Loc.F("issue.flr01.message", elementId, Mm(gapToWallMm)));

        public static IReadOnlyList<ConstructionFinding> Collect(IReadOnlyList<FloorSlabSurvey>? slabs)
        {
            var findings = new List<ConstructionFinding>();
            if (slabs == null) return findings;

            foreach (var slab in slabs)
            {
                if (RestsOnTheWall(slab.GapToWallMm)) continue;
                findings.Add(GapToSupportingWall(slab.ElementId, slab.GapToWallMm));
            }

            return findings;
        }

        private static string Mm(float value) =>
            value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
