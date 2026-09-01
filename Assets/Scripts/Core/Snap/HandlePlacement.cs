using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Геометрия размещения ручек (см. HandlePlacementTests).</summary>
    public static class HandlePlacement
    {
        public const float MinPlateAspectRatio = 3f;

        public readonly struct Box
        {
            public readonly Vector3 Center;
            public readonly Vector3 AxisX, AxisY, AxisZ;
            public readonly Vector3 Half;

            public Box(Vector3 center, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 half)
            {
                Center = center; AxisX = axisX; AxisY = axisY; AxisZ = axisZ; Half = half;
            }

            public Vector3 Axis(int index) => index == 0 ? AxisX : (index == 1 ? AxisY : AxisZ);

            public float DistanceTo(Vector3 world)
            {
                Vector3 d = world - Center;
                float sqr = 0f;
                for (int a = 0; a < 3; a++)
                {
                    float outside = Mathf.Abs(Vector3.Dot(d, Axis(a))) - Half[a];
                    if (outside > 0f) sqr += outside * outside;
                }
                return Mathf.Sqrt(sqr);
            }
        }

        public static Box BoxOf(Face[] faces)
        {
            Vector3 ax = Normal(faces, 0, Vector3.right);
            Vector3 ay = Normal(faces, 2, Vector3.up);
            Vector3 az = Normal(faces, 4, Vector3.forward);
            Vector3 center = (faces[0].center + faces[1].center) * 0.5f;
            var half = new Vector3(
                Mathf.Abs(Vector3.Dot(faces[0].center - center, ax)),
                Mathf.Abs(Vector3.Dot(faces[2].center - center, ay)),
                Mathf.Abs(Vector3.Dot(faces[4].center - center, az)));
            return new Box(center, ax, ay, az, half);
        }

        private static Vector3 Normal(Face[] faces, int index, Vector3 fallback) =>
            faces[index].normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? faces[index].normal.normalized
                : fallback;

        public static int ThinAxis(in Box box)
        {
            Vector3 h = box.Half;
            int thin = 0;
            if (h.y < h[thin]) thin = 1;
            if (h.z < h[thin]) thin = 2;
            float other = Mathf.Min(h[(thin + 1) % 3], h[(thin + 2) % 3]);
            bool isPlate = h[thin] * MinPlateAspectRatio <= other;
            return isPlate ? thin : -1;
        }

        public static Vector3 CameraOffset(in Box box, int thinAxis, Vector3 camPos, float gap)
        {
            Vector3 n = box.Axis(thinAxis);
            float side = Vector3.Dot(camPos - box.Center, n) < 0f ? -1f : 1f;
            return n * (side * (box.Half[thinAxis] + gap));
        }
    }
}
