using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FurnitureLayout
    {
        public static int LegHeightMM(int overallHeightMM, int topThicknessMM)
            => Mathf.Max(1, overallHeightMM - topThicknessMM);

        public static float TopCentreY(int overallHeightMM, int topThicknessMM)
            => (overallHeightMM * 0.5f - topThicknessMM * 0.5f) * AppConstants.MM_TO_UNITS;

        public static float LegCentreY(int overallHeightMM, int topThicknessMM)
            => (LegHeightMM(overallHeightMM, topThicknessMM) * 0.5f - overallHeightMM * 0.5f)
                * AppConstants.MM_TO_UNITS;
    }
}
