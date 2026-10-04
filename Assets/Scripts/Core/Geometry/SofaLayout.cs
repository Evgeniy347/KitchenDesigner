using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SofaLayout
    {
        public const int BackrestBottomMM = 100;
        public const int BackrestHeightMM = 700;
        public const int BackrestThicknessMM = 180;
        public const int BackrestRadiusMM = 60;
        public const int OverallHeightMM = BackrestBottomMM + BackrestHeightMM;

        public const int DefaultWidthMM = 2000;
        public const int DefaultHeightMM = OverallHeightMM;
        public const int DefaultSeatDepthMM = 720;
        public const int DefaultDepthMM = BackrestThicknessMM + DefaultSeatDepthMM;
        public const int DefaultSeatHeightMM = 360;
        public const int DefaultCornerRadiusMM = 120;

        public const int MinSeatDepthMM = 400;
        public const int MinDepthMM = BackrestThicknessMM + MinSeatDepthMM;
        public const int MinSeatHeightMM = 300;
        public const int MaxSeatHeightMM = BackrestBottomMM + 2 * BackrestThicknessMM;

        public const int BackCushionThicknessMM = 200;
        public const int ArmCushionWidthMM = 320;
        public const int ArmCushionHeightMM = 240;
        public const int ArmNoseRadiusMM = 150;
        public const int ArmTailRadiusMM = 40;
        public const int CushionGapMM = 20;
        public const int CushionRadiusMM = 90;
        public const int MinPartMM = 100;
        public const int CushionCount = 4;

        public const int PartsAcrossWidth = 4;
        public const int MinWidthMM = PartsAcrossWidth * MinPartMM + (PartsAcrossWidth - 1) * CushionGapMM;
        public const float BackCushionMaxDepthFraction = 0.3f;

        public const string SeatName = "SofaSeat";
        public const string BackrestName = "SofaBackrest";
        public const string FrontGroupName = "SofaFront";
        public const string HingeGroupName = "SofaBackrestHinge";
        public const string ArmCushionLeftName = "SofaArmCushionLeft";
        public const string ArmCushionRightName = "SofaArmCushionRight";
        public const string BackCushionLeftName = "SofaBackCushionLeft";
        public const string BackCushionRightName = "SofaBackCushionRight";

        public static Vector3Int Normalise(Vector3Int dimensionsMM) => new Vector3Int(
            Mathf.Max(MinWidthMM, dimensionsMM.x), OverallHeightMM,
            Mathf.Max(MinDepthMM, dimensionsMM.z));

        public static int SeatDepthFor(int depthMM)
            => Mathf.Max(MinSeatDepthMM, depthMM - BackrestThicknessMM);

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => Mathf.Max(0, Mathf.Min(dimensionsMM.x, SeatDepthFor(dimensionsMM.z)) / 2);

        public static int ClampCornerRadiusMM(Vector3Int dimensionsMM, int value)
            => Mathf.Clamp(value, 0, MaxCornerRadiusMM(dimensionsMM));

        public static int ClampSeatHeightMM(int value)
            => Mathf.Clamp(value, MinSeatHeightMM, MaxSeatHeightMM);

        public static Vector2Int SeatSurfaceMM(Vector3Int dimensionsMM)
            => new Vector2Int(dimensionsMM.x, SeatDepthFor(dimensionsMM.z));

        public static float FloorYMM(Vector3Int dimensionsMM) => -dimensionsMM.y * 0.5f;

        public static float BackrestFrontZMM(Vector3Int dimensionsMM)
            => -dimensionsMM.z * 0.5f + BackrestThicknessMM;

        public static float BackCushionHeightFor(int seatHeightMM)
            => Mathf.Max(1, OverallHeightMM - seatHeightMM);

        public static float BackCushionThicknessFor(int depthMM)
            => Mathf.Max(MinPartMM, Mathf.Min(BackCushionThicknessMM,
                SeatDepthFor(depthMM) * BackCushionMaxDepthFraction));

        public static float ArmCushionLengthFor(int depthMM)
            => Mathf.Max(MinPartMM, SeatDepthFor(depthMM) - CushionGapMM);

        public static float ArmCushionWidthFor(int widthMM)
            => Mathf.Max(MinPartMM, Mathf.Min(ArmCushionWidthMM,
                (widthMM - (PartsAcrossWidth - 1) * CushionGapMM) / PartsAcrossWidth));

        public static float BackCushionWidthFor(int widthMM)
            => Mathf.Max(MinPartMM,
                (widthMM - 2f * ArmCushionWidthFor(widthMM)
                    - (PartsAcrossWidth - 1) * CushionGapMM) * 0.5f);

        public static FurniturePartBox Seat(Vector3Int dimensionsMM, int seatHeightMM,
            int cornerRadiusMM)
        {
            int seatDepth = SeatDepthFor(dimensionsMM.z);
            var centre = new Vector3(0f, FloorYMM(dimensionsMM) + seatHeightMM * 0.5f,
                BackrestFrontZMM(dimensionsMM) + seatDepth * 0.5f);

            return new FurniturePartBox(SeatName, centre, dimensionsMM.x, seatDepth,
                seatHeightMM, cornerRadiusMM, FurniturePartOrientation.Horizontal,
                FurniturePartShape.Extruded, 0f);
        }

        public static FurniturePartBox Backrest(Vector3Int dimensionsMM)
        {
            var centre = new Vector3(0f,
                FloorYMM(dimensionsMM) + BackrestBottomMM + BackrestHeightMM * 0.5f,
                BackrestFrontZMM(dimensionsMM) - BackrestThicknessMM * 0.5f);

            return new FurniturePartBox(BackrestName, centre, dimensionsMM.x,
                BackrestThicknessMM, BackrestHeightMM,
                FittedRadius(BackrestRadiusMM, dimensionsMM.x, BackrestThicknessMM),
                FurniturePartOrientation.Horizontal, FurniturePartShape.SoftSlab);
        }

        public static FurniturePartBox[] Cushions(Vector3Int dimensionsMM, int seatHeightMM)
        {
            float seatTopY = FloorYMM(dimensionsMM) + seatHeightMM;
            float frontOfBackrest = BackrestFrontZMM(dimensionsMM);
            float backHeight = BackCushionHeightFor(seatHeightMM);

            float armWidth = ArmCushionWidthFor(dimensionsMM.x);
            float armHeight = Mathf.Clamp(ArmCushionHeightMM, 1f, backHeight);
            float armLength = ArmCushionLengthFor(dimensionsMM.z);
            float armCentreX = dimensionsMM.x * 0.5f - armWidth * 0.5f;
            float armCentreY = seatTopY + armHeight * 0.5f;
            float armCentreZ = frontOfBackrest + CushionGapMM + armLength * 0.5f;

            float cushionWidth = BackCushionWidthFor(dimensionsMM.x);
            float cushionThickness = BackCushionThicknessFor(dimensionsMM.z);
            float cushionCentreX = CushionGapMM * 0.5f + cushionWidth * 0.5f;
            float cushionCentreY = seatTopY + backHeight * 0.5f;
            float cushionCentreZ = frontOfBackrest + cushionThickness * 0.5f;
            float cushionRadius = FittedCushionRadius(CushionRadiusMM, cushionWidth,
                backHeight, cushionThickness);

            return new[]
            {
                Arm(ArmCushionLeftName, -armCentreX, armCentreY, armCentreZ, armWidth,
                    armLength, armHeight),
                Arm(ArmCushionRightName, armCentreX, armCentreY, armCentreZ, armWidth,
                    armLength, armHeight),
                new FurniturePartBox(BackCushionLeftName,
                    new Vector3(-cushionCentreX, cushionCentreY, cushionCentreZ),
                    cushionWidth, backHeight, cushionThickness, cushionRadius,
                    FurniturePartOrientation.Frontal, FurniturePartShape.Cushion),
                new FurniturePartBox(BackCushionRightName,
                    new Vector3(cushionCentreX, cushionCentreY, cushionCentreZ),
                    cushionWidth, backHeight, cushionThickness, cushionRadius,
                    FurniturePartOrientation.Frontal, FurniturePartShape.Cushion),
            };
        }

        private static FurniturePartBox Arm(string name, float centreX, float centreY,
            float centreZ, float width, float length, float height)
            => new FurniturePartBox(name, new Vector3(centreX, centreY, centreZ), width, length,
                height, ArmNoseRadiusMM, FurniturePartOrientation.Horizontal,
                FurniturePartShape.SoftSlab, ArmTailRadiusMM);

        private static float FittedRadius(float asked, float profileWidth, float profileDepth)
            => Mathf.Max(0f, Mathf.Min(asked, Mathf.Min(profileWidth, profileDepth) * 0.5f));

        private static float FittedCushionRadius(float asked, float profileWidth,
            float profileDepth, float thickness)
            => FittedRadius(FittedRadius(asked, thickness, thickness), profileWidth, profileDepth);
    }
}
