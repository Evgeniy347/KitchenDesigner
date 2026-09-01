using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Bulk
{
    public static class ModuleResize
    {
        public const float DefaultSpanFraction = 0.6f;

        public struct Change
        {
            public KitchenElement element;
            public Vector3 newPosition;
            public Vector3Int newDimensions;
        }

        public static List<Change> Plan(IList<KitchenElement> members, char worldAxis,
            float deltaMM, float spanFraction = DefaultSpanFraction)
        {
            var changes = new List<Change>();
            if (members == null || members.Count == 0) return changes;

            Vector3 axis = AxisVector(worldAxis);

            int deltaRoundedMM = Mathf.RoundToInt(deltaMM);
            float deltaU = deltaRoundedMM * AppConstants.MM_TO_UNITS;

            if (!ModuleSpanAlongWorldAxis(members, axis, out float moduleMin, out float moduleMax))
                return changes;

            float moduleWidth = moduleMax - moduleMin;
            float moduleMiddle = (moduleMin + moduleMax) * 0.5f;

            foreach (var e in members)
            {
                if (e == null) continue;
                SpanAlongWorldAxis(e, axis, out float emin, out float emax, out int localAxis);

                bool spansTheModule = emax - emin >= spanFraction * moduleWidth;
                float elementMiddle = (emin + emax) * 0.5f;
                if (spansTheModule)
                    changes.Add(StretchedKeepingItsNearEdge(e, axis, localAxis, deltaRoundedMM, deltaU));
                else if (elementMiddle > moduleMiddle)
                    changes.Add(ShiftedWhole(e, axis, deltaU));
            }
            return changes;
        }

        private static Change StretchedKeepingItsNearEdge(KitchenElement e, Vector3 axis, int localAxis,
            int deltaRoundedMM, float deltaU)
        {
            var dims = e.DimensionsMM;
            dims[localAxis] = Mathf.Max(1, dims[localAxis] + deltaRoundedMM);
            return new Change
            {
                element = e,
                newPosition = e.transform.position + axis * (deltaU * 0.5f),
                newDimensions = dims,
            };
        }

        private static Change ShiftedWhole(KitchenElement e, Vector3 axis, float deltaU) => new Change
        {
            element = e,
            newPosition = e.transform.position + axis * deltaU,
            newDimensions = e.DimensionsMM,
        };

        private static bool ModuleSpanAlongWorldAxis(IList<KitchenElement> members, Vector3 axis,
            out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (var e in members)
            {
                if (e == null) continue;
                SpanAlongWorldAxis(e, axis, out float emin, out float emax, out _);
                if (emin < min) min = emin;
                if (emax > max) max = emax;
            }
            return max > min;
        }

        private static Vector3 AxisVector(char worldAxis) => char.ToLowerInvariant(worldAxis) switch
        {
            'x' => Vector3.right,
            'y' => Vector3.up,
            _ => Vector3.forward,
        };

        private static void SpanAlongWorldAxis(KitchenElement e, Vector3 axis,
            out float min, out float max, out int localAxis)
        {
            var t = e.transform;
            localAxis = LocalAxisClosestTo(t, axis);

            float sizeU = e.DimensionsMM[localAxis] * AppConstants.MM_TO_UNITS;
            float center = Vector3.Dot(t.position, axis);
            min = center - sizeU * 0.5f;
            max = center + sizeU * 0.5f;
        }

        private static int LocalAxisClosestTo(Transform t, Vector3 axis)
        {
            float alongRight = Mathf.Abs(Vector3.Dot(axis, t.right));
            float alongUp = Mathf.Abs(Vector3.Dot(axis, t.up));
            float alongForward = Mathf.Abs(Vector3.Dot(axis, t.forward));
            return (alongRight >= alongUp && alongRight >= alongForward) ? 0
                : (alongUp >= alongForward ? 1 : 2);
        }
    }
}
