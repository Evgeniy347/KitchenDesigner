using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MeshArea
    {
        public static float TotalMm2(Vector3[] vertices, int[] triangles)
        {
            float total = 0f;
            for (int i = 0; i + 3 <= triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return total;
        }

        public static bool IsWoundOutward(Vector3[] vertices, int[] triangles, Vector3 interiorPoint)
        {
            for (int i = 0; i + 3 <= triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                var toTriangle = (a + b + c) / 3f - interiorPoint;
                if (Vector3.Dot(normal, toTriangle) <= 0f) return false;
            }
            return true;
        }
    }
}
