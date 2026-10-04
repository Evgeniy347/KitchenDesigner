using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class EqualGapSnap
    {
        public const int AxisX = 1;
        public const int AxisY = 2;
        public const int AxisZ = 4;
        public const int AllAxes = AxisX | AxisY | AxisZ;

        public static int MaskOf(int axis) => 1 << axis;

        public static bool HasExistingGap(in AxisCast cast) =>
            cast.HasNear && cast.HasFar && cast.FarGap > Tolerance.ContactUnits;

        public static bool GapsAreEqual(in AxisCast cast) =>
            HasExistingGap(cast) && Mathf.Abs(cast.NearGap - cast.FarGap) <= Tolerance.ContactUnits;

        public static bool TryAlong(in AxisCast cast, float threshold, out float towardNear)
        {
            towardNear = 0f;
            if (!HasExistingGap(cast)) return false;

            float difference = cast.NearGap - cast.FarGap;
            if (Mathf.Abs(difference) > threshold + Tolerance.SnapEpsilon) return false;
            towardNear = difference;
            return true;
        }

        public static bool TryShift(AxisGuideIndex index, in AxisBox moving, int axis,
            float threshold, out float shift)
        {
            shift = 0f;
            bool found = false;
            foreach (int sign in DistanceGuides.Signs)
            {
                var cast = DistanceGuides.Cast(index, moving, axis, sign);
                if (!TryAlong(cast, threshold, out float towardNear)) continue;
                float candidate = towardNear * sign;
                if (found && Mathf.Abs(candidate) >= Mathf.Abs(shift)) continue;
                shift = candidate;
                found = true;
            }
            return found;
        }

        public static Vector3 Resolve(AxisGuideIndex index, in AxisBox movingAtFree, Vector3 free,
            bool faceSnapped, Vector3 faceSnappedPosition, int axisMask, float threshold,
            out int pulledAxes)
        {
            pulledAxes = 0;
            var result = faceSnapped ? faceSnappedPosition : free;

            for (int axis = 0; axis < 3; axis++)
            {
                if ((axisMask & MaskOf(axis)) == 0) continue;

                var line = result;
                line[axis] = free[axis];
                var moving = movingAtFree.Moved(line - free);
                if (!TryShift(index, moving, axis, threshold, out float shift)) continue;

                float faceShift = result[axis] - free[axis];
                bool faceSnapMovedThisAxis = Mathf.Abs(faceShift) > Tolerance.EpsilonUnits;
                if (faceSnapMovedThisAxis && Mathf.Abs(faceShift) <= Mathf.Abs(shift)) continue;

                result[axis] = free[axis] + shift;
                pulledAxes |= MaskOf(axis);
            }
            return result;
        }

        public static bool TryForFace(AxisGuideIndex index, Vector3 lineOrigin, Vector3 normal,
            Vector3 faceCentre, float threshold, out float alongNormal)
        {
            alongNormal = 0f;
            int axis = DominantAxis(normal);
            if (Mathf.Abs(normal[axis]) < Tolerance.ParallelDot) return false;

            int sign = normal[axis] > 0f ? 1 : -1;
            float start = (faceCentre[axis] - lineOrigin[axis]) * sign;
            var cast = index.Cast(lineOrigin, axis, sign, start);
            return TryAlong(cast, threshold, out alongNormal);
        }

        public static int DominantAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return 0;
            return ay >= az ? 1 : 2;
        }
    }
}
