using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class WallSeating
    {
        public const float RotationEpsilonDegrees = 0.05f;

        public static void Seat(KitchenElement element, int depthMM)
        {
            if (element == null) return;

            var wall = WallProximity.Nearest(element);
            if (wall == null) return;

            var centre = wall.FullPosition;
            var normal = WallMountedPose.OutwardNormal(WallProximity.FaceNormal(wall), centre,
                element.transform.position);
            float standoff = WallProximity.HalfThicknessUnits(wall)
                + AppConstants.HalfHeightUnits(depthMM);

            var seated = WallMountedPose.SeatedPosition(element.transform.position, centre,
                normal, standoff);
            var facing = Quaternion.Euler(0f, WallMountedPose.YawDegrees(normal), 0f);

            if ((seated - element.transform.position).sqrMagnitude <= Tolerance.EpsilonSqr
                && Quaternion.Angle(facing, element.transform.rotation) <= RotationEpsilonDegrees)
                return;

            element.transform.SetPositionAndRotation(seated, facing);
        }
    }
}
