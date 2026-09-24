using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CameraLevelFollow
    {
        public static Vector3 ShiftTargetForLevelChange(
            Vector3 target, int fromFloorElevationMm, int toFloorElevationMm)
        {
            float deltaUnits = (toFloorElevationMm - fromFloorElevationMm) * AppConstants.MM_TO_UNITS;
            return new Vector3(target.x, target.y + deltaUnits, target.z);
        }
    }
}
