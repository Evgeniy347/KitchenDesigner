using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class SoftSlabSurface
    {
        public const int DefaultFilletSegments = 3;
        public const float MaxFilletThicknessRatio = 0.5f;
        public const float MaxFilletPlanRatio = 0.25f;

        private readonly float _width;
        private readonly float _depth;
        private readonly float _thickness;
        private readonly float _fillet;
        private readonly CornerRadii _radii;
        private readonly SoftSlabRing[] _sections;

        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }

        public float Width => _width;
        public float Depth => _depth;
        public float Thickness => _thickness;
        public float Fillet => _fillet;
        public CornerRadii Radii => _radii;
        public int PointsPerRing { get; }
        public IReadOnlyList<SoftSlabRing> Sections => _sections;

        public static float FitFillet(float width, float depth, float thickness, float asked)
        {
            float minSide = Mathf.Min(Mathf.Abs(width), Mathf.Abs(depth));
            return Mathf.Clamp(asked, 0f, Mathf.Min(
                Mathf.Abs(thickness) * MaxFilletThicknessRatio, minSide * MaxFilletPlanRatio));
        }

        public SoftSlabSurface(float width, float depth, CornerRadii radii,
            float thickness, float fillet)
            : this(width, depth, radii, thickness, fillet,
                RoundedRectProfile.DefaultSegments, DefaultFilletSegments)
        {
        }

        public SoftSlabSurface(float width, float depth, CornerRadii radii, float thickness,
            float fillet, int outlineSegments, int filletSegments)
        {
            _width = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(width));
            _depth = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(depth));
            _thickness = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(thickness));
            _fillet = FitFillet(_width, _depth, _thickness, fillet);
            _radii = RoundedRectProfile.Fit(_width, _depth, radii);
            _sections = BuildSections(_thickness, _fillet, filletSegments);
            PointsPerRing = RoundedRectProfile.RingPointCount(outlineSegments);

            int rings = _sections.Length;
            int perRing = PointsPerRing;
            int bottomCentre = rings * perRing;
            int topCentre = bottomCentre + 1;

            var positions = new Vector3[topCentre + 1];
            var normals = new Vector3[positions.Length];
            var uvs = new Vector2[positions.Length];
            var triangles = new int[((rings - 1) * perRing * 2 + 2 * perRing) * 3];

            for (int ring = 0; ring < rings; ring++)
            {
                var section = _sections[ring];
                var outline = RoundedRectProfile.Ring(
                    _width - 2f * section.Inset, _depth - 2f * section.Inset,
                    _radii.Inset(section.Inset), outlineSegments);

                for (int j = 0; j < perRing; j++)
                {
                    int index = ring * perRing + j;
                    var point = outline[j];
                    positions[index] = new Vector3(point.Position.x, section.Y, point.Position.y);
                    normals[index] = section.Normal(point.Outward);
                    uvs[index] = Unwrap(positions[index]);
                }
            }

            float half = _thickness * 0.5f;
            positions[bottomCentre] = new Vector3(0f, -half, 0f);
            normals[bottomCentre] = Vector3.down;
            uvs[bottomCentre] = Unwrap(positions[bottomCentre]);
            positions[topCentre] = new Vector3(0f, half, 0f);
            normals[topCentre] = Vector3.up;
            uvs[topCentre] = Unwrap(positions[topCentre]);

            int cursor = 0;
            for (int ring = 0; ring + 1 < rings; ring++)
            for (int j = 0; j < perRing; j++)
            {
                int next = (j + 1) % perRing;
                int lowHere = ring * perRing + j;
                int lowNext = ring * perRing + next;

                triangles[cursor++] = lowHere;
                triangles[cursor++] = lowHere + perRing;
                triangles[cursor++] = lowNext;
                triangles[cursor++] = lowNext;
                triangles[cursor++] = lowHere + perRing;
                triangles[cursor++] = lowNext + perRing;
            }

            int topRing = (rings - 1) * perRing;
            for (int j = 0; j < perRing; j++)
            {
                int next = (j + 1) % perRing;

                triangles[cursor++] = bottomCentre;
                triangles[cursor++] = j;
                triangles[cursor++] = next;

                triangles[cursor++] = topCentre;
                triangles[cursor++] = topRing + next;
                triangles[cursor++] = topRing + j;
            }

            Positions = positions;
            Normals = normals;
            Uvs = uvs;
            Triangles = triangles;
        }

        private static SoftSlabRing[] BuildSections(float thickness, float fillet, int segments)
        {
            float half = thickness * 0.5f;
            if (fillet <= Tolerance.EpsilonUnits)
                return new[]
                {
                    new SoftSlabRing(0f, -half, 0f, -1f),
                    new SoftSlabRing(0f, half, 0f, 1f),
                };

            int steps = Mathf.Max(1, segments);
            bool sidesMeet = thickness - 2f * fillet <= Tolerance.EpsilonUnits;
            var rings = new List<SoftSlabRing>();

            for (int k = 0; k <= steps; k++)
            {
                float angle = Mathf.PI * 0.5f * k / steps;
                rings.Add(new SoftSlabRing(fillet * (1f - Mathf.Sin(angle)),
                    -half + fillet * (1f - Mathf.Cos(angle)),
                    Mathf.Sin(angle), -Mathf.Cos(angle)));
            }

            for (int k = sidesMeet ? steps - 1 : steps; k >= 0; k--)
            {
                float angle = Mathf.PI * 0.5f * k / steps;
                rings.Add(new SoftSlabRing(fillet * (1f - Mathf.Sin(angle)),
                    half - fillet * (1f - Mathf.Cos(angle)),
                    Mathf.Sin(angle), Mathf.Cos(angle)));
            }

            return rings.ToArray();
        }

        private Vector2 Unwrap(Vector3 position)
            => new Vector2(position.x / _width + 0.5f, position.z / _depth + 0.5f);
    }
}
