using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CushionMesh
    {
        public static Mesh Build(Vector3 size, float radius)
            => Build(new CushionSurface(size, radius));

        public static Mesh Build(CushionSurface surface)
        {
            var mesh = new Mesh();
            if (surface == null) return mesh;

            mesh.vertices = surface.Positions;
            mesh.normals = surface.Normals;
            mesh.uv = surface.Uvs;
            mesh.triangles = surface.Triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
