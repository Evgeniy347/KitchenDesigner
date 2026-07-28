using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Плоский прямоугольник с прямоугольными дырами — геометрия одной
    /// накладки текстуры. Дыры это проёмы окон и дверей: накладка на стене с
    /// окном обязана иметь в этом месте дырку, иначе она затягивает проём
    /// плёнкой.
    ///
    /// Работает в системе координат ГРАНИ: миллиметры от левого нижнего угла,
    /// ось X = <c>Face.rightAxis</c>, ось Y = <c>Face.upAxis</c>. Меш выдаётся в
    /// юнитах и центрирован на середине области, поэтому объект-носитель ставится
    /// в центр области с поворотом <c>LookRotation(face.normal, face.upAxis)</c>
    /// и единичным масштабом.
    ///
    /// UV привязаны к НАЧАЛУ ГРАНИ, а не к области: <c>uv = мм / плитка_мм</c>.
    /// Из-за этого перемещение и растяжение области двигают «окно» по неподвижному
    /// рисунку — ровно то, что значит «изменение области отображения текстуры,
    /// а не ресайз картинки». Картинка при этом повторяется и обрезается, как
    /// обычный декор (см. MaterialManager.ComputeTileST).</summary>
    public static class PlaneWithHolesMesh
    {
        /// <summary>Ячейка тоньше этого просто не строится. Полмиллиметра — общий
        /// геометрический эпсилон проекта: меньше любого осмысленного размера и
        /// заведомо больше ошибки float на масштабе комнаты.</summary>
        public const float MinCellMM = 0.5f;

        /// <summary>Прямоугольник области в мм, дыры в тех же координатах грани.
        /// tileMM — физический размер плитки декора (0 и меньше → 1, чтобы UV не
        /// улетели в бесконечность на чисто цветовом декоре).
        /// Возвращает null, если рисовать нечего (пустая область или она целиком
        /// накрыта проёмом).</summary>
        public static Mesh? Build(RectInt rectMM, IReadOnlyList<RectInt>? holesMM, Vector2Int tileMM)
        {
            if (rectMM.width < MinCellMM || rectMM.height < MinCellMM) return null;

            float x0 = rectMM.xMin, x1 = rectMM.xMax;
            float y0 = rectMM.yMin, y1 = rectMM.yMax;

            var xSplits = new List<float> { x0, x1 };
            var ySplits = new List<float> { y0, y1 };

            if (holesMM != null)
                foreach (var h in holesMM)
                {
                    if (h.xMin > x0 && h.xMin < x1) xSplits.Add(h.xMin);
                    if (h.xMax > x0 && h.xMax < x1) xSplits.Add(h.xMax);
                    if (h.yMin > y0 && h.yMin < y1) ySplits.Add(h.yMin);
                    if (h.yMax > y0 && h.yMax < y1) ySplits.Add(h.yMax);
                }

            xSplits.Sort();
            ySplits.Sort();
            // Тот же приём, что у стены: почти совпадающие границы проёмов
            // сливаются ДО нарезки, иначе между ними остаётся ячейка-волосок.
            WallMeshBuilder.CollapseNearDuplicates(xSplits, MinCellMM);
            WallMeshBuilder.CollapseNearDuplicates(ySplits, MinCellMM);

            float cx = (x0 + x1) * 0.5f;
            float cy = (y0 + y1) * 0.5f;
            float tw = Mathf.Max(1, tileMM.x);
            float th = Mathf.Max(1, tileMM.y);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < xSplits.Count - 1; i++)
            {
                float xs = xSplits[i], xe = xSplits[i + 1];
                if (xe - xs < MinCellMM) continue;
                for (int j = 0; j < ySplits.Count - 1; j++)
                {
                    float ys = ySplits[j], ye = ySplits[j + 1];
                    if (ye - ys < MinCellMM) continue;
                    if (InsideAnyHole(holesMM, (xs + xe) * 0.5f, (ys + ye) * 0.5f)) continue;

                    int b = verts.Count;
                    AddVertex(verts, uvs, xs, ys, cx, cy, tw, th);
                    AddVertex(verts, uvs, xe, ys, cx, cy, tw, th);
                    AddVertex(verts, uvs, xe, ye, cx, cy, tw, th);
                    AddVertex(verts, uvs, xs, ye, cx, cy, tw, th);
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            if (tris.Count == 0) return null;

            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;

            var mesh = new Mesh { name = "TextureOverlay", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Вершина в юнитах от центра области + UV от начала грани.</summary>
        private static void AddVertex(List<Vector3> verts, List<Vector2> uvs,
            float xMM, float yMM, float cxMM, float cyMM, float tileW, float tileH)
        {
            verts.Add(new Vector3((xMM - cxMM) * AppConstants.MM_TO_UNITS,
                (yMM - cyMM) * AppConstants.MM_TO_UNITS, 0f));
            uvs.Add(new Vector2(xMM / tileW, yMM / tileH));
        }

        private static bool InsideAnyHole(IReadOnlyList<RectInt>? holes, float px, float py)
        {
            if (holes == null) return false;
            foreach (var h in holes)
                if (px > h.xMin && px < h.xMax && py > h.yMin && py < h.yMax) return true;
            return false;
        }
    }
}
