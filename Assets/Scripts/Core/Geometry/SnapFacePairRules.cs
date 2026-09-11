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

        public static bool TargetStillHasMaterialAbove(in ElementGeometry moved,
            in Face movedFace, in ElementGeometry other)
        {
            Vector3 n = moved.MountNormal;
            Vector3 centre = (other.Min + other.Max) * 0.5f;
            Vector3 half = (other.Max - other.Min) * 0.5f;
            float far = Vector3.Dot(centre, n)
                + Mathf.Abs(n.x) * half.x + Mathf.Abs(n.y) * half.y + Mathf.Abs(n.z) * half.z;
            return Vector3.Dot(movedFace.center, n) < far - Tolerance.EpsilonUnits;
        }

        public static bool LiesOnTheMountAxis(in ElementGeometry moved, in Face face) =>
            Mathf.Abs(Vector3.Dot(face.normal, moved.MountNormal)) >= Tolerance.ParallelDot;

        public static bool IsTheMountFace(in ElementGeometry moved, in Face face) =>
            moved.CentresOnTarget
            && Vector3.Dot(face.normal, moved.MountNormal) >= Tolerance.ParallelDot;
    }
}
