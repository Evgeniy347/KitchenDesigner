using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct WallCentreline
    {
        public const float CornerToleranceMm = 1f;

        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly Vector3 Direction;
        public readonly bool IsDefined;

        private WallCentreline(Vector3 start, Vector3 end, Vector3 direction)
        {
            Start = start;
            End = end;
            Direction = direction;
            IsDefined = true;
        }

        public static bool ThicknessAlongX(Vector3Int dimensionsMM) =>
            dimensionsMM.x <= dimensionsMM.z;

        public static int LengthMM(Vector3Int dimensionsMM) =>
            ThicknessAlongX(dimensionsMM) ? dimensionsMM.z : dimensionsMM.x;

        public static int ThicknessMM(Vector3Int dimensionsMM) =>
            ThicknessAlongX(dimensionsMM) ? dimensionsMM.x : dimensionsMM.z;

        public static WallCentreline Of(Vector3 center, Quaternion rotation, Vector3Int dimensionsMM)
        {
            int lengthMm = LengthMM(dimensionsMM);
            if (lengthMm <= 0) return default;

            Vector3 axis = rotation * (ThicknessAlongX(dimensionsMM) ? Vector3.forward : Vector3.right);
            axis.y = 0f;
            if (axis.sqrMagnitude < Tolerance.EpsilonSqr) return default;

            axis = axis.normalized;
            Vector3 half = axis * (lengthMm * AppConstants.MM_TO_UNITS * 0.5f);
            return new WallCentreline(center - half, center + half, axis);
        }

        public static bool MeetAtSharedCorner(in WallCentreline a, in WallCentreline b)
        {
            if (!a.IsDefined || !b.IsDefined) return false;
            if (Tolerance.IsParallel(Vector3.Dot(a.Direction, b.Direction))) return false;
            return a.EndsAt(b.Start) || a.EndsAt(b.End);
        }

        private bool EndsAt(Vector3 point) =>
            SamePointInPlan(Start, point) || SamePointInPlan(End, point);

        private static bool SamePointInPlan(Vector3 a, Vector3 b)
        {
            float limit = CornerToleranceMm * AppConstants.MM_TO_UNITS;
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz <= limit * limit;
        }
    }
}
