using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SofaLayout
    {
        public const int DefaultWidthMM = 2000;
        public const int DefaultHeightMM = 800;
        public const int DefaultDepthMM = 900;
        public const int DefaultSeatHeightMM = 360;
        public const int DefaultCornerRadiusMM = 120;

        public const int MinBaseHeightMM = 150;
        public const int MinBackrestHeightMM = 200;

        public const int BackDepthMM = 140;
        public const int BackRailDropMM = 40;
        public const int BackRailRadiusMM = 60;
        public const int BackCushionThicknessMM = 200;
        public const int ArmCushionWidthMM = 320;
        public const int ArmCushionHeightMM = 240;
        public const int CushionGapMM = 20;
        public const int CushionRadiusMM = 90;
        public const int MinPartMM = 100;
        public const int CushionCount = 4;

        public const int PartsAcrossWidth = 4;
        public const float BackDepthMaxDepthFraction = 0.25f;
        public const float BackCushionMaxDepthFraction = 0.3f;

        public const string BackRailName = "SofaBackRail";
        public const string ArmCushionLeftName = "SofaArmCushionLeft";
        public const string ArmCushionRightName = "SofaArmCushionRight";
        public const string BackCushionLeftName = "SofaBackCushionLeft";
        public const string BackCushionRightName = "SofaBackCushionRight";

        public static float BaseCentreYMM(int overallHeightMM, int seatHeightMM)
            => (seatHeightMM - overallHeightMM) * 0.5f;

        public static int BackrestHeightMM(int overallHeightMM, int seatHeightMM)
            => Mathf.Max(1, overallHeightMM - seatHeightMM);

        public static float BackDepthFor(int depthMM)
            => Mathf.Max(MinPartMM,
                Mathf.Min(BackDepthMM, depthMM * BackDepthMaxDepthFraction));

        public static float BackCushionThicknessFor(int depthMM)
            => Mathf.Max(MinPartMM,
                Mathf.Min(BackCushionThicknessMM, depthMM * BackCushionMaxDepthFraction));

        public static float ArmCushionLengthFor(int depthMM)
            => Mathf.Max(MinPartMM, depthMM - BackDepthFor(depthMM) - CushionGapMM);

        public static float ArmCushionWidthFor(int widthMM)
            => Mathf.Max(MinPartMM, Mathf.Min(ArmCushionWidthMM,
                (widthMM - (PartsAcrossWidth - 1) * CushionGapMM) / PartsAcrossWidth));

        public static float BackCushionWidthFor(int widthMM)
            => Mathf.Max(MinPartMM,
                (widthMM - 2f * ArmCushionWidthFor(widthMM)
                    - (PartsAcrossWidth - 1) * CushionGapMM) * 0.5f);

        public static FurniturePartBox BackRail(Vector3Int dimensionsMM, int seatHeightMM)
        {
            float backDepth = BackDepthFor(dimensionsMM.z);
            float railHeight = Mathf.Max(1f,
                BackrestHeightMM(dimensionsMM.y, seatHeightMM) - BackRailDropMM);
            float centreY = -dimensionsMM.y * 0.5f + seatHeightMM + railHeight * 0.5f;
            float centreZ = -dimensionsMM.z * 0.5f + backDepth * 0.5f;

            return new FurniturePartBox(BackRailName, new Vector3(0f, centreY, centreZ),
                dimensionsMM.x, backDepth, railHeight,
                FittedRadius(BackRailRadiusMM, dimensionsMM.x, backDepth),
                FurniturePartOrientation.Horizontal);
        }

        public static FurniturePartBox[] Cushions(Vector3Int dimensionsMM, int seatHeightMM)
        {
            float floorY = -dimensionsMM.y * 0.5f;
            float backZ = -dimensionsMM.z * 0.5f;
            float backDepth = BackDepthFor(dimensionsMM.z);
            float backrestHeight = BackrestHeightMM(dimensionsMM.y, seatHeightMM);

            float armWidth = ArmCushionWidthFor(dimensionsMM.x);
            float armHeight = Mathf.Clamp(ArmCushionHeightMM, 1f, backrestHeight);
            float armLength = ArmCushionLengthFor(dimensionsMM.z);
            float armCentreX = dimensionsMM.x * 0.5f - armWidth * 0.5f;
            float armCentreY = floorY + seatHeightMM + armHeight * 0.5f;
            float armCentreZ = backZ + backDepth + CushionGapMM + armLength * 0.5f;
            float armRadius = FittedCushionRadius(CushionRadiusMM, armLength, armHeight, armWidth);

            float cushionWidth = BackCushionWidthFor(dimensionsMM.x);
            float cushionThickness = BackCushionThicknessFor(dimensionsMM.z);
            float cushionCentreX = CushionGapMM * 0.5f + cushionWidth * 0.5f;
            float cushionCentreY = floorY + seatHeightMM + backrestHeight * 0.5f;
            float cushionCentreZ = backZ + backDepth + cushionThickness * 0.5f;
            float cushionRadius = FittedCushionRadius(CushionRadiusMM, cushionWidth,
                backrestHeight, cushionThickness);

            return new[]
            {
                new FurniturePartBox(ArmCushionLeftName,
                    new Vector3(-armCentreX, armCentreY, armCentreZ),
                    armLength, armHeight, armWidth, armRadius,
                    FurniturePartOrientation.Side, FurniturePartShape.Cushion),
                new FurniturePartBox(ArmCushionRightName,
                    new Vector3(armCentreX, armCentreY, armCentreZ),
                    armLength, armHeight, armWidth, armRadius,
                    FurniturePartOrientation.Side, FurniturePartShape.Cushion),
                new FurniturePartBox(BackCushionLeftName,
                    new Vector3(-cushionCentreX, cushionCentreY, cushionCentreZ),
                    cushionWidth, backrestHeight, cushionThickness, cushionRadius,
                    FurniturePartOrientation.Frontal, FurniturePartShape.Cushion),
                new FurniturePartBox(BackCushionRightName,
                    new Vector3(cushionCentreX, cushionCentreY, cushionCentreZ),
                    cushionWidth, backrestHeight, cushionThickness, cushionRadius,
                    FurniturePartOrientation.Frontal, FurniturePartShape.Cushion),
            };
        }

        private static float FittedRadius(float asked, float profileWidth, float profileDepth)
            => Mathf.Max(0f, Mathf.Min(asked, Mathf.Min(profileWidth, profileDepth) * 0.5f));

        private static float FittedCushionRadius(float asked, float profileWidth,
            float profileDepth, float thickness)
            => FittedRadius(FittedRadius(asked, thickness, thickness), profileWidth, profileDepth);
    }
}
