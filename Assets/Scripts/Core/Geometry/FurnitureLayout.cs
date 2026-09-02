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

        public static Vector3 PhysicalScale(Vector3Int dimensionsMM) => new Vector3(
            dimensionsMM.x * AppConstants.MM_TO_UNITS,
            dimensionsMM.y * AppConstants.MM_TO_UNITS,
            dimensionsMM.z * AppConstants.MM_TO_UNITS);

        public static Vector2Int TopSurfaceMM(Vector3Int dimensionsMM)
            => new Vector2Int(dimensionsMM.x, dimensionsMM.z);

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => Mathf.Max(0, Mathf.Min(dimensionsMM.x, dimensionsMM.z) / 2);

        public static int ClampCornerRadiusMM(Vector3Int dimensionsMM, int value)
            => Mathf.Clamp(value, 0, MaxCornerRadiusMM(dimensionsMM));

        public static Vector3 EulerAnglesFor(FurniturePartOrientation orientation)
            => orientation switch
            {
                FurniturePartOrientation.Frontal => new Vector3(-90f, 0f, 0f),
                FurniturePartOrientation.Side => new Vector3(-90f, 90f, 0f),
                _ => Vector3.zero,
            };
    }
}
