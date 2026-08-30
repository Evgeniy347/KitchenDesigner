using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SnapCandidateCollector
    {
        public const float ZeroShiftEpsilon = Tolerance.EpsilonUnits;

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
        {
            var part = new MovedPart(moved.At(basePos), basePos, maxDist, verbose, isPrimaryPass);

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
                    bool isGroove = j >= Face.BoxFaceCount;
                    Face of = isGroove ? seatFaces[j - Face.BoxFaceCount] : other.Faces[j];
                    Face mf = part.Faces[i];

                    if (!isGroove && GrooveSeating.SeatSupersedesFace(mf, of, seatFaces)) continue;

                    float dot = Vector3.Dot(mf.normal, of.normal);
                    if (!Tolerance.IsParallel(dot)) continue;

                    bool coDirectional = dot > 0;
                    if (isGroove && coDirectional) continue;

                    Vector3 offset = of.center - mf.center;
                    float planeDist = Mathf.Abs(Vector3.Dot(offset, mf.normal));
                    if (planeDist > part.MaxDist) continue;

                    if (!FaceContacts.OverlapAllowingEdgeTouch(mf, of,
                            out float overlapRatio, out bool hasLineContact)) continue;
                    if (overlapRatio < Tolerance.MinSupportOverlap) continue;

                    float planeShift = Vector3.Dot(offset, mf.normal);

                    if (coDirectional)
                        AddFarEdgeAlignment(part, other, mf, of, i, j, planeShift, hasLineContact, into);
                    else
                        AddFlushContact(part, other, wallFaces, mf, of, i, j, isGroove,
                            planeDist, planeShift, overlapRatio, hasLineContact, into);
                }
            }
        }

        private static void AddFarEdgeAlignment(in MovedPart part, in ElementGeometry other,
            in Face mf, in Face of, int i, int j, float planeShift, bool hasLineContact,
            SnapCandidates into)
        {
            if (Mathf.Abs(planeShift) <= ZeroShiftEpsilon)
            {
                if (part.IsPrimaryPass) into.AlreadyAlignedNormals.Add(mf.normal);
                return;
            }

            Vector3 alignShift = planeShift * mf.normal;
            if (WouldLandInsideNeighbour(part.Geometry, alignShift, other)) return;

            into.Candidates.Add(new SnapCandidate
            {
                dist = Mathf.Abs(planeShift),
                result = new SnapResult
                {
                    snapped = true,
                    position = part.BasePos + alignShift,
                    targetName = other.Name,
                    faceIndex = j,
                    snapPoint = mf.center,
                    targetPoint = of.center
                },
                normal = mf.normal,
                planeShift = planeShift,
                u = mf.rightAxis,
                v = mf.upAxis,
                du = 0f,
                dv = 0f,
                hasLineContact = hasLineContact,
                log = part.Verbose
                    ? $"[Snap] {part.Geometry.Name} → {other.Name} | выравнивание " +
                      $"по дальней кромке m{i}/o{j} сдвиг={planeShift * 1000f:F2}мм"
                    : null
            });
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

        private static void AddFlushContact(in MovedPart part, in ElementGeometry other,
            Face[] wallFaces, in Face mf, in Face of, int i, int j, bool isGroove,
            float planeDist, float planeShift, float overlapRatio, bool hasLineContact,
            SnapCandidates into)
        {
            Vector3 u = mf.rightAxis;
            Vector3 v = mf.upAxis;
            Rect mRect = FaceRects.Of(mf, u, v);
            Rect oRect = FaceRects.Of(of, u, v);
            float du = EdgeDetents.NearestDetentDelta(mRect.xMin, mRect.xMax, oRect.xMin, oRect.xMax,
                part.MaxDist, EdgeDetents.GrooveWallCoordsAlong(wallFaces, u), out string labelU);
            float dv = EdgeDetents.NearestDetentDelta(mRect.yMin, mRect.yMax, oRect.yMin, oRect.yMax,
                part.MaxDist, EdgeDetents.GrooveWallCoordsAlong(wallFaces, v), out string labelV);

            Vector3 snapPos = part.BasePos + planeShift * mf.normal + du * u + dv * v;

            if (!isGroove && CentreWouldLandInsideNeighbour(snapPos, other)) return;

            float dist = Vector3.Distance(snapPos, part.BasePos);
            var result = new SnapResult
            {
                snapped = true,
                position = snapPos,
                targetName = other.Name,
                faceIndex = j,
                snapPoint = mf.center,
                targetPoint = of.center
            };
            string? log = part.Verbose
                ? $"[Snap] {part.Geometry.Name} → {other.Name} | грань m{i}/o{j} " +
                  $"зазор={planeDist * 1000f:F2}мм перекр={overlapRatio:P0} " +
                  $"оси[u:{labelU} v:{labelV}] → поз {snapPos.x:F3},{snapPos.y:F3},{snapPos.z:F3}"
                : null;

            if (Mathf.Abs(planeShift) <= ZeroShiftEpsilon)
                RecordExistingFlushContact(part, result, log, mf.normal, into);

            if (dist > ZeroShiftEpsilon)
            {
                into.Candidates.Add(new SnapCandidate
                {
                    dist = dist,
                    result = result,
                    normal = mf.normal,
                    planeShift = planeShift,
                    u = u,
                    v = v,
                    du = du,
                    dv = dv,
                    hasLineContact = hasLineContact,
                    log = log
                });
            }
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
