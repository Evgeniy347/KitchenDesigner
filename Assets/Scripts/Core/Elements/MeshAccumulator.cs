using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenDesigner.Core
{
    internal sealed class MeshAccumulator
    {
        public const int SixteenBitIndexLimit = 65000;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        public int VertexCount => _vertices.Count;

        public void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
        }

        public void AddTriangle(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        public void Consume(Mesh mesh, Vector3 offset)
        {
            if (mesh == null) return;

            int start = _vertices.Count;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var triangles = mesh.triangles;

            for (int i = 0; i < vertices.Length; i++)
                AddVertex(vertices[i] + offset,
                    i < normals.Length ? normals[i] : Vector3.up,
                    i < uvs.Length ? uvs[i] : Vector2.zero);

            for (int i = 0; i < triangles.Length; i++) _triangles.Add(start + triangles[i]);

            Discard(mesh);
        }

        public Mesh Build()
        {
            var mesh = new Mesh();
            if (_vertices.Count > SixteenBitIndexLimit) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Discard(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
