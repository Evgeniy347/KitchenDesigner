using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationQuantities
    {
        public const float SteelDensityKgPerM3 = 7850f;
        public const int MinimalCageBarCount = 4;

        public static double RebarMassPerMetreKg(float diameterMm)
        {
            double radiusM = Math.Max(0f, diameterMm) * 0.0005d;
            double areaM2 = Math.PI * radiusM * radiusM;
            return areaM2 * SteelDensityKgPerM3;
        }

        public static double RebarKg(float lengthMm, float widthMm, float depthMm, float coverMm,
            float diameterMm, float stepMm)
        {
            if (lengthMm <= 0f) return 0d;

            double longitudinalLengthM = MinimalCageBarCount * lengthMm * 0.001d;

            double insetWidthMm = Math.Max(0f, widthMm - 2f * coverMm);
            double insetDepthMm = Math.Max(0f, depthMm - 2f * coverMm);
            double stirrupPerimeterM = 2d * (insetWidthMm + insetDepthMm) * 0.001d;

            int stirrupCount = stepMm > 0f
                ? (int)Math.Floor(Math.Max(0f, lengthMm) / stepMm) + 1
                : 0;
            double stirrupLengthM = stirrupCount * stirrupPerimeterM;

            double totalLengthM = longitudinalLengthM + stirrupLengthM;
            return totalLengthM * RebarMassPerMetreKg(diameterMm);
        }

        public static FoundationQuantitiesResult Of(SoilKind soil, float lengthMm, float widthMm,
            float depthMm, float sandMm, float gravelMm, float rebarDiameterMm, float rebarStepMm,
            float coverMm)
        {
            double lengthM = Math.Max(0f, lengthMm) * 0.001d;
            double widthM = Math.Max(0f, widthMm) * 0.001d;
            double depthM = Math.Max(0f, depthMm) * 0.001d;
            double sandM = Math.Max(0f, sandMm) * 0.001d;
            double gravelM = Math.Max(0f, gravelMm) * 0.001d;

            double sandM3 = lengthM * widthM * sandM;
            double gravelM3 = lengthM * widthM * gravelM;
            double concreteM3 = lengthM * widthM * depthM;
            double excavationNaturalM3 = lengthM * widthM * (depthM + sandM + gravelM);

            double excavationLooseM3 = FoundationLoosening.TryCoefficient(soil, out float kp)
                ? excavationNaturalM3 * kp
                : excavationNaturalM3;

            double formworkM2 = 2d * lengthM * depthM;
            double rebarKg = RebarKg(lengthMm, widthMm, depthMm, coverMm, rebarDiameterMm, rebarStepMm);

            return new FoundationQuantitiesResult(excavationNaturalM3, excavationLooseM3,
                sandM3, gravelM3, concreteM3, formworkM2, rebarKg);
        }

        public static FoundationQuantitiesResult OfLoadBearingWalls(SoilKind soil,
            IReadOnlyList<WallCentreline> loadBearingCentrelines, float widthMm, float depthMm,
            float sandMm, float gravelMm, float rebarDiameterMm, float rebarStepMm, float coverMm)
        {
            float lengthMm = FoundationLayout.TotalLengthMm(loadBearingCentrelines);
            return Of(soil, lengthMm, widthMm, depthMm, sandMm, gravelMm, rebarDiameterMm,
                rebarStepMm, coverMm);
        }
    }
}
