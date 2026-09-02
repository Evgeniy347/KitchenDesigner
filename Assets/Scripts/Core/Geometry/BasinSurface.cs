using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class BasinSurface
    {
        public const int DefaultFilletSegments = 4;
        public const float MaxRimPlanRatio = 0.45f;
        public const float MaxFilletPlanRatio = 0.5f;

        private readonly float _width;
        private readonly float _depth;
        private readonly float _height;
        private readonly float _rim;
        private readonly float _bowlDepth;
        private readonly float _fillet;
        private readonly CornerRadii _radii;
        private readonly SoftSlabRing[] _sections;

        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }

        public float Width => _width;
        public float Depth => _depth;
        public float Height => _height;
        public float Rim => _rim;
        public float BowlDepth => _bowlDepth;
        public float Fillet => _fillet;
        public CornerRadii Radii => _radii;
        public int PointsPerRing { get; }
        public IReadOnlyList<SoftSlabRing> Sections => _sections;

        public float BowlFloorY => _height * 0.5f - _bowlDepth;

        public static float FitRim(float width, float depth, float asked)
            => Mathf.Clamp(asked, 0f,
                Mathf.Min(Mathf.Abs(width), Mathf.Abs(depth)) * MaxRimPlanRatio);

        public static float FitBowlDepth(float height, float asked)
            => Mathf.Clamp(asked, 0f, Mathf.Abs(height));

        public static float FitFillet(float width, float depth, float rim,
            float bowlDepth, float asked)
        {
            float innerSide = Mathf.Min(Mathf.Abs(width), Mathf.Abs(depth)) - 2f * rim;
            return Mathf.Clamp(asked, 0f, Mathf.Min(Mathf.Abs(bowlDepth),
                Mathf.Max(0f, innerSide) * MaxFilletPlanRatio));
        }

        public BasinSurface(float width, float depth, float height, CornerRadii radii,
            float rim, float bowlDepth, float fillet)
            : this(width, depth, height, radii, rim, bowlDepth, fillet,
                RoundedRectProfile.DefaultSegments, DefaultFilletSegments)
        {
        }

        public BasinSurface(float width, float depth, float height, CornerRadii radii,
            float rim, float bowlDepth, float fillet, int outlineSegments, int filletSegments)
        {
            _width = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(width));
            _depth = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(depth));
            _height = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(height));
            _rim = FitRim(_width, _depth, rim);
            _bowlDepth = FitBowlDepth(_height, bowlDepth);
            _fillet = FitFillet(_width, _depth, _rim, _bowlDepth, fillet);
            _radii = RoundedRectProfile.Fit(_width, _depth, radii);
            _sections = BuildSections(_height, _rim, _bowlDepth, _fillet, filletSegments);
            PointsPerRing = RoundedRectProfile.RingPointCount(outlineSegments);

            int rings = _sections.Length;
            int perRing = PointsPerRing;
            int shellFloorCentre = rings * perRing;
            int bowlFloorCentre = shellFloorCentre + 1;

            var positions = new Vector3[bowlFloorCentre + 1];
            var normals = new Vector3[positions.Length];
            var uvs = new Vector2[positions.Length];

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

            positions[shellFloorCentre] = new Vector3(0f, -_height * 0.5f, 0f);
            normals[shellFloorCentre] = Vector3.down;
            uvs[shellFloorCentre] = Unwrap(positions[shellFloorCentre]);
            positions[bowlFloorCentre] = new Vector3(0f, BowlFloorY, 0f);
            normals[bowlFloorCentre] = Vector3.up;
            uvs[bowlFloorCentre] = Unwrap(positions[bowlFloorCentre]);

            var triangles = new List<int>();

            for (int ring = 0; ring + 1 < rings; ring++)
            {
                if (Coincide(_sections[ring], _sections[ring + 1])) continue;

                for (int j = 0; j < perRing; j++)
                {
                    int next = (j + 1) % perRing;
                    int here = ring * perRing + j;
                    int there = ring * perRing + next;

                    triangles.Add(here);
                    triangles.Add(here + perRing);
                    triangles.Add(there);
                    triangles.Add(there);
                    triangles.Add(here + perRing);
                    triangles.Add(there + perRing);
                }
            }

            int lastRing = (rings - 1) * perRing;
            for (int j = 0; j < perRing; j++)
            {
                int next = (j + 1) % perRing;

                triangles.Add(shellFloorCentre);
                triangles.Add(j);
                triangles.Add(next);

                triangles.Add(bowlFloorCentre);
                triangles.Add(lastRing + next);
                triangles.Add(lastRing + j);
            }

            Positions = positions;
            Normals = normals;
            Uvs = uvs;
            Triangles = triangles.ToArray();
        }

        private static SoftSlabRing[] BuildSections(float height, float rim,
            float bowlDepth, float fillet, int segments)
        {
            float half = height * 0.5f;
            float floor = half - bowlDepth;

            var rings = new List<SoftSlabRing>
            {
                new SoftSlabRing(0f, -half, 1f, 0f),
                new SoftSlabRing(0f, half, 1f, 0f),
                new SoftSlabRing(0f, half, 0f, 1f),
                new SoftSlabRing(rim, half, 0f, 1f),
                new SoftSlabRing(rim, half, -1f, 0f),
            };

            int steps = Mathf.Max(1, segments);
            for (int k = 0; k <= steps; k++)
            {
                float angle = Mathf.PI * 0.5f * k / steps;
                rings.Add(new SoftSlabRing(
                    rim + fillet * (1f - Mathf.Cos(angle)),
                    floor + fillet * (1f - Mathf.Sin(angle)),
                    -Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            return rings.ToArray();
        }

        private static bool Coincide(SoftSlabRing a, SoftSlabRing b)
            => Mathf.Abs(a.Inset - b.Inset) < Tolerance.EpsilonUnits
                && Mathf.Abs(a.Y - b.Y) < Tolerance.EpsilonUnits;

        private Vector2 Unwrap(Vector3 position)
            => new Vector2(position.x / _width + 0.5f, position.z / _depth + 0.5f);
    }
}
