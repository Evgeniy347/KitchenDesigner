using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class Tolerance
    {
        public const float ContactMm = 0.5f;

        public const float EpsilonUnits = 1e-4f;

        public const float ParallelDot = 0.999f;

        public const float SnapEpsilon = 1e-5f;

        public const float EpsilonSqr = 1e-6f;

        public const float UpDotThreshold = 0.99f;

        public const float ClearanceMm = 0.1f;

        public static bool ApproxEqual(float a, float b) =>
            Mathf.Abs(a - b) < EpsilonUnits;

        public static bool IsNoiseMm(float mm) =>
            Mathf.Abs(mm) < ContactMm;

        public static bool IntervalsOverlap(float min1, float max1, float min2, float max2) =>
            min1 < max2 - EpsilonUnits && max1 > min2 + EpsilonUnits;

        public static bool IsParallel(float dot) =>
            Mathf.Abs(dot) >= ParallelDot;
    }
}
