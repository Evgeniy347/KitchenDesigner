using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.Construction
{
    public static class ConstructionIssueCatalog
    {
        public const string CodeWallThicknessOffFormat = "WAL-01";

        public static ConstructionFinding WallThicknessOffFormat(string elementId,
            MasonryUnit unit, float thicknessMm, IReadOnlyList<float> series) =>
            new ConstructionFinding(ConstructionFindingLevel.Warning, CodeWallThicknessOffFormat,
                elementId,
                $"Толщина стены {Mm(thicknessMm)} мм не кратна формату «{unit.Title}» со швом: "
                + $"стандартные толщины {Series(series)} мм — кирпич придётся резать");

        private static string Mm(float value) =>
            value.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Series(IReadOnlyList<float> series)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < series.Count; i++)
            {
                if (i > 0) text.Append(" / ");
                text.Append(Mm(series[i]));
            }
            return text.ToString();
        }
    }
}
