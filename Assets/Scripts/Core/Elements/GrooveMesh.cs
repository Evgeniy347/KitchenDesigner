using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class GrooveMesh
    {
        private static Material? _grooveMat;

        public struct Rect2
        {
            public float xMin, xMax, yMin, yMax;
            public bool IsValid => xMax > xMin && yMax > yMin;
        }

        public static float DepthFraction(Vector3Int dims)
            => Mathf.Clamp((float)AppConstants.GROOVE_DEPTH_MM / Mathf.Max(1, dims.z), 0.01f, 0.9f);

        public static Rect2 ComputeRect(Vector3Int dims, GrooveSpec spec)
        {
            int w = Mathf.Max(1, dims.x);
            int h = Mathf.Max(1, dims.y);
            int off = AppConstants.GROOVE_OFFSET_MM;
            int wid = AppConstants.GROOVE_WIDTH_MM;
            int end = AppConstants.GROOVE_BLIND_END_MM;

            bool horizontal = spec.side == GrooveSide.Top || spec.side == GrooveSide.Bottom;
            int sideAcrossTheGrooveMM = horizontal ? h : w;
            int sideAlongTheGrooveMM = horizontal ? w : h;

            if (off + wid >= sideAcrossTheGrooveMM) return default;

            float alongMin, alongMax;
            if (spec.kind == GrooveKind.Blind)
            {
                if (2 * end >= sideAlongTheGrooveMM) return default;
                float e = (float)end / sideAlongTheGrooveMM;
                alongMin = -0.5f + e;
                alongMax = 0.5f - e;
            }
            else
            {
                alongMin = -0.5f;
                alongMax = 0.5f;
            }

            float o = (float)off / sideAcrossTheGrooveMM;
            float t = (float)wid / sideAcrossTheGrooveMM;
            bool fromMax = spec.side == GrooveSide.Top || spec.side == GrooveSide.Right;
            float acrossMin = fromMax ? 0.5f - o - t : -0.5f + o;
            float acrossMax = fromMax ? 0.5f - o : -0.5f + o + t;

            return horizontal
                ? new Rect2 { xMin = alongMin, xMax = alongMax, yMin = acrossMin, yMax = acrossMax }
                : new Rect2 { xMin = acrossMin, xMax = acrossMax, yMin = alongMin, yMax = alongMax };
        }

        public static List<Rect2> ComputeRects(Vector3Int dims, IReadOnlyList<GrooveSpec>? grooves)
        {
            var result = new List<Rect2>();
            if (grooves == null) return result;
            foreach (var g in grooves)
            {
                var r = ComputeRect(dims, g);
                if (r.IsValid) result.Add(r);
            }
            return result;
        }

        public static List<Rect2> ClampHoles(IReadOnlyList<Rect2>? holes)
        {
            var result = new List<Rect2>();
            if (holes == null) return result;
            foreach (var h in holes)
            {
                var r = new Rect2
                {
                    xMin = Mathf.Clamp(h.xMin, -0.5f, 0.5f),
                    xMax = Mathf.Clamp(h.xMax, -0.5f, 0.5f),
                    yMin = Mathf.Clamp(h.yMin, -0.5f, 0.5f),
                    yMax = Mathf.Clamp(h.yMax, -0.5f, 0.5f),
                };
                if (r.IsValid) result.Add(r);
            }
            return result;
        }

        public readonly struct SubmeshLayout
        {
            public readonly int Grooves;
            public readonly int BareEnds;

            public SubmeshLayout(int grooves, int bareEnds)
            {
                Grooves = grooves;
                BareEnds = bareEnds;
            }
        }

        public static Mesh Build(Vector3Int dims, IReadOnlyList<GrooveSpec>? grooves,
            IReadOnlyList<Rect2>? holes = null, int holeAxis = 2, int bareFaceMask = 0)
            => Build(dims, grooves, holes, holeAxis, bareFaceMask, out _);

        public static Mesh Build(Vector3Int dims, IReadOnlyList<GrooveSpec>? grooves,
            IReadOnlyList<Rect2>? holes, int holeAxis, int bareFaceMask,
            out SubmeshLayout layout)
        {
            if (holeAxis != 2)
            {
                var permuted = BuildAlongZ(dims, null, holes, holeAxis,
                    PermuteFaceMask(bareFaceMask, holeAxis), out layout);
                Permute(permuted, holeAxis);
                return permuted;
            }
            return BuildAlongZ(dims, grooves, holes, 2, bareFaceMask, out layout);
        }

        private static int PermuteFaceMask(int mask, int holeAxis)
        {
            if (mask == 0 || holeAxis == 2) return mask;
            int result = 0;
            for (int face = 0; face < 6; face++)
                if ((mask & (1 << face)) != 0)
                    result |= 1 << (FinalAxis(face / 2, holeAxis) * 2 + (face & 1));
            return result;
        }

        private static void Permute(Mesh mesh, int holeAxis)
        {
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                verts[i] = holeAxis == 1
                    ? new Vector3(v.x, v.z, v.y)
                    : new Vector3(v.z, v.y, v.x);
            }
            mesh.SetVertices(verts);
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tris = mesh.GetTriangles(sub);
                for (int t = 0; t < tris.Length; t += 3)
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                mesh.SetTriangles(tris, sub);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static Mesh BuildAlongZ(Vector3Int dims, IReadOnlyList<GrooveSpec>? grooves,
            IReadOnlyList<Rect2>? holes, int holeAxis, int bareFaceMask,
            out SubmeshLayout layout)
        {
            var rects = ComputeRects(dims, grooves);
            var holeRects = ClampHoles(holes);
            const float zf = 0.5f, zb = -0.5f;
            float gz = zf - DepthFraction(dims);

            var xs = AxisCuts(rects, holeRects, horizontal: true);
            var ys = AxisCuts(rects, holeRects, horizontal: false);
            int nx = xs.Count - 1, ny = ys.Count - 1;

            var inside = new bool[nx, ny];
            var hole = new bool[nx, ny];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    float cx = (xs[i] + xs[i + 1]) * 0.5f, cy = (ys[j] + ys[j + 1]) * 0.5f;
                    hole[i, j] = IsInside(holeRects, cx, cy);
                    inside[i, j] = !hole[i, j] && IsInside(rects, cx, cy);
                }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var body = new List<int>();
            var cut = new List<int>();
            var ends = new List<int>();

            List<int> Outer(int face) => (bareFaceMask & (1 << face)) != 0 ? ends : body;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    if (hole[i, j]) continue;
                    float x0 = xs[i], x1 = xs[i + 1], y0 = ys[j], y1 = ys[j + 1];
                    if (inside[i, j])
                        AddQuad(verts, uvs, cut, UvPlane.XY,
                            V(x0, y0, gz), V(x1, y0, gz), V(x1, y1, gz), V(x0, y1, gz));
                    else
                        AddQuad(verts, uvs, Outer(4), UvPlane.XY,
                            V(x0, y0, zf), V(x1, y0, zf), V(x1, y1, zf), V(x0, y1, zf));
                }
            }

            if (holeRects.Count == 0)
            {
                AddQuad(verts, uvs, Outer(5), UvPlane.XY,
                    V(0.5f, -0.5f, zb), V(-0.5f, -0.5f, zb), V(-0.5f, 0.5f, zb), V(0.5f, 0.5f, zb));
            }
            else
            {
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                    {
                        if (hole[i, j]) continue;
                        float x0 = xs[i], x1 = xs[i + 1], y0 = ys[j], y1 = ys[j + 1];
                        AddQuad(verts, uvs, Outer(5), UvPlane.XY,
                            V(x1, y0, zb), V(x0, y0, zb), V(x0, y1, zb), V(x1, y1, zb));
                    }
            }

            for (int i = 1; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    if (hole[i - 1, j] || hole[i, j]) continue;
                    bool left = inside[i - 1, j], right = inside[i, j];
                    if (left == right) continue;
                    float x = xs[i], y0 = ys[j], y1 = ys[j + 1];
                    if (right)
                        AddQuad(verts, uvs, cut, UvPlane.ZY,
                            V(x, y0, zf), V(x, y0, gz), V(x, y1, gz), V(x, y1, zf));
                    else
                        AddQuad(verts, uvs, cut, UvPlane.ZY,
                            V(x, y0, gz), V(x, y0, zf), V(x, y1, zf), V(x, y1, gz));
                }
            }
            for (int j = 1; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    if (hole[i, j - 1] || hole[i, j]) continue;
                    bool below = inside[i, j - 1], above = inside[i, j];
                    if (below == above) continue;
                    float y = ys[j], x0 = xs[i], x1 = xs[i + 1];
                    if (above)
                        AddQuad(verts, uvs, cut, UvPlane.XZ,
                            V(x0, y, zf), V(x1, y, zf), V(x1, y, gz), V(x0, y, gz));
                    else
                        AddQuad(verts, uvs, cut, UvPlane.XZ,
                            V(x0, y, gz), V(x1, y, gz), V(x1, y, zf), V(x0, y, zf));
                }
            }

            for (int i = 1; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    bool left = hole[i - 1, j], right = hole[i, j];
                    if (left == right) continue;
                    float x = xs[i], y0 = ys[j], y1 = ys[j + 1];
                    if (right)
                        AddQuad(verts, uvs, body, UvPlane.ZY,
                            V(x, y0, zf), V(x, y0, zb), V(x, y1, zb), V(x, y1, zf));
                    else
                        AddQuad(verts, uvs, body, UvPlane.ZY,
                            V(x, y0, zb), V(x, y0, zf), V(x, y1, zf), V(x, y1, zb));
                }
            }
            for (int j = 1; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    bool below = hole[i, j - 1], above = hole[i, j];
                    if (below == above) continue;
                    float y = ys[j], x0 = xs[i], x1 = xs[i + 1];
                    if (above)
                        AddQuad(verts, uvs, body, UvPlane.XZ,
                            V(x0, y, zf), V(x1, y, zf), V(x1, y, zb), V(x0, y, zb));
                    else
                        AddQuad(verts, uvs, body, UvPlane.XZ,
                            V(x0, y, zb), V(x1, y, zb), V(x1, y, zf), V(x0, y, zf));
                }
            }

            for (int j = 0; j < ny; j++)
            {
                float y0 = ys[j], y1 = ys[j + 1];
                if (!hole[0, j])
                {
                    float zl = inside[0, j] ? gz : zf;
                    AddQuad(verts, uvs, Outer(1), UvPlane.ZY,
                        V(-0.5f, y0, zb), V(-0.5f, y0, zl), V(-0.5f, y1, zl), V(-0.5f, y1, zb));
                }
                if (!hole[nx - 1, j])
                {
                    float zr = inside[nx - 1, j] ? gz : zf;
                    AddQuad(verts, uvs, Outer(0), UvPlane.ZY,
                        V(0.5f, y0, zr), V(0.5f, y0, zb), V(0.5f, y1, zb), V(0.5f, y1, zr));
                }
            }
            for (int i = 0; i < nx; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                if (!hole[i, 0])
                {
                    float zd = inside[i, 0] ? gz : zf;
                    AddQuad(verts, uvs, Outer(3), UvPlane.XZ,
                        V(x0, -0.5f, zb), V(x1, -0.5f, zb), V(x1, -0.5f, zd), V(x0, -0.5f, zd));
                }
                if (!hole[i, ny - 1])
                {
                    float zu = inside[i, ny - 1] ? gz : zf;
                    AddQuad(verts, uvs, Outer(2), UvPlane.XZ,
                        V(x0, 0.5f, zu), V(x1, 0.5f, zu), V(x1, 0.5f, zb), V(x0, 0.5f, zb));
                }
            }

            var mesh = new Mesh { name = "PartWithGrooves" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            int extra = (cut.Count > 0 ? 1 : 0) + (ends.Count > 0 ? 1 : 0);
            int next = 1;
            layout = new SubmeshLayout(
                cut.Count > 0 ? next++ : -1,
                ends.Count > 0 ? next++ : -1);

            mesh.subMeshCount = 1 + extra;
            mesh.SetTriangles(body, 0);
            if (layout.Grooves >= 0) mesh.SetTriangles(cut, layout.Grooves);
            if (layout.BareEnds >= 0) mesh.SetTriangles(ends, layout.BareEnds);
            mesh.RecalculateNormals();
            ScaleUvToDecor(mesh, dims, holeAxis);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int FinalAxis(int canonical, int holeAxis) => holeAxis switch
        {
            1 => canonical == 0 ? 0 : (canonical == 1 ? 2 : 1),
            0 => canonical == 1 ? 1 : (canonical == 0 ? 2 : 0),
            _ => canonical,
        };

        private static void ScaleUvToDecor(Mesh mesh, Vector3Int dims, int holeAxis)
        {
            float[] d =
            {
                Mathf.Max(1, dims.x), Mathf.Max(1, dims.y), Mathf.Max(1, dims.z),
            };
            var normals = mesh.normals;
            var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++)
            {
                var n = normals[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                int uAxis, vAxis;
                if (ax >= ay && ax >= az) { uAxis = 2; vAxis = 1; }
                else if (ay >= az) { uAxis = 0; vAxis = 2; }
                else { uAxis = 0; vAxis = 1; }

                uv[i] = new Vector2(
                    uv[i].x * d[FinalAxis(uAxis, holeAxis)] / d[0],
                    uv[i].y * d[FinalAxis(vAxis, holeAxis)] / d[1]);
            }
            mesh.SetUVs(0, uv);
        }

        public static Material GrooveMaterial()
        {
            if (_grooveMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                var color = new Color(0.18f, 0.18f, 0.20f, 1f);
                _grooveMat = new Material(shader);
                _grooveMat.SetColor("_BaseColor", color);
                _grooveMat.color = color;
            }
            return _grooveMat!;
        }

        private static List<float> AxisCuts(List<Rect2> rects, List<Rect2> holes, bool horizontal)
        {
            var cuts = new List<float> { -0.5f, 0.5f };
            foreach (var r in rects)
            {
                cuts.Add(horizontal ? r.xMin : r.yMin);
                cuts.Add(horizontal ? r.xMax : r.yMax);
            }
            foreach (var r in holes)
            {
                cuts.Add(horizontal ? r.xMin : r.yMin);
                cuts.Add(horizontal ? r.xMax : r.yMax);
            }
            cuts.Sort();

            var result = new List<float>();
            foreach (var c in cuts)
            {
                float v = Mathf.Clamp(c, -0.5f, 0.5f);
                if (result.Count == 0 || v - result[result.Count - 1] > 1e-5f)
                    result.Add(v);
            }
            if (result.Count < 2) result = new List<float> { -0.5f, 0.5f };
            return result;
        }

        private static bool IsInside(List<Rect2> rects, float x, float y)
        {
            foreach (var r in rects)
                if (x > r.xMin && x < r.xMax && y > r.yMin && y < r.yMax)
                    return true;
            return false;
        }

        private enum UvPlane { XY, ZY, XZ }

        private static Vector2 Uv(Vector3 v, UvPlane plane) => plane switch
        {
            UvPlane.XY => new Vector2(v.x + 0.5f, v.y + 0.5f),
            UvPlane.ZY => new Vector2(v.z + 0.5f, v.y + 0.5f),
            _ => new Vector2(v.x + 0.5f, v.z + 0.5f),
        };

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            UvPlane plane, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(Uv(a, plane)); uvs.Add(Uv(b, plane));
            uvs.Add(Uv(c, plane)); uvs.Add(Uv(d, plane));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
