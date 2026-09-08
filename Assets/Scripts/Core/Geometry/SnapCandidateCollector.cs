using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SnapCandidateCollector
    {
        public const float ZeroShiftEpsilon = SnapPairOffer.ZeroShiftEpsilon;

        private readonly struct MovedPart
        {
            public readonly ElementGeometry Geometry;
            public readonly Vector3 BasePos;
            public readonly float MaxDist;
            public readonly bool Verbose;
            public readonly bool IsPrimaryPass;

            public MovedPart(ElementGeometry geometry, Vector3 basePos, float maxDist,
                bool verbose, bool isPrimaryPass)
            {
                Geometry = geometry;
                BasePos = basePos;
                MaxDist = maxDist;
                Verbose = verbose;
                IsPrimaryPass = isPrimaryPass;
            }

            public Face[] Faces => Geometry.Faces;
        }

        public static void Collect(IPosedGeometry moved, IReadOnlyList<ElementGeometry> others,
            Vector3 basePos, float maxDist, bool verbose, bool isPrimaryPass, SnapCandidates into)
            => Collect(moved.At(basePos), others, basePos, maxDist, verbose, isPrimaryPass, into);

        public static void Collect(in ElementGeometry moved, IReadOnlyList<ElementGeometry> others,
            Vector3 basePos, float maxDist, bool verbose, bool isPrimaryPass, SnapCandidates into)
        {
            var part = new MovedPart(moved, basePos, maxDist, verbose, isPrimaryPass);

            foreach (var other in others)
            {
                if (other.IsEmpty || other.Id == part.Geometry.Id) continue;
                CollectAgainst(part, other, into);
            }
        }

        private static void CollectAgainst(in MovedPart part, in ElementGeometry other,
            SnapCandidates into)
        {
            Face[] seatFaces = part.Geometry.IsPanel
                ? other.GrooveSeatFaces
                : System.Array.Empty<Face>();
            Face[] wallFaces = other.GrooveWallFaces;

            for (int i = 0; i < Face.BoxFaceCount; i++)
            {
                for (int j = 0; j < Face.BoxFaceCount + seatFaces.Length; j++)
                {
                    bool isGrooveSeat = j >= Face.BoxFaceCount;
                    Face of = isGrooveSeat ? seatFaces[j - Face.BoxFaceCount] : other.Faces[j];
                    Face mf = part.Faces[i];

                    var offer = SnapPairOffer.ForSelection(part.Geometry, part.BasePos, other,
                        mf, of, isGrooveSeat, seatFaces, wallFaces, part.MaxDist);
                    if (offer.role == SnapPairRole.NotACandidate) continue;

                    if (offer.role == SnapPairRole.FarEdgeAlignment && offer.alreadyInPlace)
                    {
                        if (part.IsPrimaryPass) into.AlreadyAlignedNormals.Add(mf.normal);
                        continue;
                    }

                    if (offer.rejection == SnapPairRejection.LandsInsideNeighbour) continue;

                    if (offer.role == SnapPairRole.Flush && offer.alreadyInPlace)
                        RecordExistingFlushContact(part, Offered(other, offer, mf, of, j),
                            LogFor(part, other, offer, i, j), mf.normal, into);

                    if (!offer.accepted) continue;

                    into.Candidates.Add(new SnapCandidate
                    {
                        dist = offer.dist,
                        result = Offered(other, offer, mf, of, j),
                        normal = mf.normal,
                        planeShift = offer.shift,
                        u = offer.u,
                        v = offer.v,
                        du = offer.du,
                        dv = offer.dv,
                        hasLineContact = offer.hasLineContact,
                        log = LogFor(part, other, offer, i, j),
                    });
                }
            }
        }

        private static SnapResult Offered(in ElementGeometry other, in SnapPairOffer offer,
            in Face mf, in Face of, int j) => new SnapResult
            {
                snapped = true,
                position = offer.snapPos,
                targetName = other.Name,
                faceIndex = j,
                snapPoint = mf.center,
                targetPoint = of.center,
            };

        private static string? LogFor(in MovedPart part, in ElementGeometry other,
            in SnapPairOffer offer, int i, int j)
        {
            if (!part.Verbose) return null;

            string head = $"[Snap] {part.Geometry.Name} → {other.Name} | ";
            if (offer.role == SnapPairRole.Centring)
                return head + $"посадка под деталью m{i}/o{j} " +
                       $"оси[u:{offer.detentU} v:{offer.detentV}] " +
                       $"du={offer.du * 1000f:F2}мм dv={offer.dv * 1000f:F2}мм";

            if (offer.role == SnapPairRole.FarEdgeAlignment)
                return head + $"выравнивание по дальней кромке m{i}/o{j} " +
                       $"сдвиг={offer.shift * 1000f:F2}мм";

            return head + $"грань m{i}/o{j} зазор={offer.planeDist * 1000f:F2}мм " +
                   $"перекр={offer.overlapRatio:P0} " +
                   $"оси[u:{offer.detentU} v:{offer.detentV}] → " +
                   $"поз {offer.snapPos.x:F3},{offer.snapPos.y:F3},{offer.snapPos.z:F3}";
        }

        private static void RecordExistingFlushContact(in MovedPart part, SnapResult result,
            string? log, Vector3 normal, SnapCandidates into)
        {
            into.ZeroShiftNormals.Add(normal);
            if (!part.IsPrimaryPass) return;

            if (into.ConfirmedContact.snapped) return;

            var confirm = result;
            confirm.position = part.BasePos;
            into.ConfirmedContact = confirm;
            into.ConfirmedContactLog = log;
        }
    }
}
