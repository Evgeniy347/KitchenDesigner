using System;

namespace KitchenDesigner.Core.Construction
{
    public static class FrostDepth
    {
        public const string FormulaSource = "СП 22.13330.2016, 5.5.3, формула (5.3)";
        public const string ThermalCalculationSource = "СП 25.13330";

        public const float ClayFactorMm = 230f;
        public const float SandyLoamFactorMm = 280f;
        public const float CoarseSandFactorMm = 300f;
        public const float ThermalCalculationAboveMm = 2500f;

        public static bool TrySoilFactorMm(SoilKind soil, out float factorMm)
        {
            switch (soil)
            {
                case SoilKind.Loam:
                case SoilKind.Clay:
                    factorMm = ClayFactorMm;
                    return true;
                case SoilKind.SandyLoam:
                    factorMm = SandyLoamFactorMm;
                    return true;
                case SoilKind.Sand:
                case SoilKind.Unknown:
                    factorMm = CoarseSandFactorMm;
                    return true;
                default:
                    factorMm = 0f;
                    return false;
            }
        }

        public static bool TryNormativeMm(ConstructionRegion region, SoilKind soil, out float depthMm)
        {
            depthMm = 0f;
            if (!TrySoilFactorMm(soil, out float factorMm)) return false;
            if (!RegionClimate.TryOf(region, out var climate)) return false;

            double depth = factorMm * Math.Sqrt(climate.NegativeMonthSumDeg);
            if (depth > ThermalCalculationAboveMm) return false;

            depthMm = (float)depth;
            return true;
        }
    }
}
