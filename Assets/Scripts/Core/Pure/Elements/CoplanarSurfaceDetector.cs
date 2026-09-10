using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CoplanarSurfaceDetector
    {
        public const float DEFAULT_MIN_SEPARATION_MM = 0.5f;
        public const float AREA_EPS_MM = 0.01f;

        public static (Vector3 centerMM, Vector3 sizeMM) FromWorldBounds(Bounds worldBounds) =>
            (worldBounds.center / AppConstants.MM_TO_UNITS, worldBounds.size / AppConstants.MM_TO_UNITS);

        public static List<string> Fights(
            (Vector3 centerMM, Vector3 sizeMM)[] parts, Func<int, string> nameOf,
            float minSeparationMM = DEFAULT_MIN_SEPARATION_MM)
        {
            var found = new List<string>();
            for (int i = 0; i < parts.Length; i++)
                for (int j = i + 1; j < parts.Length; j++)
                    for (int axis = 0; axis < 3; axis++)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float pi = Plane(parts[i], axis, side);
                            float pj = Plane(parts[j], axis, side);
                            if (Mathf.Abs(pi - pj) >= minSeparationMM) continue;
                            if (!OverlapsAcross(parts[i], parts[j], axis)) continue;

                            found.Add(nameOf(i) + " и " + nameOf(j) + ": грани по "
                                + "XYZ"[axis] + (side > 0 ? "+" : "-") + " на "
                                + pi.ToString("0.###") + " и " + pj.ToString("0.###")
                                + " мм — расходятся на " + Mathf.Abs(pi - pj).ToString("0.###"));
                        }
            return found;
        }

        private static float Plane((Vector3 centerMM, Vector3 sizeMM) part, int axis, int side) =>
            part.centerMM[axis] + side * part.sizeMM[axis] * 0.5f;

        private static bool OverlapsAcross(
            (Vector3 centerMM, Vector3 sizeMM) a, (Vector3 centerMM, Vector3 sizeMM) b, int axis)
        {
            for (int k = 0; k < 3; k++)
            {
                if (k == axis) continue;
                float lo = Mathf.Max(a.centerMM[k] - a.sizeMM[k] * 0.5f,
                    b.centerMM[k] - b.sizeMM[k] * 0.5f);
                float hi = Mathf.Min(a.centerMM[k] + a.sizeMM[k] * 0.5f,
                    b.centerMM[k] + b.sizeMM[k] * 0.5f);
                if (hi - lo <= AREA_EPS_MM) return false;
            }
            return true;
        }
    }
}
