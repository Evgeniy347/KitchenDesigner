using System;
using System.Globalization;

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

        public const string UnknownValue = "—";
        public const string BeyondFormulaValue = "> 2500 мм";

        public const string NoSoilFactorReason = "Прочерк: СП 22.13330 не даёт d0 для торфа.";
        public const string BeyondFormulaReason =
            "Глубже 2500 мм: требуется теплотехнический расчёт по СП 25.13330.";
        public const string UnknownRegionReason =
            "Прочерк: климата для этого региона в таблице СП 131.13330.2020 нет.";

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

        public static FrostDepthReading Read(ConstructionRegion region, SoilKind soil)
        {
            if (!RegionClimate.TryOf(region, out var climate))
                return new FrostDepthReading(0f, UnknownValue, UnknownRegionReason, string.Empty);

            string station = climate.ReferenceStation;
            if (!TrySoilFactorMm(soil, out float factorMm))
                return new FrostDepthReading(0f, UnknownValue, NoSoilFactorReason, station);

            double depth = factorMm * Math.Sqrt(climate.NegativeMonthSumDeg);
            if (depth > ThermalCalculationAboveMm)
                return new FrostDepthReading(0f, BeyondFormulaValue, BeyondFormulaReason, station);

            return new FrostDepthReading((float)depth, Millimetres(depth), string.Empty, station);
        }

        public static bool TryNormativeMm(ConstructionRegion region, SoilKind soil, out float depthMm)
        {
            var reading = Read(region, soil);
            depthMm = reading.DepthMm;
            return reading.HasNumber;
        }

        private static string Millimetres(double depth) =>
            ((int)Math.Round(depth, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture) + " мм";
    }
}
