using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapPairOffer
    {
        public const float ZeroShiftEpsilon = Tolerance.EpsilonUnits;

        public SnapPairRole role;
        public SnapPairRejection rejection;
        public bool accepted;
        public bool alreadyInPlace;
        public float planeDist;
        public float planeShift;
        public float shift;
        public bool hasOverlap;
        public float overlapRatio;
        public bool hasLineContact;
        public Vector3 u;
        public Vector3 v;
        public float du;
        public float dv;
        public string detentU;
        public string detentV;
        public Vector3 snapPos;
        public float dist;

        public static SnapPairOffer For(in ElementGeometry moved, Vector3 basePos,
            in ElementGeometry other, in Face movedFace, in Face otherFace,
            bool isGrooveSeat, Face[] seatFaces, Face[] grooveWallFaces, float maxDist)
        {
            var offer = new SnapPairOffer
            {
                u = movedFace.rightAxis,
                v = movedFace.upAxis,
                snapPos = basePos,
                detentU = EdgeDetents.FreeLabel,
                detentV = EdgeDetents.FreeLabel,
            };

            SnapPairRole role = SnapFacePairRules.RoleOf(moved, movedFace, otherFace, isGrooveSeat,
                seatFaces, out SnapPairRejection why);
            offer.role = role;
            offer.rejection = why;
            if (role == SnapPairRole.NotACandidate) return offer;

            offer.planeShift = Vector3.Dot(otherFace.center - movedFace.center, movedFace.normal);
            offer.planeDist = Mathf.Abs(offer.planeShift);
            if (offer.planeDist > maxDist)
            {
                offer.rejection = SnapPairRejection.BeyondThreshold;
                return offer;
            }

            bool hasOverlap = FaceContacts.OverlapAllowingEdgeTouch(movedFace, otherFace,
                out float overlapRatio, out bool hasLineContact);
            offer.hasOverlap = hasOverlap;
            offer.overlapRatio = hasOverlap ? overlapRatio : 0f;
            offer.hasLineContact = hasLineContact;
            if (!hasOverlap)
            {
                offer.rejection = SnapPairRejection.NoOverlap;
                return offer;
            }
            if (overlapRatio < Tolerance.MinSupportOverlap)
            {
                offer.rejection = SnapPairRejection.OverlapTooSmall;
                return offer;
            }

            if (role == SnapPairRole.Centring)
            {
                SnapMountSeat.Deltas(moved, movedFace, otherFace, maxDist,
                    out offer.du, out offer.dv, out offer.detentU, out offer.detentV);
                offer.Land(basePos, movedFace.normal);
                if (offer.dist <= ZeroShiftEpsilon)
                {
                    offer.alreadyInPlace = true;
                    offer.rejection = SnapPairRejection.AlreadyInPlace;
                    return offer;
                }
            }
            else if (role == SnapPairRole.FarEdgeAlignment)
            {
                offer.shift = offer.planeShift;
                offer.Land(basePos, movedFace.normal);
                if (offer.planeDist <= ZeroShiftEpsilon)
                {
                    offer.alreadyInPlace = true;
                    offer.rejection = SnapPairRejection.AlreadyInPlace;
                    return offer;
                }
                if (WouldLandInsideNeighbour(moved, offer.shift * movedFace.normal, other))
                {
                    offer.rejection = SnapPairRejection.LandsInsideNeighbour;
                    return offer;
                }
            }
            else
            {
                Rect mRect = FaceRects.Of(movedFace, offer.u, offer.v);
                Rect oRect = FaceRects.Of(otherFace, offer.u, offer.v);
                offer.du = EdgeDetents.NearestDetentDelta(mRect.xMin, mRect.xMax,
                    oRect.xMin, oRect.xMax, maxDist,
                    EdgeDetents.GrooveWallCoordsAlong(grooveWallFaces, offer.u), out offer.detentU);
                offer.dv = EdgeDetents.NearestDetentDelta(mRect.yMin, mRect.yMax,
                    oRect.yMin, oRect.yMax, maxDist,
                    EdgeDetents.GrooveWallCoordsAlong(grooveWallFaces, offer.v), out offer.detentV);
                offer.shift = offer.planeShift;
                offer.Land(basePos, movedFace.normal);

                if (!isGrooveSeat && CentreWouldLandInsideNeighbour(offer.snapPos, other))
                {
                    offer.rejection = SnapPairRejection.LandsInsideNeighbour;
                    return offer;
                }

                offer.alreadyInPlace = offer.planeDist <= ZeroShiftEpsilon;
                if (offer.dist <= ZeroShiftEpsilon)
                {
                    offer.rejection = SnapPairRejection.AlreadyInPlace;
                    return offer;
                }
            }

            offer.accepted = true;
            return offer;
        }

        private void Land(Vector3 basePos, Vector3 normal)
        {
            snapPos = basePos + shift * normal + du * u + dv * v;
            dist = Vector3.Distance(snapPos, basePos);
        }

        private static bool WouldLandInsideNeighbour(in ElementGeometry moved, Vector3 shift,
            in ElementGeometry other) =>
            Tolerance.IntervalsOverlap(moved.Min.x + shift.x, moved.Max.x + shift.x, other.Min.x, other.Max.x) &&
            Tolerance.IntervalsOverlap(moved.Min.y + shift.y, moved.Max.y + shift.y, other.Min.y, other.Max.y) &&
            Tolerance.IntervalsOverlap(moved.Min.z + shift.z, moved.Max.z + shift.z, other.Min.z, other.Max.z);

        private static bool CentreWouldLandInsideNeighbour(Vector3 snapPos, in ElementGeometry other)
        {
            foreach (var f in other.Faces)
                if (Vector3.Dot(snapPos - f.center, f.normal) >= 0f) return false;
            return true;
        }
    }
}
