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
                $"Глубина заложения ленты {Mm(actualDepthMm)} мм меньше глубины промерзания "
                + $"{Mm(frostDepthMm)} мм на пучинистом грунте «{SoilKindTitles.Of(soil)}»: "
                + "силы морозного пучения зимой выдавят фундамент");

        public static ConstructionFinding SoleTooNarrow(string elementId, SoilKind soil,
            float wallThicknessMm, float minWidthMm, float actualWidthMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeSoleTooNarrow, elementId,
                $"Ширина подошвы ленты {Mm(actualWidthMm)} мм меньше минимума {Mm(minWidthMm)} "
                + $"мм для стены {Mm(wallThicknessMm)} мм на грунте «{SoilKindTitles.Of(soil)}»: "
                + "несущая способность подошвы недостаточна");

        public static ConstructionFinding CushionTooThin(string elementId, float sandMm,
            float gravelMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeCushionTooThin, elementId,
                $"Подушка тоньше {Mm(FoundationRules.MinCushionLayerMm)} мм: песок {Mm(sandMm)} "
                + $"мм, щебень {Mm(gravelMm)} мм — тоньше минимума она не работает как "
                + "выравнивающий и дренирующий слой");

        public static ConstructionFinding RebarCoverTooThin(string elementId, float coverMm,
            float diameterMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeRebarProtection, elementId,
                $"Защитный слой арматуры {Mm(coverMm)} мм меньше 2×Ø ({Mm(2f * diameterMm)} мм "
                + $"для Ø{Mm(diameterMm)}): арматура может оголиться");

        public static ConstructionFinding RebarStepTooWide(string elementId, float stepMm,
            float widthMm) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeRebarProtection, elementId,
                $"Шаг арматуры {Mm(stepMm)} мм больше ширины ленты {Mm(widthMm)} мм: каркас "
                + "не держит форму на такой ширине");

        public static ConstructionFinding WallNotCovered(string elementId) =>
            new ConstructionFinding(ConstructionFindingLevel.Error, CodeWallNotCovered, elementId,
                "Несущая стена не покрыта лентой: осевая стены не входит ни в одну полилинию "
                + "фундамента");

        private static string Mm(float value) =>
            value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
