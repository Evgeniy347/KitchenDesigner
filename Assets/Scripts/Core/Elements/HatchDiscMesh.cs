using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class HatchDiscMesh
    {
        private static Mesh? _unit;

        public static Mesh Unit()
        {
            if (_unit != null) return _unit!;

            var mesh = CylinderStackMesh.Build(new CylinderSection(0.5f, 1f));
            var turn = Quaternion.AngleAxis(90f, Vector3.right);

            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = turn * vertices[i];
            mesh.vertices = vertices;

            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = turn * normals[i];
            mesh.normals = normals;

            mesh.RecalculateBounds();
            mesh.name = "HatchDisc";
            _unit = mesh;
            return mesh;
        }
    }
}
