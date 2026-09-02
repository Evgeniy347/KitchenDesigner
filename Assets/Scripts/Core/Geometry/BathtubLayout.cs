using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathtubLayout
    {
        public const int DefaultWidthMM = 1700;
        public const int DefaultHeightMM = 600;
        public const int DefaultDepthMM = 700;

        public const int DefaultRimWidthMM = 40;
        public const int DefaultBowlRadiusMM = 120;
        public const int DefaultBowlDepthMM = 450;
        public const int DefaultBowlFilletMM = 80;

        public const int MinRimWidthMM = 5;
        public const int MinBowlSideMM = 200;
        public const int MinBowlDepthMM = 50;
        public const int MinFloorThicknessMM = 30;
        public const int MinBowlFloorSideMM = 60;

        public static int MaxRimWidthMM(Vector3Int dimensionsMM)
            => Mathf.Max(MinRimWidthMM,
                (Mathf.Min(dimensionsMM.x, dimensionsMM.z) - MinBowlSideMM) / 2);

        public static int ClampRimWidthMM(Vector3Int dimensionsMM, int value)
            => Mathf.Clamp(value, MinRimWidthMM, MaxRimWidthMM(dimensionsMM));

        public static int InnerWidthMM(Vector3Int dimensionsMM, int rimWidthMM)
            => Mathf.Max(1, dimensionsMM.x - 2 * ClampRimWidthMM(dimensionsMM, rimWidthMM));

        public static int InnerDepthMM(Vector3Int dimensionsMM, int rimWidthMM)
            => Mathf.Max(1, dimensionsMM.z - 2 * ClampRimWidthMM(dimensionsMM, rimWidthMM));

        public static int InnerSideMM(Vector3Int dimensionsMM, int rimWidthMM)
            => Mathf.Min(InnerWidthMM(dimensionsMM, rimWidthMM),
                InnerDepthMM(dimensionsMM, rimWidthMM));

        public static int MaxBowlRadiusMM(Vector3Int dimensionsMM, int rimWidthMM)
            => Mathf.Max(0, InnerSideMM(dimensionsMM, rimWidthMM) / 2);

        public static int ClampBowlRadiusMM(Vector3Int dimensionsMM, int rimWidthMM, int value)
            => Mathf.Clamp(value, 0, MaxBowlRadiusMM(dimensionsMM, rimWidthMM));

        public static int MaxBowlDepthMM(Vector3Int dimensionsMM)
            => Mathf.Max(MinBowlDepthMM, dimensionsMM.y - MinFloorThicknessMM);

        public static int ClampBowlDepthMM(Vector3Int dimensionsMM, int value)
            => Mathf.Clamp(value, MinBowlDepthMM, MaxBowlDepthMM(dimensionsMM));

        public static int MaxBowlFilletMM(Vector3Int dimensionsMM, int rimWidthMM,
            int bowlDepthMM)
            => Mathf.Max(0, Mathf.Min(ClampBowlDepthMM(dimensionsMM, bowlDepthMM),
                (InnerSideMM(dimensionsMM, rimWidthMM) - MinBowlFloorSideMM) / 2));

        public static int ClampBowlFilletMM(Vector3Int dimensionsMM, int rimWidthMM,
            int bowlDepthMM, int value)
            => Mathf.Clamp(value, 0, MaxBowlFilletMM(dimensionsMM, rimWidthMM, bowlDepthMM));

        public static int ShellCornerRadiusMM(Vector3Int dimensionsMM, int rimWidthMM,
            int bowlRadiusMM)
            => ClampBowlRadiusMM(dimensionsMM, rimWidthMM, bowlRadiusMM)
                + ClampRimWidthMM(dimensionsMM, rimWidthMM);

        public static float BowlFloorYMM(Vector3Int dimensionsMM, int bowlDepthMM)
            => dimensionsMM.y * 0.5f - ClampBowlDepthMM(dimensionsMM, bowlDepthMM);

        public static Vector3Int DefaultDimensionsMM
            => new Vector3Int(DefaultWidthMM, DefaultHeightMM, DefaultDepthMM);
    }
}
