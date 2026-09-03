namespace KitchenDesigner.Core
{
    public static class DoorOpeningLayout
    {
        public const int LeafFloorGapMM = 10;

        public const float WallBaseNorm = -0.5f;

        public static float CentreAboveWallBaseMM(int openingHeightMM) =>
            openingHeightMM * 0.5f;

        public static int LeafHeightMM(int openingHeightMM) =>
            openingHeightMM > LeafFloorGapMM ? openingHeightMM - LeafFloorGapMM : 0;

        public static float LeafCentreOffsetMM(int openingHeightMM) =>
            openingHeightMM > LeafFloorGapMM ? LeafFloorGapMM * 0.5f : 0f;

        public static Span GroundedSpanNorm(float centreNorm, float halfNorm)
        {
            float top = centreNorm + halfNorm;
            return new Span(WallBaseNorm, top > WallBaseNorm ? top : WallBaseNorm);
        }
    }
}
