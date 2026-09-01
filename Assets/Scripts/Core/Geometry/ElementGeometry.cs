using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ElementGeometry
    {
        public readonly int Id;

        public readonly string Name;

        public readonly Face[] Faces;

        public readonly Face[] GrooveSeatFaces;

        public readonly Face[] GrooveWallFaces;

        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public readonly bool IsPanel;

        public readonly bool CentresOnTarget;

        public ElementGeometry(int id, string name, Face[] faces, Face[] grooveSeatFaces,
            Face[] grooveWallFaces, Vector3 min, Vector3 max, bool isPanel,
            bool centresOnTarget = false)
        {
            Id = id;
            Name = name;
            Faces = faces;
            GrooveSeatFaces = grooveSeatFaces;
            GrooveWallFaces = grooveWallFaces;
            Min = min;
            Max = max;
            IsPanel = isPanel;
            CentresOnTarget = centresOnTarget;
        }

        public bool IsEmpty => Faces == null || Faces.Length == 0;

        public static void BoundsOf(Vector3[] vertices, out Vector3 min, out Vector3 max)
        {
            min = vertices[0];
            max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }
        }

        public static ElementGeometry Box(string name, Vector3 center, Vector3 sizeUnits,
            bool isPanel = false, bool centresOnTarget = false)
            => Box(name, center, sizeUnits, Quaternion.identity, isPanel, centresOnTarget);

        public static ElementGeometry Box(string name, Vector3 center, Vector3 sizeUnits,
            Quaternion rotation, bool isPanel = false, bool centresOnTarget = false)
        {
            var half = sizeUnits * 0.5f;
            var axes = new[]
            {
                rotation * Vector3.right,
                rotation * Vector3.up,
                rotation * Vector3.forward,
            };
            var faceDims = new[]
            {
                new Vector2(sizeUnits.y, sizeUnits.z),
                new Vector2(sizeUnits.x, sizeUnits.z),
                new Vector2(sizeUnits.x, sizeUnits.y),
            };
            var offsets = new[]
            {
                axes[0] * half.x, -axes[0] * half.x,
                axes[1] * half.y, -axes[1] * half.y,
                axes[2] * half.z, -axes[2] * half.z,
            };
            var normals = new[] { axes[0], -axes[0], axes[1], -axes[1], axes[2], -axes[2] };
            var rightAxis = new[] { axes[1], axes[1], axes[0], axes[0], axes[0], axes[0] };
            var upAxis = new[] { axes[2], axes[2], axes[2], axes[2], axes[1], axes[1] };

            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
                faces[i] = new Face(center + offsets[i], normals[i], faceDims[i / 2],
                    rightAxis[i], upAxis[i]);

            var corners = new Vector3[8];
            int c = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        corners[c++] = center
                            + axes[0] * (half.x * sx)
                            + axes[1] * (half.y * sy)
                            + axes[2] * (half.z * sz);
            BoundsOf(corners, out var min, out var max);

            var empty = System.Array.Empty<Face>();
            return new ElementGeometry(name.GetHashCode(), name, faces, empty, empty,
                min, max, isPanel, centresOnTarget);
        }
    }
}
