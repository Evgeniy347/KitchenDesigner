using System;

namespace KitchenDesigner.Core.Construction
{
    public static class RoofPitchPlanes
    {
        public static bool RidgeAlongX(RoofFootprint footprint, RoofRidgeAxis requested)
        {
            switch (requested)
            {
                case RoofRidgeAxis.X: return true;
                case RoofRidgeAxis.Z: return false;
                default: return footprint.LongAxisIsX;
            }
        }

        public static RoofFrame Build(RoofFootprint footprint, RoofType type,
            RoofRidgeAxis ridgeAxis, float overhangMm)
        {
            bool ridgeAlongX = RidgeAlongX(footprint, ridgeAxis);
            float spanMm = ridgeAlongX ? footprint.WidthXMm : footprint.LengthZMm;
            float slopeMm = ridgeAlongX ? footprint.LengthZMm : footprint.WidthXMm;
            float overhang = Math.Max(0f, overhangMm);
            float extendedSpanMm = Math.Max(0f, spanMm) + 2f * overhang;
            float extendedSlopeMm = Math.Max(0f, slopeMm) + 2f * overhang;

            switch (type)
            {
                case RoofType.Single:
                    return Single(extendedSpanMm, extendedSlopeMm);
                case RoofType.Gable:
                    return Gable(extendedSpanMm, extendedSlopeMm, Math.Max(0f, spanMm));
                case RoofType.Hip:
                    return Hip(extendedSpanMm, extendedSlopeMm);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "неизвестный тип кровли");
            }
        }

        private static RoofFrame Single(float spanMm, float slopeMm)
        {
            var plane = new RoofPlaneFootprint(slopeMm, spanMm, spanMm);
            float perimeterMm = 2f * (spanMm + slopeMm);
            return new RoofFrame(new[] { plane }, 0f, perimeterMm, spanMm);
        }

        private static RoofFrame Gable(float spanMm, float slopeMm, float unextendedSpanMm)
        {
            float runMm = slopeMm * 0.5f;
            var plane = new RoofPlaneFootprint(runMm, spanMm, spanMm);
            float perimeterMm = 2f * (spanMm + slopeMm);
            float gutterMm = 2f * spanMm;
            return new RoofFrame(new[] { plane, plane }, unextendedSpanMm, perimeterMm, gutterMm);
        }

        private static RoofFrame Hip(float spanMm, float slopeMm)
        {
            float runMm = slopeMm * 0.5f;
            float ridgeMm = Math.Max(0f, spanMm - slopeMm);
            var trapezoid = new RoofPlaneFootprint(runMm, spanMm, ridgeMm);
            var triangle = new RoofPlaneFootprint(runMm, slopeMm, 0f);
            float perimeterMm = 2f * (spanMm + slopeMm);
            return new RoofFrame(new[] { trapezoid, trapezoid, triangle, triangle },
                ridgeMm, perimeterMm, perimeterMm);
        }
    }
}
