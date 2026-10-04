using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class MatSurface
    {
        public const int DefaultEdgeSegments = 8;
        public const float HardEdgeAngleDeg = 45f;

        private const float SliceMergeUnits = 1e-7f;

        private readonly float _width;
        private readonly float _depth;
        private readonly float _thickness;
        private readonly float _edge;
        private readonly bool _roundLowerFace;
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }

        public float EdgeRadius => _edge;

        public static float FitEdge(float width, float thickness, float asked)
            => Mathf.Clamp(asked, 0f, Mathf.Min(Mathf.Abs(thickness) * 0.5f, Mathf.Abs(width) * 0.25f));

        public MatSurface(float width, float depth, float thickness, CornerRadii radii,
            float edgeRadius, bool roundLowerFace)
            : this(width, depth, thickness, radii, edgeRadius, roundLowerFace,
                RoundedRectProfile.DefaultSegments, DefaultEdgeSegments)
        {
        }

        public MatSurface(float width, float depth, float thickness, CornerRadii radii,
            float edgeRadius, bool roundLowerFace, int outlineSegments, int edgeSegments)
        {
            _width = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(width));
            _depth = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(depth));
            _thickness = Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(thickness));
            _edge = FitEdge(_width, _thickness, edgeRadius);
            _roundLowerFace = roundLowerFace;

            var outline = RoundedRectProfile.Build(_width, _depth, radii, outlineSegments);
            var slices = Slices(outline, Mathf.Max(1, edgeSegments));

            AddTop(outline, slices);
            AddWalls(outline, slices);
            AddBottom(outline);

            Positions = _positions.ToArray();
            Normals = _normals.ToArray();
            Uvs = _uvs.ToArray();
            Triangles = _triangles.ToArray();
        }

        public float TopHeightAt(float x)
        {
            float half = _width * 0.5f;
            float flat = half - _edge;
            float reach = Mathf.Abs(x) - flat;
            float top = _thickness * 0.5f;
            if (_edge <= 0f || reach <= 0f) return top;
            reach = Mathf.Min(reach, _edge);
            return top - _edge + Mathf.Sqrt(Mathf.Max(0f, _edge * _edge - reach * reach));
        }

        private float TopArcLengthAt(float x)
        {
            float half = _width * 0.5f;
            float flat = half - _edge;
            float sign = x < 0f ? -1f : 1f;
            float reach = Mathf.Abs(x) - flat;
            if (_edge <= 0f || reach <= 0f) return x;
            float angle = Mathf.Asin(Mathf.Clamp01(reach / _edge));
            return sign * (flat + _edge * angle);
        }

        private Vector3 TopNormalAt(float x)
        {
            float flat = _width * 0.5f - _edge;
            float reach = Mathf.Abs(x) - flat;
            if (_edge <= 0f || reach <= 0f) return Vector3.up;
            float sin = Mathf.Clamp01(reach / _edge);
            float sign = x < 0f ? -1f : 1f;
            return new Vector3(sign * sin, Mathf.Sqrt(Mathf.Max(0f, 1f - sin * sin)), 0f);
        }

        private float Place(float height) => _roundLowerFace ? -height : height;

        private Vector3 Flip(Vector3 normal)
            => _roundLowerFace ? new Vector3(normal.x, -normal.y, normal.z) : normal;

        private List<float> Slices(Vector2[] outline, int edgeSegments)
        {
            var values = new List<float>();
            float half = _width * 0.5f;
            float flat = half - _edge;
            foreach (var point in outline) values.Add(point.x);
            values.Add(-half);
            values.Add(half);
            if (_edge > 0f)
                for (int k = 0; k <= edgeSegments; k++)
                {
                    float reach = _edge * Mathf.Sin(Mathf.PI * 0.5f * k / edgeSegments);
                    values.Add(flat + reach);
                    values.Add(-(flat + reach));
                }

            values.Sort();
            var unique = new List<float>();
            foreach (var value in values)
                if (unique.Count == 0 || value - unique[unique.Count - 1] > SliceMergeUnits)
                    unique.Add(value);
            return unique;
        }

        private static void ZRangeAt(Vector2[] outline, float x, out float minZ, out float maxZ)
        {
            minZ = float.MaxValue;
            maxZ = float.MinValue;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                float low = Mathf.Min(a.x, b.x);
                float high = Mathf.Max(a.x, b.x);
                if (x < low - SliceMergeUnits || x > high + SliceMergeUnits) continue;

                if (high - low <= SliceMergeUnits)
                {
                    minZ = Mathf.Min(minZ, Mathf.Min(a.y, b.y));
                    maxZ = Mathf.Max(maxZ, Mathf.Max(a.y, b.y));
                    continue;
                }

                float t = Mathf.Clamp01((x - a.x) / (b.x - a.x));
                float z = Mathf.Lerp(a.y, b.y, t);
                minZ = Mathf.Min(minZ, z);
                maxZ = Mathf.Max(maxZ, z);
            }
        }

        private void AddTop(Vector2[] outline, List<float> slices)
        {
            var lower = new int[slices.Count];
            var upper = new int[slices.Count];
            for (int i = 0; i < slices.Count; i++)
            {
                float x = slices[i];
                ZRangeAt(outline, x, out float minZ, out float maxZ);
                float y = Place(TopHeightAt(x));
                var normal = Flip(TopNormalAt(x));
                float u = TopArcLengthAt(x) / _width + 0.5f;
                lower[i] = AddVertex(new Vector3(x, y, minZ), normal, new Vector2(u, minZ / _depth + 0.5f));
                upper[i] = AddVertex(new Vector3(x, y, maxZ), normal, new Vector2(u, maxZ / _depth + 0.5f));
            }

            for (int i = 0; i + 1 < slices.Count; i++)
            {
                AddFacing(lower[i], upper[i], upper[i + 1], Flip(Vector3.up));
                AddFacing(lower[i], upper[i + 1], lower[i + 1], Flip(Vector3.up));
            }
        }

        private void AddBottom(Vector2[] outline)
        {
            var down = Flip(Vector3.down);
            float y = Place(-_thickness * 0.5f);
            int centre = AddVertex(new Vector3(0f, y, 0f), down, new Vector2(0.5f, 0.5f));
            var ring = new int[outline.Length];
            for (int i = 0; i < outline.Length; i++)
                ring[i] = AddVertex(new Vector3(outline[i].x, y, outline[i].y), down,
                    new Vector2(outline[i].x / _width + 0.5f, outline[i].y / _depth + 0.5f));

            for (int i = 0; i < outline.Length; i++)
                AddFacing(centre, ring[i], ring[(i + 1) % outline.Length], down);
        }

        private void AddWalls(Vector2[] outline, List<float> slices)
        {
            int count = outline.Length;
            var edgeNormals = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % count];
                edgeNormals[i] = new Vector2(b.y - a.y, a.x - b.x).normalized;
            }

            float hardCos = Mathf.Cos(HardEdgeAngleDeg * Mathf.Deg2Rad);
            float run = 0f;
            for (int i = 0; i < count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % count];
                var before = edgeNormals[(i - 1 + count) % count];
                var after = edgeNormals[(i + 1) % count];
                var self = edgeNormals[i];
                var normalA = Vector2.Dot(before, self) >= hardCos ? (before + self).normalized : self;
                var normalB = Vector2.Dot(self, after) >= hardCos ? (self + after).normalized : self;

                var points = new List<Vector2> { a };
                float low = Mathf.Min(a.x, b.x);
                float high = Mathf.Max(a.x, b.x);
                if (high - low > SliceMergeUnits)
                {
                    var inside = new List<float>();
                    foreach (float x in slices)
                        if (x > low + SliceMergeUnits && x < high - SliceMergeUnits) inside.Add(x);
                    if (a.x > b.x) inside.Reverse();
                    foreach (float x in inside)
                        points.Add(Vector2.Lerp(a, b, (x - a.x) / (b.x - a.x)));
                }
                points.Add(b);

                float total = (b - a).magnitude;
                for (int k = 0; k + 1 < points.Count; k++)
                {
                    var p = points[k];
                    var q = points[k + 1];
                    float tp = total > 0f ? (p - a).magnitude / total : 0f;
                    float tq = total > 0f ? (q - a).magnitude / total : 1f;
                    var np = Vector2.Lerp(normalA, normalB, tp).normalized;
                    var nq = Vector2.Lerp(normalA, normalB, tq).normalized;
                    AddWallQuad(p, q, np, nq, run + (p - a).magnitude, run + (q - a).magnitude,
                        self);
                }

                run += total;
            }
        }

        private void AddWallQuad(Vector2 p, Vector2 q, Vector2 np, Vector2 nq, float runP,
            float runQ, Vector2 faceNormal)
        {
            float bottom = Place(-_thickness * 0.5f);
            float topP = Place(TopHeightAt(p.x));
            float topQ = Place(TopHeightAt(q.x));
            float vBottom = 0f;
            float vTopP = (TopHeightAt(p.x) + _thickness * 0.5f) / _depth;
            float vTopQ = (TopHeightAt(q.x) + _thickness * 0.5f) / _depth;

            int bp = AddVertex(new Vector3(p.x, bottom, p.y), new Vector3(np.x, 0f, np.y),
                new Vector2(runP / _width, vBottom));
            int bq = AddVertex(new Vector3(q.x, bottom, q.y), new Vector3(nq.x, 0f, nq.y),
                new Vector2(runQ / _width, vBottom));
            int tp = AddVertex(new Vector3(p.x, topP, p.y), new Vector3(np.x, 0f, np.y),
                new Vector2(runP / _width, vTopP));
            int tq = AddVertex(new Vector3(q.x, topQ, q.y), new Vector3(nq.x, 0f, nq.y),
                new Vector2(runQ / _width, vTopQ));

            var face = new Vector3(faceNormal.x, 0f, faceNormal.y);
            AddFacing(bp, bq, tq, face);
            AddFacing(bp, tq, tp, face);
        }

        private int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _positions.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
            return _positions.Count - 1;
        }

        private void AddFacing(int a, int b, int c, Vector3 outward)
        {
            var cross = Vector3.Cross(_positions[b] - _positions[a], _positions[c] - _positions[a]);
            if (cross.sqrMagnitude <= 0f) return;
            bool flip = Vector3.Dot(cross, outward) < 0f;
            _triangles.Add(a);
            _triangles.Add(flip ? c : b);
            _triangles.Add(flip ? b : c);
        }
    }
}
