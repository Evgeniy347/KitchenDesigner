using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SnapFacePairRules
    {
        public static SnapPairRole RoleOf(in ElementGeometry moved, in Face movedFace,
            in Face otherFace, bool isGrooveSeat, Face[] seatFaces)
            => RoleOf(moved, movedFace, otherFace, isGrooveSeat, seatFaces, out _);

        public static SnapPairRole RoleOf(in ElementGeometry moved, in Face movedFace,
            in Face otherFace, bool isGrooveSeat, Face[] seatFaces, out SnapPairRejection why)
        {
            if (!isGrooveSeat && GrooveSeating.SeatSupersedesFace(movedFace, otherFace, seatFaces))
            {
                why = SnapPairRejection.SupersededByGrooveSeat;
                return SnapPairRole.NotACandidate;
            }

            if (moved.CentresOnTarget && !LiesOnTheMountAxis(moved, movedFace))
            {
                why = SnapPairRejection.OffTheMountAxis;
                return SnapPairRole.NotACandidate;
            }

            float dot = Vector3.Dot(movedFace.normal, otherFace.normal);
            if (!Tolerance.IsParallel(dot))
            {
                why = SnapPairRejection.NotParallel;
                return SnapPairRole.NotACandidate;
            }

            bool coDirectional = dot > 0f;
            if (coDirectional && isGrooveSeat)
            {
                why = SnapPairRejection.CoDirectionalGrooveSeat;
                return SnapPairRole.NotACandidate;
            }
            if (coDirectional && moved.CentresOnTarget)
            {
                why = SnapPairRejection.CoDirectionalMount;
                return SnapPairRole.NotACandidate;
            }

            why = SnapPairRejection.None;
            if (IsTheMountFace(moved, movedFace)) return SnapPairRole.Centring;
            return coDirectional ? SnapPairRole.FarEdgeAlignment : SnapPairRole.Flush;
        }

        public static bool LiesOnTheMountAxis(in ElementGeometry moved, in Face face) =>
            Mathf.Abs(Vector3.Dot(face.normal, moved.MountNormal)) >= Tolerance.ParallelDot;

        public static bool IsTheMountFace(in ElementGeometry moved, in Face face) =>
            moved.CentresOnTarget
            && Vector3.Dot(face.normal, moved.MountNormal) >= Tolerance.ParallelDot;
    }
}
