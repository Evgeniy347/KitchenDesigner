using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    internal static class DashedLineDrawer
    {
        public const float DashLengthPx = 9f;
        public const float DashGapPx = 6f;

        public static void Dashed(Camera cam, Vector3 a, Vector3 b, float thicknessPx)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < Tolerance.EpsilonUnits) return;
            Vector3 dir = delta / length;

            float worldPerPixel = MeasureGeometry.WorldSizeForPixels(cam, (a + b) * 0.5f, 1f);
            float dash = DashLengthPx * worldPerPixel;
            float step = (DashLengthPx + DashGapPx) * worldPerPixel;
            if (step < Tolerance.EpsilonUnits) return;

            for (float t = 0f; t < length; t += step)
            {
                Vector3 p0 = a + dir * t;
                Vector3 p1 = a + dir * Mathf.Min(t + dash, length);
                Strip(cam, p0, p1, thicknessPx);
            }
        }

        public static void Strip(Camera cam, Vector3 a, Vector3 b, float thicknessPx)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < Tolerance.EpsilonSqr) return;

            Vector3 side = Vector3.Cross(axis.normalized, cam.transform.forward);
            if (side.sqrMagnitude < Tolerance.EpsilonSqr) return;
            side.Normalize();

            float ha = MeasureGeometry.WorldSizeForPixels(cam, a, thicknessPx * 0.5f);
            float hb = MeasureGeometry.WorldSizeForPixels(cam, b, thicknessPx * 0.5f);

            GL.Vertex(a - side * ha);
            GL.Vertex(a + side * ha);
            GL.Vertex(b + side * hb);
            GL.Vertex(b - side * hb);
        }

        public static void Point(Camera cam, Vector3 p, float radiusPx)
        {
            float r = MeasureGeometry.WorldSizeForPixels(cam, p, radiusPx);
            Vector3 right = cam.transform.right * r;
            Vector3 up = cam.transform.up * r;

            GL.Vertex(p - right - up);
            GL.Vertex(p - right + up);
            GL.Vertex(p + right + up);
            GL.Vertex(p + right - up);
        }
    }
}
