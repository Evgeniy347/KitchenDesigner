using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PouffeLayout
    {
        public const int DefaultWidthMM = 450;
        public const int DefaultHeightMM = 400;
        public const int DefaultDepthMM = 450;
        public const int DefaultCornerRadiusMM = 120;
        public const int DefaultSeatThicknessMM = 50;

        public const int MinSeatThicknessMM = 20;
        public const int SeatThicknessHeightDivisor = 3;
        public const int MinSeatInsetMM = 15;
        public const int MinSeatSideMM = 10;

        public const string SeatName = "PouffeSeat";

        public static readonly float CornerPullRatio = 1f - Mathf.Sqrt(0.5f);

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => FurnitureLayout.MaxCornerRadiusMM(dimensionsMM);

        public static int ClampCornerRadiusMM(Vector3Int dimensionsMM, int value)
            => FurnitureLayout.ClampCornerRadiusMM(dimensionsMM, value);

        public static int MaxSeatThicknessMM(int overallHeightMM)
            => Mathf.Max(MinSeatThicknessMM, overallHeightMM / SeatThicknessHeightDivisor);

        public static int ClampSeatThicknessMM(int overallHeightMM, int value)
            => Mathf.Clamp(value, MinSeatThicknessMM, MaxSeatThicknessMM(overallHeightMM));

        public static int BodyHeightMM(int overallHeightMM, int seatThicknessMM)
            => FurnitureLayout.LegHeightMM(overallHeightMM, seatThicknessMM);

        public static float BodyCentreY(int overallHeightMM, int seatThicknessMM)
            => FurnitureLayout.LegCentreY(overallHeightMM, seatThicknessMM);

        public static float SeatCentreYMM(int overallHeightMM, int seatThicknessMM)
            => (overallHeightMM - seatThicknessMM) * 0.5f;

        public static int SeatInsetMM(int cornerRadiusMM)
            => Mathf.Max(MinSeatInsetMM,
                Mathf.CeilToInt(Mathf.Max(0, cornerRadiusMM) * CornerPullRatio));

        public static float SeatRadiusMM(int cornerRadiusMM)
            => Mathf.Max(0f, Mathf.Max(0, cornerRadiusMM) - SeatInsetMM(cornerRadiusMM));

        public static SofaPartBox Seat(Vector3Int dimensionsMM, int cornerRadiusMM,
            int seatThicknessMM)
        {
            int radiusMM = ClampCornerRadiusMM(dimensionsMM, cornerRadiusMM);
            int thicknessMM = ClampSeatThicknessMM(dimensionsMM.y, seatThicknessMM);
            int insetMM = SeatInsetMM(radiusMM);

            return new SofaPartBox(SeatName,
                new Vector3(0f, SeatCentreYMM(dimensionsMM.y, thicknessMM), 0f),
                Mathf.Max(MinSeatSideMM, dimensionsMM.x - 2f * insetMM),
                Mathf.Max(MinSeatSideMM, dimensionsMM.z - 2f * insetMM),
                thicknessMM,
                SeatRadiusMM(radiusMM),
                SofaPartOrientation.Horizontal, SofaPartShape.SoftSlab);
        }
    }
}
