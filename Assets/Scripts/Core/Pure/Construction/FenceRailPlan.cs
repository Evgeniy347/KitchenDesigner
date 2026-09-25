namespace KitchenDesigner.Core.Construction
{
    public static class FenceRailPlan
    {
        public const float HeightThresholdMm = 2000f;
        public const int RailCountBelowThreshold = 2;
        public const int RailCountAtOrAboveThreshold = 3;

        public static int RailCountFor(float heightMm) =>
            heightMm >= HeightThresholdMm ? RailCountAtOrAboveThreshold : RailCountBelowThreshold;
    }
}
