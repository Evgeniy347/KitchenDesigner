using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Handles;

namespace KitchenDesigner.Core.Measure
{
    public static class MeasureGeometry
    {
        public static float ToMm(float units) => units / AppConstants.MM_TO_UNITS;

        public static Vector3 ProjectOnDominantAxis(Vector3 anchor, Vector3 target)
        {
            Vector3 d = target - anchor;
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            if (ax >= ay && ax >= az) return anchor + new Vector3(d.x, 0f, 0f);
            if (ay >= az) return anchor + new Vector3(0f, d.y, 0f);
            return anchor + new Vector3(0f, 0f, d.z);
        }

        public static int AxisOf(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            bool sameX = Mathf.Abs(d.x) < Tolerance.EpsilonUnits;
            bool sameY = Mathf.Abs(d.y) < Tolerance.EpsilonUnits;
            bool sameZ = Mathf.Abs(d.z) < Tolerance.EpsilonUnits;

            if (sameY && sameZ && !sameX) return 0;
            if (sameX && sameZ && !sameY) return 1;
            if (sameX && sameY && !sameZ) return 2;
            return -1;
        }

        public static int NearestIndex(IReadOnlyList<Vector2> screenPoints, Vector2 mouse, float radiusPx)
        {
            int best = -1;
            float bestSqr = radiusPx * radiusPx;
            for (int i = 0; i < screenPoints.Count; i++)
            {
                float sqr = (screenPoints[i] - mouse).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        public static float DistancePointToSegmentPx(Vector2 a, Vector2 b, Vector2 p) =>
            ScreenDistance.PointToSegmentPixels(a, b, p);

        public static string FormatMm(float lengthUnits, bool axisAligned)
        {
            int mm = Mathf.RoundToInt(ToMm(lengthUnits));
            return axisAligned ? $"{mm} мм" : $"{UI.UIStyle.GlyphAngle} {mm} мм";
        }

        public static float WorldSizeForPixels(Camera camera, Vector3 worldPoint, float pixels)
        {
            if (camera == null) return 0f;
            var view = new PinholeView(
                camera.transform.position, camera.transform.forward,
                camera.transform.right, camera.transform.up,
                camera.fieldOfView, camera.pixelWidth, camera.pixelHeight,
                camera.orthographic, camera.orthographicSize);
            return view.WorldSizeForPixels(worldPoint, pixels);
        }
    }
}
