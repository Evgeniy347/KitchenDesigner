using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CapsuleTableMesh
    {
        public const int Segments = RoundedRectProfile.DefaultSegments;

        public static Mesh Build(float widthUnits, float thicknessUnits, float depthUnits,
            float yOffset = 0f)
        {
            float radius = Mathf.Min(widthUnits, depthUnits) * 0.5f;
            var profile = RoundedRectProfile.Uniform(widthUnits, depthUnits, radius, Segments);
            return ProfileExtrusionMesh.Build(profile, widthUnits, depthUnits, thicknessUnits, yOffset);
        }

        public const int MinLegInsetFromContourMM = 50;

        public static Vector3[] GetLegPositions(float widthMM, float heightMM, float depthMM,
            float legCenterY, int legInsetMM = 0,
            int legCrossSectionMM = RadiusTableElement.LegCrossSectionMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float a = Mathf.Max(Tolerance.EpsilonUnits, widthMM * toU * 0.5f);
            float b = Mathf.Max(Tolerance.EpsilonUnits, depthMM * toU * 0.5f);

            float insetU = Mathf.Max(legInsetMM, MinLegInsetFromContourMM) * toU;
            float legCircumradiusU = legCrossSectionMM * toU * Mathf.Sqrt(2f) * 0.5f;
            float pullU = insetU + legCircumradiusU;

            var quadrants = new[]
            {
                new Vector2( 1f,  1f),
                new Vector2( 1f, -1f),
                new Vector2(-1f,  1f),
                new Vector2(-1f, -1f),
            };

            float cos45 = Mathf.Cos(Mathf.PI * 0.25f);
            float sin45 = Mathf.Sin(Mathf.PI * 0.25f);

            var legs = new Vector3[quadrants.Length];
            for (int i = 0; i < quadrants.Length; i++)
            {
                float sx = quadrants[i].x, sz = quadrants[i].y;
                var onContour = new Vector2(a * cos45 * sx, b * sin45 * sz);
                var inward = new Vector2(-cos45 * sx / a, -sin45 * sz / b).normalized;
                float pull = Mathf.Min(pullU, onContour.magnitude);
                var centre = onContour + inward * pull;
                legs[i] = new Vector3(centre.x, legCenterY, centre.y);
            }

            return legs;
        }
    }
}
