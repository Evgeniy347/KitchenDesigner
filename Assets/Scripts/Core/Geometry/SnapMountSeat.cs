using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SnapMountSeat
    {
        public static void Deltas(in ElementGeometry moved, in Face movedFace, in Face otherFace,
            float maxDist, out float du, out float dv, out string labelU, out string labelV)
        {
            Vector3 u = movedFace.rightAxis;
            Vector3 v = movedFace.upAxis;
            Rect mRect = FaceRects.Of(movedFace, u, v);
            Rect oRect = FaceRects.Of(otherFace, u, v);
            du = Detent(moved, mRect.xMin, mRect.xMax, oRect.xMin, oRect.xMax, maxDist, out labelU);
            dv = Detent(moved, mRect.yMin, mRect.yMax, oRect.yMin, oRect.yMax, maxDist, out labelV);
        }

        private static float Detent(in ElementGeometry moved, float aMin, float aMax,
            float bMin, float bMax, float maxDist, out string label)
        {
            if (bMax - bMin < aMax - aMin + Tolerance.EpsilonUnits)
            {
                label = EdgeDetents.CentreLabel;
                return EdgeDetents.CentreDelta(aMin, aMax, bMin, bMax, maxDist);
            }

            return EdgeDetents.MountDetentDelta(aMin, aMax, bMin, bMax, maxDist,
                moved.MountEdgeDetentUnits, out label);
        }
    }
}
