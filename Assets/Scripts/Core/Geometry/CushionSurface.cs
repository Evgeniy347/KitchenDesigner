using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class CushionSurface
    {
        public const int DefaultArcSegments = 3;
        public const int DefaultFlatSegments = 2;
        public const float DefaultBulgeRatio = 0.18f;
        public const float MaxBulgeRatio = 0.3f;
        public const float NormalProbeRatio = 0.002f;

        private readonly Vector3 _half;
        private readonly Vector3 _core;
        private readonly Vector3 _inner;
        private readonly float _radius;
        private readonly float _bulge;
        private readonly float _probe;
        private readonly float[][] _samples;

        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }

        public Vector3 HalfExtents => _half;
        public Vector3 CoreHalfExtents => _core;
        public Vector3 InnerHalfExtents => _inner;
        public float Radius => _radius;
        public float Bulge => _bulge;

        public static float BulgeFor(Vector3 size)
            => MinComponent(Abs(size)) * 0.5f * DefaultBulgeRatio;

        public CushionSurface(Vector3 size, float radius)
            : this(size, radius, BulgeFor(size), DefaultArcSegments, DefaultFlatSegments)
        {
        }

        public CushionSurface(Vector3 size, float radius, float bulge,
            int arcSegments, int flatSegments)
        {
            _half = new Vector3(
                Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(size.x) * 0.5f),
                Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(size.y) * 0.5f),
                Mathf.Max(Tolerance.EpsilonUnits, Mathf.Abs(size.z) * 0.5f));

            _bulge = Mathf.Clamp(bulge, 0f, MinComponent(_half) * MaxBulgeRatio);
            _core = new Vector3(_half.x - _bulge, _half.y - _bulge, _half.z - _bulge);
            _radius = Mathf.Clamp(radius, 0f, MinComponent(_core));
            _inner = new Vector3(_core.x - _radius, _core.y - _radius, _core.z - _radius);
            _probe = Mathf.Max(Tolerance.EpsilonUnits, MinComponent(_core) * NormalProbeRatio);

            int arc = Mathf.Max(1, arcSegments);
            int flat = Mathf.Max(1, flatSegments);
            _samples = new[]
            {
                Samples(_inner.x, _core.x, arc, flat),
                Samples(_inner.y, _core.y, arc, flat),
                Samples(_inner.z, _core.z, arc, flat),
            };

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int axis = 0; axis < 3; axis++)
            {
                BuildFace(axis, 1f, positions, normals, uvs, triangles);
                BuildFace(axis, -1f, positions, normals, uvs, triangles);
            }

            Positions = positions.ToArray();
            Normals = normals.ToArray();
            Uvs = uvs.ToArray();
            Triangles = triangles.ToArray();
        }

        public float DistanceToInnerBox(Vector3 point)
            => (point - ClampToInnerBox(point)).magnitude;

        public Vector3 ClampToInnerBox(Vector3 point) => new Vector3(
            Mathf.Clamp(point.x, -_inner.x, _inner.x),
            Mathf.Clamp(point.y, -_inner.y, _inner.y),
            Mathf.Clamp(point.z, -_inner.z, _inner.z));

        public static float BulgeWeight(float normalized)
        {
            float cos = Mathf.Cos(Mathf.PI * 0.5f * normalized);
            return cos * cos;
        }

        private float[] Samples(float inner, float core, int arcSegments, int flatSegments)
        {
            var list = new List<float>();
            if (inner > Tolerance.EpsilonUnits)
                for (int k = 0; k <= flatSegments; k++)
                    list.Add(-inner + 2f * inner * k / flatSegments);
            else
                list.Add(0f);

            if (_radius <= Tolerance.EpsilonUnits) return list.ToArray();

            for (int k = 1; k <= arcSegments; k++)
            {
                float t = k == arcSegments
                    ? core
                    : inner + _radius * Mathf.Tan(Mathf.PI * 0.25f * k / arcSegments);
                list.Insert(0, -t);
                list.Add(t);
            }

            return list.ToArray();
        }

        private void BuildFace(int axis, float sign, List<Vector3> positions,
            List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;
            var alongU = _samples[u];
            var alongV = _samples[v];
            int start = positions.Count;

            for (int i = 0; i < alongU.Length; i++)
            for (int j = 0; j < alongV.Length; j++)
            {
                positions.Add(Point(axis, sign, alongU[i], alongV[j]));
                normals.Add(Normal(axis, sign, alongU[i], alongV[j]));
                uvs.Add(new Vector2(
                    alongU[i] / (2f * _core[u]) + 0.5f,
                    alongV[j] / (2f * _core[v]) + 0.5f));
            }

            for (int i = 0; i + 1 < alongU.Length; i++)
            for (int j = 0; j + 1 < alongV.Length; j++)
            {
                int v00 = start + i * alongV.Length + j;
                int v10 = v00 + alongV.Length;
                int v01 = v00 + 1;
                int v11 = v10 + 1;

                if (sign > 0f)
                {
                    triangles.Add(v00); triangles.Add(v10); triangles.Add(v11);
                    triangles.Add(v00); triangles.Add(v11); triangles.Add(v01);
                }
                else
                {
                    triangles.Add(v00); triangles.Add(v11); triangles.Add(v10);
                    triangles.Add(v00); triangles.Add(v01); triangles.Add(v11);
                }
            }
        }

        private Vector3 Point(int axis, float sign, float alongU, float alongV)
        {
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            var cube = Vector3.zero;
            cube[axis] = sign * _core[axis];
            cube[u] = alongU;
            cube[v] = alongV;

            var inner = ClampToInnerBox(cube);
            var outward = cube - inner;
            var direction = outward.sqrMagnitude > Tolerance.EpsilonSqr
                ? outward.normalized
                : AxisDirection(axis, sign);

            float weight = BulgeWeight(alongU / _core[u]) * BulgeWeight(alongV / _core[v]);
            return inner + direction * (_radius + _bulge * weight);
        }

        private Vector3 Normal(int axis, float sign, float alongU, float alongV)
        {
            var acrossU = Direction(Point(axis, sign, alongU + _probe, alongV)
                                    - Point(axis, sign, alongU - _probe, alongV));
            var acrossV = Direction(Point(axis, sign, alongU, alongV + _probe)
                                    - Point(axis, sign, alongU, alongV - _probe));

            var normal = Vector3.Cross(acrossU, acrossV) * sign;
            return normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? normal.normalized
                : AxisDirection(axis, sign);
        }

        private static Vector3 Direction(Vector3 step)
        {
            float length = step.magnitude;
            return length > 0f ? step / length : Vector3.zero;
        }

        private static Vector3 AxisDirection(int axis, float sign)
        {
            var direction = Vector3.zero;
            direction[axis] = sign;
            return direction;
        }

        private static Vector3 Abs(Vector3 value)
            => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private static float MinComponent(Vector3 value)
            => Mathf.Min(value.x, Mathf.Min(value.y, value.z));
    }
}
