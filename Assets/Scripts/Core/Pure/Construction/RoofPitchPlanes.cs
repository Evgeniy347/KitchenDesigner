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

        public static void ExtendedSpanAndSlope(RoofFootprint footprint, RoofRidgeAxis ridgeAxis,
            float overhangMm, out float spanMm, out float slopeMm)
        {
            RawAndExtendedSpanAndSlope(footprint, ridgeAxis, overhangMm,
                out _, out spanMm, out slopeMm);
        }

        private static void RawAndExtendedSpanAndSlope(RoofFootprint footprint,
            RoofRidgeAxis ridgeAxis, float overhangMm, out float rawSpanMm,
            out float extendedSpanMm, out float extendedSlopeMm)
        {
            bool ridgeAlongX = RidgeAlongX(footprint, ridgeAxis);
            float span = ridgeAlongX ? footprint.WidthXMm : footprint.LengthZMm;
            float slope = ridgeAlongX ? footprint.LengthZMm : footprint.WidthXMm;
            float overhang = Math.Max(0f, overhangMm);
            rawSpanMm = Math.Max(0f, span);
            extendedSpanMm = rawSpanMm + 2f * overhang;
            extendedSlopeMm = Math.Max(0f, slope) + 2f * overhang;
        }

        public static RoofFrame Build(RoofFootprint footprint, RoofType type,
            RoofRidgeAxis ridgeAxis, float overhangMm)
        {
            RawAndExtendedSpanAndSlope(footprint, ridgeAxis, overhangMm,
                out float rawSpanMm, out float extendedSpanMm, out float extendedSlopeMm);

            switch (type)
            {
                case RoofType.Single:
                    return Single(extendedSpanMm, extendedSlopeMm);
                case RoofType.Gable:
                    return Gable(extendedSpanMm, extendedSlopeMm, rawSpanMm);
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
