using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapNeighbourFacts
    {
        public bool hasFacingFaces;
        public bool hasCandidateFaces;
        public SnapPairRejection exclusion;
        public SnapPairRejection rejection;
        public SnapPairRole role;
        public float bestDot;
        public int movedFaceIndex;
        public int otherFaceIndex;
        public float distanceUnits;
        public float overlapRatio;
        public bool withinThreshold;
        public bool overlapEnough;
        public bool wouldSnap;

        public static SnapNeighbourFacts Of(in ElementGeometry moved, Vector3 basePos,
            in ElementGeometry other, float maxDist)
        {
            var facts = new SnapNeighbourFacts
            {
                bestDot = 1f,
                movedFaceIndex = -1,
                otherFaceIndex = -1,
                distanceUnits = -1f,
                overlapRatio = -1f,
                role = SnapPairRole.NotACandidate,
            };
            if (moved.IsEmpty || other.IsEmpty) return facts;

            Face[] seatFaces = moved.IsPanel ? other.GrooveSeatFaces : System.Array.Empty<Face>();
            Face[] wallFaces = other.GrooveWallFaces;

            int bestRank = int.MaxValue;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Face.BoxFaceCount; i++)
            {
                for (int j = 0; j < Face.BoxFaceCount + seatFaces.Length; j++)
                {
                    bool isGrooveSeat = j >= Face.BoxFaceCount;
                    Face of = isGrooveSeat ? seatFaces[j - Face.BoxFaceCount] : other.Faces[j];
                    Face mf = moved.Faces[i];

                    float dot = Vector3.Dot(mf.normal, of.normal);
                    if (dot < facts.bestDot) facts.bestDot = dot;
                    bool opposed = Tolerance.IsParallel(dot) && dot < 0f;
                    if (opposed) facts.hasFacingFaces = true;

                    var offer = SnapPairOffer.For(moved, basePos, other, mf, of, isGrooveSeat,
                        seatFaces, wallFaces, maxDist);

                    if (offer.role == SnapPairRole.NotACandidate)
                    {
                        if (opposed && facts.exclusion == SnapPairRejection.None)
                            facts.exclusion = offer.rejection;
                        continue;
                    }

                    facts.hasCandidateFaces = true;

                    bool offers = offer.accepted
                        || (offer.role == SnapPairRole.Flush && offer.alreadyInPlace);
                    int rank = offers ? 0 : (offer.hasOverlap ? 1 : 2);

                    float distance = offer.role == SnapPairRole.Centring
                        && offer.rejection != SnapPairRejection.BeyondThreshold
                        ? offer.dist
                        : offer.planeDist;

                    if (rank > bestRank || (rank == bestRank && distance >= bestDistance)) continue;

                    bestRank = rank;
                    bestDistance = distance;
                    facts.role = offer.role;
                    facts.rejection = offer.rejection;
                    facts.movedFaceIndex = i;
                    facts.otherFaceIndex = j;
                    facts.distanceUnits = distance;
                    facts.overlapRatio = offer.overlapRatio;
                    facts.withinThreshold = offer.rejection != SnapPairRejection.BeyondThreshold;
                    facts.overlapEnough = offer.hasOverlap
                        && offer.overlapRatio >= Tolerance.MinSupportOverlap;
                    facts.wouldSnap = offers;
                }
            }

            return facts;
        }
    }
}
