using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PartUv
    {
        public static Vector2[] BoxProjectionUnits(Vector3[] positions, Vector3[] normals)
        {
            var uvs = new Vector2[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                int axis = DominantAxis(normals[i]);
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;
                uvs[i] = new Vector2(positions[i][u], positions[i][v]);
            }

            return uvs;
        }

        private static int DominantAxis(Vector3 normal)
        {
            float x = Mathf.Abs(normal.x);
            float y = Mathf.Abs(normal.y);
            float z = Mathf.Abs(normal.z);
            if (x >= y && x >= z) return 0;
            return y >= z ? 1 : 2;
        }
    }
}
