using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapPortSeat
    {
        public bool bothSidesCarryPorts;
        public bool taken;
        public bool alreadySeated;
        public bool opposed;
        public int movedPort;
        public int otherPort;
        public Vector3 delta;
        public float mouthGapUnits;
        public Vector3 mouthUnits;
        public Vector3 targetMouthUnits;
        public string targetName;

        public float ShiftUnits => taken ? delta.magnitude : -1f;

        public static bool FaceEachOther(in SnapPort a, in SnapPort b) =>
            Vector3.Dot(a.Outward, b.Outward) <= -Tolerance.ParallelDot;

        public static SnapPortSeat Empty() => new SnapPortSeat
        {
            movedPort = -1,
            otherPort = -1,
            mouthGapUnits = -1f,
            targetName = string.Empty,
        };

        public static SnapPortSeat Best(in ElementGeometry moved, in ElementGeometry other,
            float maxDist)
        {
            var seat = Empty();
            seat.targetName = other.Name;

            if (!moved.HasPorts || !other.HasPorts) return seat;

            var mine = moved.Ports;
            var theirs = other.Ports;
            seat.bothSidesCarryPorts = true;

            int bestRank = int.MaxValue;
            float bestGap = float.MaxValue;
            float nearest = float.MaxValue;

            for (int i = 0; i < mine.Length; i++)
            {
                for (int j = 0; j < theirs.Length; j++)
                {
                    Vector3 d = theirs[j].Position - mine[i].Position;
                    float gap = d.magnitude;
                    if (gap < nearest) nearest = gap;
                    if (gap > maxDist) continue;

                    int rank = FaceEachOther(mine[i], theirs[j]) ? 0 : 1;
                    if (rank > bestRank || (rank == bestRank && gap >= bestGap)) continue;

                    bestRank = rank;
                    bestGap = gap;
                    seat.taken = true;
                    seat.opposed = rank == 0;
                    seat.movedPort = i;
                    seat.otherPort = j;
                    seat.delta = d;
                    seat.mouthUnits = mine[i].Position;
                    seat.targetMouthUnits = theirs[j].Position;
                }
            }

            seat.mouthGapUnits = nearest == float.MaxValue ? -1f : nearest;
            seat.alreadySeated = seat.taken && bestGap <= SnapPairOffer.ZeroShiftEpsilon;
            return seat;
        }

        public static SnapPortSeat BestOf(in ElementGeometry moved,
            IReadOnlyList<ElementGeometry> others, float maxDist)
        {
            var best = Empty();
            if (moved.IsEmpty || others == null || !moved.HasPorts) return best;

            int bestRank = int.MaxValue;
            float bestGap = float.MaxValue;
            float nearest = float.MaxValue;

            foreach (var other in others)
            {
                if (other.IsEmpty || other.Id == moved.Id) continue;

                var seat = Best(moved, other, maxDist);
                if (!seat.bothSidesCarryPorts) continue;

                best.bothSidesCarryPorts = true;
                if (seat.mouthGapUnits >= 0f && seat.mouthGapUnits < nearest)
                    nearest = seat.mouthGapUnits;
                if (!seat.taken) continue;

                int rank = seat.opposed ? 0 : 1;
                float gap = seat.delta.magnitude;
                if (rank > bestRank || (rank == bestRank && gap >= bestGap)) continue;

                bestRank = rank;
                bestGap = gap;
                bool carried = best.bothSidesCarryPorts;
                best = seat;
                best.bothSidesCarryPorts = carried;
            }

            if (nearest != float.MaxValue) best.mouthGapUnits = nearest;
            return best;
        }

        public static bool TryAlongNormal(in ElementGeometry self, Vector3 faceCenter,
            Vector3 normal, IReadOnlyList<ElementGeometry> others, float maxDist, out float gap)
        {
            gap = 0f;
            if (others == null || !CarriesAMouth(self, faceCenter, normal)) return false;

            float lateralLimit = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            int bestRank = int.MaxValue;
            float bestAbs = float.MaxValue;
            bool found = false;

            foreach (var other in others)
            {
                if (other.IsEmpty || other.Id == self.Id) continue;
                if (ReferenceEquals(other.Faces, self.Faces)) continue;
                var theirs = other.Ports;
                if (theirs == null) continue;

                foreach (var mouth in theirs)
                {
                    Vector3 d = mouth.Position - faceCenter;
                    float along = Vector3.Dot(d, normal);
                    if (Mathf.Abs(along) > maxDist) continue;
                    if ((d - along * normal).magnitude > lateralLimit) continue;

                    int rank = Vector3.Dot(mouth.Outward, normal) <= -Tolerance.ParallelDot ? 0 : 1;
                    float abs = Mathf.Abs(along);
                    if (rank > bestRank || (rank == bestRank && abs >= bestAbs)) continue;

                    bestRank = rank;
                    bestAbs = abs;
                    gap = along;
                    found = true;
                }
            }

            return found;
        }

        private static bool CarriesAMouth(in ElementGeometry self, Vector3 faceCenter,
            Vector3 normal)
        {
            var mine = self.Ports;
            if (mine == null) return false;

            foreach (var port in mine)
            {
                if (Vector3.Dot(port.Outward, normal) < Tolerance.ParallelDot) continue;
                Vector3 d = port.Position - faceCenter;
                if ((d - Vector3.Dot(d, normal) * normal).magnitude <= Tolerance.EpsilonUnits)
                    return true;
            }

            return false;
        }
    }
}
