using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PartHighlightBands
    {
        public const float BandFraction = 0.2f;

        public const float BandMaxMM = 50f;

        public const float FittingMouthBandMM = 10f;

        public static float DepthMM(float spanMM) =>
            spanMM <= 0f ? 0f : Mathf.Min(spanMM * BandFraction, BandMaxMM);

        public static float MouthDepthMM(float legLengthMM) =>
            legLengthMM <= 0f ? 0f : Mathf.Min(FittingMouthBandMM, legLengthMM);
    }
}
