using System;
using System.Globalization;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public static class PipeElementSpec
    {
        public const int DEFAULT_LENGTH_MM = 600;
        public const int MIN_LENGTH_MM = 10;
        public const int MAX_LENGTH_MM = 6000;

        public static int ClampLengthMM(int lengthMM) =>
            lengthMM < MIN_LENGTH_MM ? MIN_LENGTH_MM
            : lengthMM > MAX_LENGTH_MM ? MAX_LENGTH_MM
            : lengthMM;

        public static int SectionMM(string? sizeId) =>
            (int)Math.Round(PipeSpec.Get(sizeId).OuterDiameterMm, MidpointRounding.AwayFromZero);

        public static string OuterDiameterText(string? sizeId) =>
            Text(PipeSpec.Get(sizeId).OuterDiameterMm);

        public static string InnerDiameterText(string? sizeId) =>
            Text(PipeSpec.Get(sizeId).InnerDiameterMm);

        public static string WallThicknessText(string? sizeId) =>
            Text(PipeSpec.Get(sizeId).WallThicknessMm);

        public static string[] Designations()
        {
            var table = PipeSpec.Table;
            var labels = new string[table.Length];
            for (int i = 0; i < table.Length; i++)
                labels[i] = table[i].NominalBoreMm.ToString(CultureInfo.InvariantCulture)
                    + " (" + table[i].Designation + ")";
            return labels;
        }

        public static int IndexOf(string? sizeId)
        {
            var normalized = PipeSpec.NormalizeSize(sizeId);
            var table = PipeSpec.Table;
            for (int i = 0; i < table.Length; i++)
                if (string.Equals(table[i].Id, normalized, StringComparison.Ordinal)) return i;
            return 0;
        }

        public static string SizeIdAt(int index)
        {
            var table = PipeSpec.Table;
            return index >= 0 && index < table.Length ? table[index].Id : PipeSpec.DEFAULT_SIZE;
        }

        private static string Text(float valueMm) =>
            valueMm.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
