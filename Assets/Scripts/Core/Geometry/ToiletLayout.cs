using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ToiletLayout
    {
        public const int WidthMM = 360;
        public const int HeightMM = 790;
        public const int DepthMM = 660;

        public const int DefaultSeatHeightMM = 400;
        public const int MinSeatHeightMM = 350;

        public const int SeatThicknessMM = 20;
        public const int LidThicknessMM = 18;
        public const int BowlBodyHeightMM = 210;
        public const int MinCisternHeightMM = 250;

        public const int CisternDepthMM = 200;
        public const int BowlDepthMM = 460;
        public const int PedestalWidthMM = 200;
        public const int PedestalDepthMM = 300;
        public const int SeatInsetMM = 10;

        public const int ButtonWidthMM = 90;
        public const int ButtonDepthMM = 60;
        public const int ButtonThicknessMM = 10;
        public const int ButtonCornerRadiusMM = 25;

        public const int BowlPlanRadiusMM = 170;
        public const int CisternPlanRadiusMM = 40;
        public const int PedestalPlanRadiusMM = 60;
        public const int SeatPlanRadiusMM = 165;

        public const string PedestalName = "ToiletPedestal";
        public const string BowlName = "ToiletBowl";
        public const string CisternName = "ToiletCistern";
        public const string SeatName = "ToiletSeat";
        public const string LidName = "ToiletLid";
        public const string ButtonName = "ToiletFlushButton";

        public static Vector3Int DimensionsMM => new Vector3Int(WidthMM, HeightMM, DepthMM);

        public static int MaxSeatHeightMM =>
            HeightMM - SeatThicknessMM - LidThicknessMM - MinCisternHeightMM;

        public static int ClampSeatHeightMM(int value)
            => Mathf.Clamp(value, MinSeatHeightMM, MaxSeatHeightMM);

        public static int SeatTopMM(int seatHeightMM)
            => ClampSeatHeightMM(seatHeightMM) + SeatThicknessMM;

        public static int LidTopMM(int seatHeightMM) => SeatTopMM(seatHeightMM) + LidThicknessMM;

        public static int CisternHeightMM(int seatHeightMM) => HeightMM - LidTopMM(seatHeightMM);

        public static int PedestalHeightMM(int seatHeightMM)
            => ClampSeatHeightMM(seatHeightMM) - BowlBodyHeightMM;

        public static float CentreY(float bottomAboveFloorMM, float topAboveFloorMM)
            => (bottomAboveFloorMM + topAboveFloorMM) * 0.5f - HeightMM * 0.5f;

        public static float BackEdgeZMM => -DepthMM * 0.5f;

        public static float CisternCentreZMM => BackEdgeZMM + CisternDepthMM * 0.5f;

        public static float BowlCentreZMM => DepthMM * 0.5f - BowlDepthMM * 0.5f;

        public static float PedestalCentreZMM => BowlCentreZMM - BowlDepthMM * 0.5f
            + PedestalDepthMM * 0.5f + PedestalFrontInsetMM;

        public const int PedestalFrontInsetMM = 20;

        public static FurniturePartBox[] CeramicParts(int seatHeightMM)
        {
            int seat = ClampSeatHeightMM(seatHeightMM);
            int seatTop = SeatTopMM(seat);
            int lidTop = LidTopMM(seat);

            return new[]
            {
                Slab(PedestalName, PedestalCentreZMM, CentreY(0, PedestalHeightMM(seat)),
                    PedestalWidthMM, PedestalDepthMM, PedestalHeightMM(seat),
                    PedestalPlanRadiusMM),

                Slab(BowlName, BowlCentreZMM, CentreY(seat - BowlBodyHeightMM, seat),
                    WidthMM, BowlDepthMM, BowlBodyHeightMM, BowlPlanRadiusMM),

                Slab(SeatName, BowlCentreZMM, CentreY(seat, seatTop),
                    WidthMM - 2 * SeatInsetMM, BowlDepthMM - 2 * SeatInsetMM,
                    SeatThicknessMM, SeatPlanRadiusMM),

                Slab(LidName, BowlCentreZMM, CentreY(seatTop, lidTop),
                    WidthMM - 2 * SeatInsetMM, BowlDepthMM - 2 * SeatInsetMM,
                    LidThicknessMM, SeatPlanRadiusMM),

                Slab(CisternName, CisternCentreZMM, CentreY(lidTop, HeightMM),
                    WidthMM, CisternDepthMM, CisternHeightMM(seat), CisternPlanRadiusMM),
            };
        }

        public static FurniturePartBox[] ChromeParts() => new[]
        {
            Slab(ButtonName, CisternCentreZMM, CentreY(HeightMM - ButtonThicknessMM, HeightMM),
                ButtonWidthMM, ButtonDepthMM, ButtonThicknessMM, ButtonCornerRadiusMM),
        };

        private static FurniturePartBox Slab(string name, float centreZMM, float centreYMM,
            float widthMM, float depthMM, float thicknessMM, float planRadiusMM)
            => new FurniturePartBox(name, new Vector3(0f, centreYMM, centreZMM),
                widthMM, depthMM, thicknessMM, planRadiusMM,
                FurniturePartOrientation.Horizontal, FurniturePartShape.SoftSlab);
    }
}
