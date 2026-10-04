using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MatMesh
    {
        public static Mesh Build(MatSurface surface)
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
