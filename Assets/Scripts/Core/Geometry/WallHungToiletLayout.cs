using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallHungToiletLayout
    {
        public const int WidthMM = 360;
        public const int HeightMM = 1000;
        public const int DepthMM = 540;

        public const int DefaultSeatHeightMM = 400;
        public const int MinSeatHeightMM = 350;
        public const int MaxSeatHeightMM = 600;

        public const int BowlBodyHeightMM = 300;
        public const int BowlDepthMM = 500;
        public const int SeatThicknessMM = 20;
        public const int LidThicknessMM = 18;
        public const int SeatDepthMM = 460;
        public const int SeatInsetMM = 10;

        public const int PlateWidthMM = 240;
        public const int PlateFaceHeightMM = 165;
        public const int PlateThicknessMM = 20;
        public const int DefaultPlateBottomMM = 600;
        public const int MinPlateGapAboveLidMM = 50;

        public const int ButtonLargeWidthMM = 110;
        public const int ButtonSmallWidthMM = 70;
        public const int ButtonFaceHeightMM = 115;
        public const int ButtonThicknessMM = 8;
        public const int ButtonGapMM = 10;
        public const int ButtonSideMarginMM = 25;
        public const int ButtonCornerRadiusMM = 12;

        public const int BowlPlanRadiusMM = 170;
        public const int SeatPlanRadiusMM = 165;
        public const int PlateCornerRadiusMM = 12;

        public const string BowlName = "WallHungToiletBowl";
        public const string SeatName = "WallHungToiletSeat";
        public const string LidName = "WallHungToiletLid";
        public const string PlateName = "WallHungToiletFlushPlate";
        public const string ButtonLargeName = "WallHungToiletButtonFull";
        public const string ButtonSmallName = "WallHungToiletButtonHalf";

        public static Vector3Int DimensionsMM => new Vector3Int(WidthMM, HeightMM, DepthMM);

        public static int ClampSeatHeightMM(int value)
            => Mathf.Clamp(value, MinSeatHeightMM, MaxSeatHeightMM);

        public static int LidTopMM(int seatHeightMM)
            => ClampSeatHeightMM(seatHeightMM) + SeatThicknessMM + LidThicknessMM;

        public static int MinPlateBottomMM(int seatHeightMM)
            => LidTopMM(seatHeightMM) + MinPlateGapAboveLidMM;

        public static int MaxPlateBottomMM => HeightMM - PlateFaceHeightMM;

        public static int ClampPlateBottomMM(int seatHeightMM, int value)
            => Mathf.Clamp(value, MinPlateBottomMM(seatHeightMM), MaxPlateBottomMM);

        public static int BowlBottomMM(int seatHeightMM)
            => ClampSeatHeightMM(seatHeightMM) - BowlBodyHeightMM;

        public static float CentreY(float bottomAboveFloorMM, float topAboveFloorMM)
            => (bottomAboveFloorMM + topAboveFloorMM) * 0.5f - HeightMM * 0.5f;

        public static float BackEdgeZMM => -DepthMM * 0.5f;

        public static float PlateCentreZMM => BackEdgeZMM + PlateThicknessMM * 0.5f;

        public static float PlateFaceZMM => BackEdgeZMM + PlateThicknessMM;

        public static float ButtonCentreZMM => PlateFaceZMM + ButtonThicknessMM * 0.5f;

        public static float BowlCentreZMM => BackEdgeZMM + BowlDepthMM * 0.5f;

        public static float SeatCentreZMM => BackEdgeZMM + PlateThicknessMM + SeatDepthMM * 0.5f;

        public static float ButtonLargeCentreXMM =>
            -PlateWidthMM * 0.5f + ButtonSideMarginMM + ButtonLargeWidthMM * 0.5f;

        public static float ButtonSmallCentreXMM =>
            ButtonLargeCentreXMM + ButtonLargeWidthMM * 0.5f + ButtonGapMM
            + ButtonSmallWidthMM * 0.5f;

        public static FurniturePartBox[] CeramicParts(int seatHeightMM)
        {
            int seat = ClampSeatHeightMM(seatHeightMM);
            int seatTop = seat + SeatThicknessMM;

            return new[]
            {
                Slab(BowlName, 0f, BowlCentreZMM, CentreY(BowlBottomMM(seat), seat),
                    WidthMM, BowlDepthMM, BowlBodyHeightMM, BowlPlanRadiusMM),

                Slab(SeatName, 0f, SeatCentreZMM, CentreY(seat, seatTop),
                    WidthMM - 2 * SeatInsetMM, SeatDepthMM, SeatThicknessMM, SeatPlanRadiusMM),

                Slab(LidName, 0f, SeatCentreZMM, CentreY(seatTop, LidTopMM(seat)),
                    WidthMM - 2 * SeatInsetMM, SeatDepthMM, LidThicknessMM, SeatPlanRadiusMM),
            };
        }

        public static FurniturePartBox[] ChromeParts(int seatHeightMM, int plateBottomMM)
        {
            int bottom = ClampPlateBottomMM(seatHeightMM, plateBottomMM);
            float plateCentreY = CentreY(bottom, bottom + PlateFaceHeightMM);

            return new[]
            {
                Panel(PlateName, 0f, PlateCentreZMM, plateCentreY,
                    PlateWidthMM, PlateFaceHeightMM, PlateThicknessMM, PlateCornerRadiusMM),

                Panel(ButtonLargeName, ButtonLargeCentreXMM, ButtonCentreZMM, plateCentreY,
                    ButtonLargeWidthMM, ButtonFaceHeightMM, ButtonThicknessMM,
                    ButtonCornerRadiusMM),

                Panel(ButtonSmallName, ButtonSmallCentreXMM, ButtonCentreZMM, plateCentreY,
                    ButtonSmallWidthMM, ButtonFaceHeightMM, ButtonThicknessMM,
                    ButtonCornerRadiusMM),
            };
        }

        private static FurniturePartBox Slab(string name, float centreXMM, float centreZMM,
            float centreYMM, float widthMM, float depthMM, float thicknessMM, float planRadiusMM)
            => new FurniturePartBox(name, new Vector3(centreXMM, centreYMM, centreZMM),
                widthMM, depthMM, thicknessMM, planRadiusMM,
                FurniturePartOrientation.Horizontal, FurniturePartShape.SoftSlab);

        private static FurniturePartBox Panel(string name, float centreXMM, float centreZMM,
            float centreYMM, float widthMM, float faceHeightMM, float thicknessMM,
            float radiusMM)
            => new FurniturePartBox(name, new Vector3(centreXMM, centreYMM, centreZMM),
                widthMM, faceHeightMM, thicknessMM, radiusMM,
                FurniturePartOrientation.Frontal, FurniturePartShape.Extruded);
    }
}
