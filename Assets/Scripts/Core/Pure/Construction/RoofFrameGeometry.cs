using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Construction
{
    public static class RoofFrameGeometry
    {
        public static List<Vector3[]> PlaneBoundariesUnits(RoofFootprint footprintMm, RoofType type,
            RoofRidgeAxis ridgeAxis, float overhangMm, float pitchDeg)
        {
            var boundaries = new List<Vector3[]>();

            RoofPitchPlanes.ExtendedSpanAndSlope(footprintMm, ridgeAxis, overhangMm,
                out float spanMm, out float slopeMm);
            bool ridgeAlongX = RoofPitchPlanes.RidgeAlongX(footprintMm, ridgeAxis);
            float tanPitch = Mathf.Tan(pitchDeg * Mathf.Deg2Rad);

            float halfSpan = spanMm * 0.5f;
            float halfSlope = slopeMm * 0.5f;

            switch (type)
            {
                case RoofType.Single:
                {
                    float rise = slopeMm * tanPitch;
                    boundaries.Add(Fix(new[]
                    {
                        Point(-halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, halfSlope, rise, ridgeAlongX),
                        Point(-halfSpan, halfSlope, rise, ridgeAlongX),
                    }));
                    break;
                }
                case RoofType.Gable:
                {
                    float rise = halfSlope * tanPitch;
                    boundaries.Add(Fix(new[]
                    {
                        Point(-halfSpan, halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, 0f, rise, ridgeAlongX),
                        Point(-halfSpan, 0f, rise, ridgeAlongX),
                    }));
                    boundaries.Add(Fix(new[]
                    {
                        Point(-halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, 0f, rise, ridgeAlongX),
                        Point(-halfSpan, 0f, rise, ridgeAlongX),
                    }));
                    break;
                }
                case RoofType.Hip:
                {
                    float rise = halfSlope * tanPitch;
                    float ridgeHalf = halfSpan - halfSlope;
                    boundaries.Add(Fix(new[]
                    {
                        Point(-halfSpan, halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, halfSlope, 0f, ridgeAlongX),
                        Point(ridgeHalf, 0f, rise, ridgeAlongX),
                        Point(-ridgeHalf, 0f, rise, ridgeAlongX),
                    }));
                    boundaries.Add(Fix(new[]
                    {
                        Point(-halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(ridgeHalf, 0f, rise, ridgeAlongX),
                        Point(-ridgeHalf, 0f, rise, ridgeAlongX),
                    }));
                    boundaries.Add(Fix(new[]
                    {
                        Point(halfSpan, halfSlope, 0f, ridgeAlongX),
                        Point(halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(ridgeHalf, 0f, rise, ridgeAlongX),
                    }));
                    boundaries.Add(Fix(new[]
                    {
                        Point(-halfSpan, -halfSlope, 0f, ridgeAlongX),
                        Point(-halfSpan, halfSlope, 0f, ridgeAlongX),
                        Point(-ridgeHalf, 0f, rise, ridgeAlongX),
                    }));
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "неизвестный тип кровли");
            }

            return boundaries;
        }

        private static Vector3 Point(float spanCoordMm, float slopeCoordMm, float riseMm,
            bool ridgeAlongX)
        {
            float toU = AppConstants.MM_TO_UNITS;
            return ridgeAlongX
                ? new Vector3(spanCoordMm * toU, riseMm * toU, slopeCoordMm * toU)
                : new Vector3(slopeCoordMm * toU, riseMm * toU, spanCoordMm * toU);
        }

        private static Vector3[] Fix(Vector3[] boundary)
        {
            if (boundary.Length < 3) return boundary;
            var cross = Vector3.Cross(boundary[1] - boundary[0], boundary[2] - boundary[0]);
            if (cross.y >= 0f) return boundary;
            Array.Reverse(boundary);
            return boundary;
        }
    }
}
