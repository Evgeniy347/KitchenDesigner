using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ResizeMath
    {
        public static void Compute(
            Vector3Int dimsBefore, int axisIndex,
            Vector3 normal, Vector3 faceCenter0, Vector3 uAxis, Vector3 vAxis, Vector2 faceSize,
            Vector3 centerStart, float sizeStartUnits,
            float rawDelta, IReadOnlyList<ElementGeometry> others, in ElementGeometry self,
            bool snapEnabled, float threshold,
            out Vector3Int newDims, out Vector3 newCenter, out bool snapped)
        {
            snapped = false;
            float finalDelta = rawDelta;

            if (snapEnabled)
            {
                Vector3 faceCenterAfterRawDelta = faceCenter0 + normal * rawDelta;
                if (ResizeSnap.SnapDelta(faceCenterAfterRawDelta, normal, uAxis, vAxis, faceSize,
                        others, self, threshold, out float gap))
                {
                    finalDelta = rawDelta + gap;
                    snapped = true;
                }
            }

            float newSizeUnits = sizeStartUnits + finalDelta;
            int newDimMM = Mathf.Max(1, Mathf.RoundToInt(newSizeUnits / AppConstants.MM_TO_UNITS));

            float deltaRoundedToMm = newDimMM * AppConstants.MM_TO_UNITS - sizeStartUnits;

            if (snapped && deltaRoundedToMm - finalDelta > Tolerance.EpsilonUnits * 0.5f)
            {
                newDimMM = Mathf.Max(1, newDimMM - 1);
                deltaRoundedToMm = newDimMM * AppConstants.MM_TO_UNITS - sizeStartUnits;
            }

            newDims = dimsBefore;
            if (axisIndex == 0) newDims.x = newDimMM;
            else if (axisIndex == 1) newDims.y = newDimMM;
            else newDims.z = newDimMM;

            newCenter = centerStart + normal * (deltaRoundedToMm * 0.5f);
        }

        public static int DimAlong(Vector3Int dims, int axisIndex) =>
            axisIndex == 0 ? dims.x : (axisIndex == 1 ? dims.y : dims.z);

        public static Vector3 CenterForAppliedDims(Vector3 centerStart, Vector3 normal,
            float sizeStartUnits, Vector3Int appliedDims, int axisIndex)
        {
            float appliedDelta = DimAlong(appliedDims, axisIndex) * AppConstants.MM_TO_UNITS - sizeStartUnits;
            return centerStart + normal * (appliedDelta * 0.5f);
        }
    }
}
