using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
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

            public bool IntersectsSegment(Vector3 origin, Vector3 dir, float length)
            {
                Vector3 d = origin - Center;
                float tMin = 0f, tMax = length;
                for (int a = 0; a < 3; a++)
                {
                    Vector3 axis = Axis(a);
                    float o = Vector3.Dot(d, axis);
                    float slope = Vector3.Dot(dir, axis);
                    float h = Half[a];
                    bool parallelToThisSlab = Mathf.Abs(slope) < Tolerance.EpsilonUnits;
                    if (parallelToThisSlab)
                    {
                        bool offsetPastTheSlab = Mathf.Abs(o) > h;
                        if (offsetPastTheSlab) return false;
                        continue;
                    }
                    float t1 = (-h - o) / slope, t2 = (h - o) / slope;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tMin = Mathf.Max(tMin, t1);
                    tMax = Mathf.Min(tMax, t2);
                    if (tMin > tMax) return false;
                }
                float insideLength = tMax - tMin;
                return insideLength > Tolerance.EpsilonUnits;
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

        public static bool Blocked(
            Vector3 origin, Vector3 normal, float arrowLen, IReadOnlyList<Box>? neighbours)
        {
            if (neighbours == null || neighbours.Count == 0) return false;
            for (int i = 0; i < neighbours.Count; i++)
                if (neighbours[i].IntersectsSegment(origin, normal, arrowLen)) return true;
            return false;
        }

        public static float PullBack(
            Vector3 origin, Vector3 normal, float arrowLen, float maxShift,
            IReadOnlyList<Box>? neighbours)
        {
            if (!Blocked(origin, normal, arrowLen, neighbours)) return 0f;
            float step = arrowLen * 0.5f;
            for (float d = arrowLen; d <= maxShift; d += step)
                if (!Blocked(origin - normal * d, normal, arrowLen, neighbours)) return d;
            return arrowLen;
        }
    }
}
