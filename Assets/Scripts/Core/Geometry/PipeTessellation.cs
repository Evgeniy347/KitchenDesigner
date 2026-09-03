using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PipeTessellation
    {
        public const int MinRadialSegments = 16;
        public const int MaxRadialSegments = 64;
        public const float MaxChordMM = 12f;

        public static int RadialSegmentsFor(float radiusMM)
        {
            if (radiusMM <= 0f) return MinRadialSegments;

            int wanted = Mathf.CeilToInt(Mathf.PI * 2f * radiusMM / MaxChordMM);
            return Mathf.Clamp(wanted + (wanted & 1), MinRadialSegments, MaxRadialSegments);
        }

        public static float SagittaMM(float radiusMM, int radialSegments) =>
            radiusMM * (1f - Mathf.Cos(Mathf.PI / Mathf.Max(1, radialSegments)));
    }
}
