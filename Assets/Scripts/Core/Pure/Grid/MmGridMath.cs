using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MmGridMath
    {
        public const float EpsMm = 0.01f;

        public const float OffGridFindingToleranceMm = 0.05f;

        public static float RoundMm(float mm) => Mathf.Floor(mm + 0.5f + EpsMm);

        public static float ShiftToWholeMm(float mm) => RoundMm(mm) - mm;

        public static bool TryMeasureOffGrid(Vector3 minCornerMm, float toleranceMm,
            out Vector3 shiftsMm)
        {
            shiftsMm = Vector3.zero;
            bool any = false;
            for (int axis = 0; axis < 3; axis++)
            {
                float shift = ShiftToWholeMm(minCornerMm[axis]);
                if (Mathf.Abs(shift) <= toleranceMm) continue;
                shiftsMm[axis] = shift;
                any = true;
            }
            return any;
        }
    }
}
