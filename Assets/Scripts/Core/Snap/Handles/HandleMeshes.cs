using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    /// <summary>Меши ручек: единичные, вдоль +Z (см. HandleMeshesTests).</summary>
    public static class HandleMeshes
    {
        private const int Segments = 16;

        private static Mesh? _cone;
        private static Mesh? _cube;
        private static Mesh? _cylinder;

        public static Mesh Cone()
        {
            if (_cone != null) return _cone;
            var verts = new List<Vector3>
            {
                new Vector3(0f, 0f, 0.5f),
                new Vector3(0f, 0f, -0.5f),
            };
            const int apex = 0, baseCenter = 1;
            int ring = verts.Count;
            AddRing(verts, -0.5f);

            var tris = new List<int>();
            for (int i = 0; i < Segments; i++)
            {
                int cur = ring + i, next = ring + (i + 1) % Segments;
                tris.Add(apex); tris.Add(next); tris.Add(cur);
                tris.Add(baseCenter); tris.Add(cur); tris.Add(next);
            }
            _cone = Build("HandleCone", verts, tris);
            return _cone;
        }

        public static Mesh Cylinder()
        {
            if (_cylinder != null) return _cylinder;
            var verts = new List<Vector3>
            {
                new Vector3(0f, 0f, 0.5f),
                new Vector3(0f, 0f, -0.5f),
            };
            const int frontCenter = 0, backCenter = 1;
            int front = verts.Count;
            AddRing(verts, 0.5f);
            int back = verts.Count;
            AddRing(verts, -0.5f);

            var tris = new List<int>();
            for (int i = 0; i < Segments; i++)
            {
                int j = (i + 1) % Segments;
                tris.Add(frontCenter); tris.Add(front + i); tris.Add(front + j);
                tris.Add(backCenter); tris.Add(back + j); tris.Add(back + i);
                tris.Add(front + i); tris.Add(back + i); tris.Add(back + j);
                tris.Add(front + i); tris.Add(back + j); tris.Add(front + j);
            }
            _cylinder = Build("HandleCylinder", verts, tris);
            return _cylinder;
        }

        public static Mesh Cube()
        {
            if (_cube != null) return _cube;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            const float h = 0.5f;
            Quad(new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h));
            Quad(new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h));
            Quad(new Vector3(h, -h, h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h));
            Quad(new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h));
            Quad(new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h), new Vector3(-h, h, -h));
            Quad(new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h));

            _cube = Build("HandleCube", verts, tris);
            return _cube;
        }

        private static void AddRing(List<Vector3> verts, float z)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a = (float)i / Segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, z));
            }
        }

        private static Mesh Build(string name, List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
