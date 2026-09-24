using System;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationSoleWidth
    {
        public const string Source =
            "СП 22.13330.2016, минимальный конструктивный напуск подошвы ленты за грань стены "
            + "(пункт не подтверждён исполнителем — см. NormativeUnverified)";

        public const float SandMarginMm = 100f;
        public const float SandyLoamMarginMm = 150f;
        public const float LoamMarginMm = 200f;
        public const float ClayMarginMm = 200f;
        public const float UnknownMarginMm = 200f;

        public static bool TryMarginMm(SoilKind soil, out float marginMm)
        {
            switch (soil)
            {
                case SoilKind.Sand:
                    marginMm = SandMarginMm;
                    return true;
                case SoilKind.SandyLoam:
                    marginMm = SandyLoamMarginMm;
                    return true;
                case SoilKind.Loam:
                    marginMm = LoamMarginMm;
                    return true;
                case SoilKind.Clay:
                    marginMm = ClayMarginMm;
                    return true;
                case SoilKind.Unknown:
                    marginMm = UnknownMarginMm;
                    return true;
                default:
                    marginMm = 0f;
                    return false;
            }
        }

        public static bool TryMinimumWidthMm(SoilKind soil, float wallThicknessMm, out float minWidthMm)
        {
            if (!TryMarginMm(soil, out float marginMm))
            {
                minWidthMm = 0f;
                return false;
            }

            minWidthMm = Math.Max(0f, wallThicknessMm) + 2f * marginMm;
            return true;
        }
    }
}
