using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PlaneWithHolesMesh
    {
        public const float MinCellMM = 0.5f;

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
