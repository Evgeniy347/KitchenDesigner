using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CushionMesh
    {
        public static Mesh Build(Vector3 size, float radius)
            => Build(new CushionSurface(size, radius), false);

        public static Mesh Build(Vector3 size, float radius, bool physicalUv)
            => Build(new CushionSurface(size, radius), physicalUv);

        public static Mesh Build(CushionSurface surface) => Build(surface, false);

        public static Mesh Build(CushionSurface surface, bool physicalUv)
        {
            var mesh = new Mesh();
            if (surface == null) return mesh;

            mesh.vertices = surface.Positions;
            mesh.normals = surface.Normals;
            mesh.uv = physicalUv
                ? PartUv.BoxProjectionUnits(surface.Positions, surface.Normals)
                : surface.Uvs;
            mesh.triangles = surface.Triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
