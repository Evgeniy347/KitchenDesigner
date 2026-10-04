using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SoftSlabMesh
    {
        public static Mesh Build(float width, float depth, float radius,
            float thickness, float fillet)
            => Build(new SoftSlabSurface(width, depth, CornerRadii.Uniform(radius),
                thickness, fillet));

        public static Mesh Build(SoftSlabSurface surface) => Build(surface, false);

        public static Mesh Build(SoftSlabSurface surface, bool physicalUv)
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
