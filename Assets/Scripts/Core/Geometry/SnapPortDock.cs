using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapPortDock
    {
        public bool bothSidesCarryPorts;
        public bool taken;
        public bool decidedByCursor;
        public int movedPort;
        public int otherPort;
        public int targetId;
        public string targetName;
        public float mouthGapUnits;
        public float cursorDistanceUnits;
        public Vector3 rotationAxis;
        public float rotationDegrees;
        public Vector3 movedMouthUnits;
        public Vector3 targetMouthUnits;

        public static SnapPortDock Empty() => new SnapPortDock
        {
            movedPort = -1,
            otherPort = -1,
            targetId = 0,
            targetName = string.Empty,
            mouthGapUnits = -1f,
            cursorDistanceUnits = -1f,
            rotationAxis = Vector3.up,
            rotationDegrees = -1f,
        };

        public static SnapPortDock For(in PortedPart moved, in PortedPart other, float maxDist,
            in SnapCursor cursor)
        {
            var dock = Empty();
            dock.targetId = other.Id;
            dock.targetName = other.Name;

            if (!moved.HasPorts || !other.HasPorts || moved.Id == other.Id) return dock;
            dock.bothSidesCarryPorts = true;

            var mine = moved.Ports;
            var theirs = other.Ports;

            float bestGap = float.MaxValue;
            float bestTurn = float.MaxValue;

            for (int i = 0; i < mine.Length; i++)
            {
                for (int j = 0; j < theirs.Length; j++)
                {
                    float gap = (theirs[j].Position - mine[i].Position).magnitude;
                    TurnOnto(mine[i].Outward, -theirs[j].Outward, out Vector3 axis, out float turn);

                    if (gap > bestGap + Tolerance.EpsilonUnits) continue;
                    if (gap > bestGap - Tolerance.EpsilonUnits && turn >= bestTurn) continue;

                    bestGap = gap;
                    bestTurn = turn;
                    dock.movedPort = i;
                    dock.otherPort = j;
                    dock.rotationAxis = axis;
                    dock.rotationDegrees = turn;
                    dock.movedMouthUnits = mine[i].Position;
                    dock.targetMouthUnits = theirs[j].Position;
                }
            }

            dock.mouthGapUnits = bestGap == float.MaxValue ? -1f : bestGap;
            dock.cursorDistanceUnits = dock.movedPort < 0
                ? -1f
                : cursor.DistanceTo(dock.targetMouthUnits);
            dock.taken = dock.movedPort >= 0 && bestGap <= maxDist;
            return dock;
        }

        public static SnapPortDock Best(in PortedPart moved, IReadOnlyList<PortedPart> others,
            float maxDist, in SnapCursor cursor)
        {
            var best = Empty();
            if (!moved.HasPorts || others == null) return best;

            float bestKey = float.MaxValue;
            float bestGap = float.MaxValue;
            float nearest = float.MaxValue;

            foreach (var other in others)
            {
                var dock = For(moved, other, maxDist, cursor);
                if (!dock.bothSidesCarryPorts) continue;

                best.bothSidesCarryPorts = true;
                if (dock.mouthGapUnits >= 0f && dock.mouthGapUnits < nearest)
                    nearest = dock.mouthGapUnits;
                if (!dock.taken) continue;

                float key = cursor.Present ? dock.cursorDistanceUnits : dock.mouthGapUnits;
                if (key > bestKey + Tolerance.EpsilonUnits) continue;
                if (key > bestKey - Tolerance.EpsilonUnits
                    && dock.mouthGapUnits >= bestGap) continue;

                bestKey = key;
                bestGap = dock.mouthGapUnits;
                dock.bothSidesCarryPorts = true;
                dock.decidedByCursor = cursor.Present;
                best = dock;
            }

            if (nearest != float.MaxValue) best.mouthGapUnits = nearest;
            return best;
        }

        public static void TurnOnto(Vector3 from, Vector3 to, out Vector3 axis,
            out float degrees)
        {
            Vector3 a = from.normalized;
            Vector3 b = to.normalized;
            float dot = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);

            if (dot >= Tolerance.ParallelDot)
            {
                axis = Vector3.up;
                degrees = 0f;
                return;
            }

            if (dot <= -Tolerance.ParallelDot)
            {
                axis = PerpendicularTo(a);
                degrees = 180f;
                return;
            }

            axis = Vector3.Cross(a, b).normalized;
            degrees = Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        private static Vector3 PerpendicularTo(Vector3 direction)
        {
            Vector3 upright = Vector3.up - Vector3.Dot(direction, Vector3.up) * direction;
            if (upright.sqrMagnitude > Tolerance.EpsilonSqr) return upright.normalized;

            Vector3 sideways = Vector3.right - Vector3.Dot(direction, Vector3.right) * direction;
            return sideways.normalized;
        }
    }
}
