using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ShowerColumnSpec
    {
        public const int DefaultColumnHeightMM = 1150;
        public const int DefaultRiserDiameterMM = 32;
        public const int DefaultHeadDiameterMM = 250;
        public const int DefaultHeadThicknessMM = 30;
        public const int DefaultArmReachMM = 380;
        public const int DefaultWallOffsetMM = 60;
        public const int DefaultHandShowerDiameterMM = 110;
        public const int DefaultHoseLengthMM = 1000;

        public const int MinColumnHeightMM = 500;
        public const int MaxColumnHeightMM = 2200;
        public const int MinRiserDiameterMM = 16;
        public const int MaxRiserDiameterMM = 60;
        public const int MinHeadDiameterMM = 80;
        public const int MaxHeadDiameterMM = 600;
        public const int MinHeadThicknessMM = 8;
        public const int MaxHeadThicknessMM = 120;
        public const int MinWallOffsetMM = 20;
        public const int MaxWallOffsetMM = 200;
        public const int MinArmClearanceMM = 40;
        public const int MaxArmReachMM = 800;
        public const int MinHandShowerDiameterMM = 60;
        public const int MaxHandShowerDiameterMM = 200;
        public const int MinHoseLengthMM = 300;
        public const int MaxHoseLengthMM = 3000;

        public readonly int ColumnHeightMM;
        public readonly int RiserDiameterMM;
        public readonly int HeadDiameterMM;
        public readonly int HeadThicknessMM;
        public readonly int ArmReachMM;
        public readonly int WallOffsetMM;
        public readonly int HandShowerDiameterMM;
        public readonly int HoseLengthMM;

        private ShowerColumnSpec(int columnHeightMM, int riserDiameterMM, int headDiameterMM,
            int headThicknessMM, int armReachMM, int wallOffsetMM, int handShowerDiameterMM,
            int hoseLengthMM)
        {
            ColumnHeightMM = columnHeightMM;
            RiserDiameterMM = riserDiameterMM;
            HeadDiameterMM = headDiameterMM;
            HeadThicknessMM = headThicknessMM;
            ArmReachMM = armReachMM;
            WallOffsetMM = wallOffsetMM;
            HandShowerDiameterMM = handShowerDiameterMM;
            HoseLengthMM = hoseLengthMM;
        }

        public static ShowerColumnSpec Default => Clamped(DefaultColumnHeightMM,
            DefaultRiserDiameterMM, DefaultHeadDiameterMM, DefaultHeadThicknessMM,
            DefaultArmReachMM, DefaultWallOffsetMM, DefaultHandShowerDiameterMM,
            DefaultHoseLengthMM);

        public static ShowerColumnSpec Clamped(int columnHeightMM, int riserDiameterMM,
            int headDiameterMM, int headThicknessMM, int armReachMM, int wallOffsetMM,
            int handShowerDiameterMM, int hoseLengthMM)
        {
            int riser = ClampRiserDiameterMM(riserDiameterMM);
            int offset = ClampWallOffsetMM(wallOffsetMM);
            int height = ClampColumnHeightMM(columnHeightMM, riser);

            return new ShowerColumnSpec(
                height,
                riser,
                ClampHeadDiameterMM(headDiameterMM),
                ClampHeadThicknessMM(headThicknessMM),
                ClampArmReachMM(armReachMM, riser, offset),
                offset,
                ClampHandShowerDiameterMM(handShowerDiameterMM),
                ClampHoseLengthMM(hoseLengthMM));
        }

        public static int ClampRiserDiameterMM(int value) =>
            Mathf.Clamp(value, MinRiserDiameterMM, MaxRiserDiameterMM);

        public static int ClampWallOffsetMM(int value) =>
            Mathf.Clamp(value, MinWallOffsetMM, MaxWallOffsetMM);

        public static int MinColumnHeightForMM(int riserDiameterMM) =>
            Mathf.Max(MinColumnHeightMM,
                Mathf.CeilToInt(ShowerColumnLayout.BendRadiusMM(ClampRiserDiameterMM(riserDiameterMM))
                    * 2f));

        public static int ClampColumnHeightMM(int value, int riserDiameterMM) =>
            Mathf.Clamp(value, MinColumnHeightForMM(riserDiameterMM), MaxColumnHeightMM);

        public static int ClampHeadDiameterMM(int value) =>
            Mathf.Clamp(value, MinHeadDiameterMM, MaxHeadDiameterMM);

        public static int ClampHeadThicknessMM(int value) =>
            Mathf.Clamp(value, MinHeadThicknessMM, MaxHeadThicknessMM);

        public static int MinArmReachForMM(int riserDiameterMM, int wallOffsetMM) =>
            ClampWallOffsetMM(wallOffsetMM) + MinArmClearanceMM
            + Mathf.CeilToInt(ShowerColumnLayout.BendRadiusMM(ClampRiserDiameterMM(riserDiameterMM)));

        public static int ClampArmReachMM(int value, int riserDiameterMM, int wallOffsetMM) =>
            Mathf.Clamp(value, MinArmReachForMM(riserDiameterMM, wallOffsetMM), MaxArmReachMM);

        public static int ClampHandShowerDiameterMM(int value) =>
            Mathf.Clamp(value, MinHandShowerDiameterMM, MaxHandShowerDiameterMM);

        public static int ClampHoseLengthMM(int value) =>
            Mathf.Clamp(value, MinHoseLengthMM, MaxHoseLengthMM);
    }
}
