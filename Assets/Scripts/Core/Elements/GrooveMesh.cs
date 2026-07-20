using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Процедурная геометрия детали с пазами. Как AssembledFacadeMesh,
    /// строится в НОРМАЛИЗОВАННОМ кубе [−0.5,0.5] (масштабируется localScale детали),
    /// поэтому коллайдер/ручки/выделение остаются на полном коробе, а абсолютные
    /// размеры паза сохраняются при любом размере детали (доля = мм/сторона).
    ///
    /// Пазы режутся в ЛИЦЕВОЙ пласти (+Z) вдоль выбранной кромки. Лицевая грань
    /// тесселируется решёткой по границам пазов: клетка внутри паза уходит на дно
    /// кармана, остальные остаются на пласти; на переходах ставятся стенки. Сквозной
    /// паз выходит на торец — соответствующая боковая грань укорачивается до дна.
    /// Чистые функции — покрываются тестами.</summary>
    public static class GrooveMesh
    {
        private static Material? _grooveMat;

        /// <summary>Прямоугольник паза на лицевой грани в нормализованных
        /// координатах [−0.5,0.5]. Невалидный (нулевой) — паз не помещается.</summary>
        public struct Rect2
        {
            public float xMin, xMax, yMin, yMax;
            public bool IsValid => xMax > xMin && yMax > yMin;
        }

        /// <summary>Глубина паза как доля толщины детали (с защитой от «насквозь»).</summary>
        public static float DepthFraction(Vector3Int dims)
            => Mathf.Clamp((float)AppConstants.GROOVE_DEPTH_MM / Mathf.Max(1, dims.z), 0.01f, 0.9f);

        /// <summary>Границы одного паза на лицевой грани. Смещение отсчитывается
        /// от кромки указанной стороны внутрь; глухой укорочен с обоих торцов.</summary>
        public static Rect2 ComputeRect(Vector3Int dims, GrooveSpec spec)
        {
            int w = Mathf.Max(1, dims.x);
            int h = Mathf.Max(1, dims.y);
            int off = AppConstants.GROOVE_OFFSET_MM;
            int wid = AppConstants.GROOVE_WIDTH_MM;
            int end = AppConstants.GROOVE_BLIND_END_MM;

            bool horizontal = spec.side == GrooveSide.Top || spec.side == GrooveSide.Bottom;
            int across = horizontal ? h : w; // сторона, поперёк которой отмеряется смещение
            int along = horizontal ? w : h;  // сторона, вдоль которой идёт паз

            // Паз со смещением 16 + ширина 4 не помещается на узкой детали.
            if (off + wid >= across) return default;

            float a0, a1; // границы ВДОЛЬ направления паза
            if (spec.kind == GrooveKind.Blind)
            {
                if (2 * end >= along) return default; // на длину не осталось места
                float e = (float)end / along;
                a0 = -0.5f + e;
                a1 = 0.5f - e;
            }
            else
            {
                a0 = -0.5f;
                a1 = 0.5f;
            }

            float o = (float)off / across;
            float t = (float)wid / across;
            // Ближняя к кромке стенка паза лежит на расстоянии смещения от кромки.
            bool fromMax = spec.side == GrooveSide.Top || spec.side == GrooveSide.Right;
            float c0 = fromMax ? 0.5f - o - t : -0.5f + o;
            float c1 = fromMax ? 0.5f - o : -0.5f + o + t;

            return horizontal
                ? new Rect2 { xMin = a0, xMax = a1, yMin = c0, yMax = c1 }
                : new Rect2 { xMin = c0, xMax = c1, yMin = a0, yMax = a1 };
        }

        /// <summary>Границы всех помещающихся пазов детали.</summary>
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

        /// <summary>Меш детали с ДВУМЯ сабмешами: 0 — тело (декор), 1 — пазы
        /// (тёмный материал). Пустой список пазов даёт обычную коробку.</summary>
        public static Mesh Build(Vector3Int dims, IReadOnlyList<GrooveSpec>? grooves)
        {
            var rects = ComputeRects(dims, grooves);
            const float zf = 0.5f, zb = -0.5f;
            float gz = zf - DepthFraction(dims);

            var xs = AxisCuts(rects, horizontal: true);
            var ys = AxisCuts(rects, horizontal: false);
            int nx = xs.Count - 1, ny = ys.Count - 1;

            // Карта «клетка решётки лежит внутри паза» — по ней строятся дно
            // карманов, стенки и укорочение торцов. Пересечения пазов и любое
            // их количество обрабатываются одинаково.
            var inside = new bool[nx, ny];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    inside[i, j] = IsInside(rects,
                        (xs[i] + xs[i + 1]) * 0.5f, (ys[j] + ys[j + 1]) * 0.5f);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var body = new List<int>();
            var cut = new List<int>();

            // Лицевая грань: клетки вне пазов — на пласти, внутри — на дне кармана.
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    float x0 = xs[i], x1 = xs[i + 1], y0 = ys[j], y1 = ys[j + 1];
                    if (inside[i, j])
                        AddQuad(verts, uvs, cut, UvPlane.XY,
                            V(x0, y0, gz), V(x1, y0, gz), V(x1, y1, gz), V(x0, y1, gz));
                    else
                        AddQuad(verts, uvs, body, UvPlane.XY,
                            V(x0, y0, zf), V(x1, y0, zf), V(x1, y1, zf), V(x0, y1, zf));
                }
            }

            // Задняя грань сплошная: паз режет только пласть.
            AddQuad(verts, uvs, body, UvPlane.XY,
                V(0.5f, -0.5f, zb), V(-0.5f, -0.5f, zb), V(-0.5f, 0.5f, zb), V(0.5f, 0.5f, zb));

            // Стенки карманов на внутренних линиях решётки (нормаль — внутрь паза).
            for (int i = 1; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
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

            // Торцы: полосами по решётке. Там, где сквозной паз выходит наружу,
            // торец доходит только до дна кармана — паз остаётся открытым.
            for (int j = 0; j < ny; j++)
            {
                float y0 = ys[j], y1 = ys[j + 1];
                float zl = inside[0, j] ? gz : zf;
                AddQuad(verts, uvs, body, UvPlane.ZY,
                    V(-0.5f, y0, zb), V(-0.5f, y0, zl), V(-0.5f, y1, zl), V(-0.5f, y1, zb));
                float zr = inside[nx - 1, j] ? gz : zf;
                AddQuad(verts, uvs, body, UvPlane.ZY,
                    V(0.5f, y0, zr), V(0.5f, y0, zb), V(0.5f, y1, zb), V(0.5f, y1, zr));
            }
            for (int i = 0; i < nx; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                float zd = inside[i, 0] ? gz : zf;
                AddQuad(verts, uvs, body, UvPlane.XZ,
                    V(x0, -0.5f, zb), V(x1, -0.5f, zb), V(x1, -0.5f, zd), V(x0, -0.5f, zd));
                float zu = inside[i, ny - 1] ? gz : zf;
                AddQuad(verts, uvs, body, UvPlane.XZ,
                    V(x0, 0.5f, zu), V(x1, 0.5f, zu), V(x1, 0.5f, zb), V(x0, 0.5f, zb));
            }

            var mesh = new Mesh { name = "PartWithGrooves" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(cut, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Тёмный материал дна и стенок паза (общий на все детали).</summary>
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

        // ── Решётка ────────────────────────────────────────────────────

        /// <summary>Отсортированные без дублей линии реза по оси: края детали
        /// плюс границы всех пазов.</summary>
        private static List<float> AxisCuts(List<Rect2> rects, bool horizontal)
        {
            var cuts = new List<float> { -0.5f, 0.5f };
            foreach (var r in rects)
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
            // Вырожденный случай (деталь без пазов) — минимум одна клетка.
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

        // ── Примитивы ──────────────────────────────────────────────────

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
