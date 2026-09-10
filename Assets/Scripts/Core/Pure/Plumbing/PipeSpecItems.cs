using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeSpecItems
    {
        public static float LengthMeters(int lengthMM) => lengthMM * 0.001f;

        public static string PipeLineName(PipeSize size) => $"Труба ДН{size.NominalBoreMm}";

        public static SpecItem PipeLine(PipeSize size, int lengthMM) =>
            new SpecItem(SpecSections.Plumbing, PipeLineName(size), "",
                SpecUnit.LinearMeters, LengthMeters(lengthMM));

        public static string FittingLineName(string title, IReadOnlyList<string?> boreSizeIds)
        {
            var parts = new string[boreSizeIds.Count];
            for (int i = 0; i < boreSizeIds.Count; i++)
                parts[i] = PipeSpec.NominalOrDash(boreSizeIds[i]);
            return $"{title} ДН{string.Join("×", parts)}";
        }

        public static SpecItem FittingLine(string title, IReadOnlyList<string?> boreSizeIds) =>
            new SpecItem(SpecSections.Plumbing, FittingLineName(title, boreSizeIds), "",
                SpecUnit.Pieces, 1f);
    }
}
