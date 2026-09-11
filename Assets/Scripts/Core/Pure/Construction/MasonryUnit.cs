using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct MasonryUnit
    {
        public const string BrickStandard = "ГОСТ 530-2012";
        public const string AeratedBlockStandard = "ГОСТ 31360-2024";
        public const string TimberStandard = "ГОСТ 8486-86";
        public const string FrameStandard = "шаг утеплителя 600 мм";

        public readonly MasonryTechnology Technology;
        public readonly string Title;
        public readonly MasonryCounting Counting;
        public readonly float LengthMm;
        public readonly float HeightMm;
        public readonly float WidthMm;
        public readonly string Source;

        public MasonryUnit(MasonryTechnology technology, string title, MasonryCounting counting,
            float lengthMm, float heightMm, float widthMm, string source)
        {
            Technology = technology;
            Title = title;
            Counting = counting;
            LengthMm = lengthMm;
            HeightMm = heightMm;
            WidthMm = widthMm;
            Source = source;
        }

        public bool HasFormat => Counting == MasonryCounting.Pieces;

        public double BareVolumeM3 =>
            LengthMm * 0.001d * (HeightMm * 0.001d) * (WidthMm * 0.001d);

        public double JointedVolumeM3(double jointMm) =>
            (LengthMm + jointMm) * 0.001d
            * ((HeightMm + jointMm) * 0.001d)
            * ((WidthMm + jointMm) * 0.001d);

        public static readonly IReadOnlyList<MasonryUnit> Table = new[]
        {
            new MasonryUnit(MasonryTechnology.BrickSingle, "Кирпич 250×120×65",
                MasonryCounting.Pieces, 250f, 65f, 120f, BrickStandard),
            new MasonryUnit(MasonryTechnology.BrickThickened, "Кирпич 250×120×88",
                MasonryCounting.Pieces, 250f, 88f, 120f, BrickStandard),
            new MasonryUnit(MasonryTechnology.AeratedBlock, "Газоблок 600×300×200",
                MasonryCounting.Pieces, 600f, 200f, 300f, AeratedBlockStandard),
            new MasonryUnit(MasonryTechnology.Timber, "Брус",
                MasonryCounting.Volume, 0f, 0f, 0f, TimberStandard),
            new MasonryUnit(MasonryTechnology.Frame, "Каркас",
                MasonryCounting.Studs, 0f, 0f, 0f, FrameStandard),
        };

        public static MasonryUnit Of(MasonryTechnology technology)
        {
            foreach (var unit in Table)
                if (unit.Technology == technology) return unit;
            throw new ArgumentOutOfRangeException(nameof(technology),
                technology, "формат кладки отсутствует в MasonryUnit.Table");
        }

        public static IReadOnlyList<float> ThicknessSeries(MasonryTechnology technology,
            float jointMm)
        {
            var unit = Of(technology);
            if (!unit.HasFormat) return Array.Empty<float>();

            var series = new float[SeriesRows];
            for (int rows = 1; rows <= SeriesRows; rows++)
                series[rows - 1] = rows * unit.WidthMm + (rows - 1) * jointMm;
            return series;
        }

        public const int SeriesRows = 4;
    }
}
