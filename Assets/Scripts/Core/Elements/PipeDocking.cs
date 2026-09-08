using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class PipeDocking
    {
        public const float RotationEpsilonDegrees = WallSeating.RotationEpsilonDegrees;

        public static void Seat(KitchenElement element, Vector3 poseOrigin,
            Quaternion poseRotation, IReadOnlyList<KitchenElement> scene, in SnapCursor cursor)
        {
            if (element == null || scene == null) return;

            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.SnapEnabled) return;

            var moved = element.ToPortedPart(element.transform.position);
            if (!moved.HasPorts) return;

            float maxDist = settings.SnapThreshold * AppConstants.MM_TO_UNITS
                            + Tolerance.SnapEpsilon;

            var dock = SnapPortDock.Best(moved, scene.ToPortedParts(), maxDist, cursor);
            if (!dock.taken) return;

            var turn = Quaternion.AngleAxis(dock.rotationDegrees, dock.rotationAxis);
            var rotation = turn * poseRotation;
            var seatedOrigin = dock.targetMouthUnits - turn * (dock.movedMouthUnits - poseOrigin);
            var position = element.transform.position + (seatedOrigin - poseOrigin);

            if ((position - element.transform.position).sqrMagnitude <= Tolerance.EpsilonSqr
                && Quaternion.Angle(rotation, element.transform.rotation)
                   <= RotationEpsilonDegrees)
                return;

            element.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
