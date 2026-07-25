using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Процедурная геометрия ящика GTV AXIS PRO: 2 металлические боковины
    /// (14 мм), дно и задник (плита 16 мм). Раскрой — версия 1 каталога (брошюра
    /// «Преимущества», стр. 8): задник во всю высоту (до низа), дно упирается в него.
    /// Фасад НЕ входит — он отдельный элемент.
    ///
    /// Меш строится в НОРМАЛИЗОВАННОМ кубе [−0.5,0.5] и масштабируется localScale
    /// элемента = КОНТУРНЫЙ бокс LW × минПроём × NL (зазоры направляющих 37.5 мм
    /// на сторону и подъём короба входят в бокс). Поэтому контур (чёрные рёбра),
    /// снэп и коллайдер работают по проёму корпуса, а видимый короб — внутри него.</summary>
    public static class DrawerMesh
    {
        /// <summary>Панель короба: позиция и размер в мм от левого-нижнего-ЗАДНЕГО
        /// угла контурного бокса (ящик выезжает в +Z, перед — z = NL).</summary>
        public struct Box
        {
            public string name;
            public Vector3 minMM;
            public Vector3 sizeMM;
        }

        /// <summary>Раскладка короба на 4 панели по формулам каталога.</summary>
        public static List<Box> ComputeBoxes(int lwMM, DrawerType type, int nlMM)
        {
            float w = lwMM, d = nlMM;
            float side = DrawerConstants.SIDE_WALL_THICKNESS;
            float clr = DrawerConstants.SLIDE_CLEARANCE_PER_SIDE;
            float lift = DrawerConstants.GetBottomLift(type);
            float h = DrawerConstants.GetTypeHeight(type);
            float panel = DrawerConstants.PANEL_THICKNESS;
            float backH = DrawerConstants.GetBackHeight(type);
            float backRear = DrawerConstants.BACK_REAR_OFFSET;
            float bottomW = w - DrawerConstants.BOTTOM_WIDTH_INSET;
            float bottomD = d - DrawerConstants.BOTTOM_DEPTH_INSET;
            float backW = w - DrawerConstants.BACK_WIDTH_INSET;

            return new List<Box>
            {
                // Боковины: внутренняя грань на 37.5 от стенки проёма, растут наружу.
                new Box
                {
                    name = "Боковина L",
                    minMM = new Vector3(clr - side, lift, 0f),
                    sizeMM = new Vector3(side, h, d),
                },
                new Box
                {
                    name = "Боковина R",
                    minMM = new Vector3(w - clr, lift, 0f),
                    sizeMM = new Vector3(side, h, d),
                },
                // Дно: (LW−75) × (NL−24), заподлицо с передним торцом боковин,
                // сзади упирается в задник.
                new Box
                {
                    name = "Дно",
                    minMM = new Vector3(clr, lift, d - bottomD),
                    sizeMM = new Vector3(bottomW, panel, bottomD),
                },
                // Задник: (LW−87), до низа короба, задняя грань на 8 мм от торца.
                new Box
                {
                    name = "Задник",
                    minMM = new Vector3((w - backW) * 0.5f, lift, backRear),
                    sizeMM = new Vector3(backW, backH, panel),
                },
            };
        }

        /// <summary>Меш короба (один сабмеш — цвет GTV через MaterialManager).</summary>
        public static Mesh Build(int lwMM, DrawerType type, int nlMM)
        {
            var dims = new Vector3(
                Mathf.Max(1, lwMM),
                Mathf.Max(1, DrawerConstants.GetMinOpeningHeight(type)),
                Mathf.Max(1, nlMM));
            return BuildFromBoxes(ComputeBoxes(lwMM, type, nlMM), dims);
        }

        /// <summary>Собрать меш из панелей короба. Панели заданы в мм от угла
        /// контурного бокса; меш строится в нормализованном кубе [−0.5,0.5] и
        /// масштабируется localScale элемента = контурный бокс. Переиспользуется
        /// раскроем GTV и Movento — геометрия панелей у них разная, сборка одна.</summary>
        public static Mesh BuildFromBoxes(List<Box> boxes, Vector3 contourDimsMM)
        {
            var dims = new Vector3(
                Mathf.Max(1f, contourDimsMM.x),
                Mathf.Max(1f, contourDimsMM.y),
                Mathf.Max(1f, contourDimsMM.z));

            var verts = new List<Vector3>();
            var tris = new List<int>();

            foreach (var box in boxes)
            {
                // мм → нормализованные координаты контурного бокса.
                Vector3 center = box.minMM + box.sizeMM * 0.5f;
                Vector3 c = new Vector3(
                    center.x / dims.x - 0.5f,
                    center.y / dims.y - 0.5f,
                    center.z / dims.z - 0.5f);
                Vector3 s = new Vector3(
                    box.sizeMM.x / dims.x,
                    box.sizeMM.y / dims.y,
                    box.sizeMM.z / dims.z);
                AddBox(verts, tris, c, s);
            }

            var mesh = new Mesh { name = "DrawerBox" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Коробка с плоскими гранями (как AssembledFacadeMesh.AddBox).</summary>
        private static void AddBox(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 size)
        {
            float x0 = center.x - size.x * 0.5f, x1 = center.x + size.x * 0.5f;
            float y0 = center.y - size.y * 0.5f, y1 = center.y + size.y * 0.5f;
            float z0 = center.z - size.z * 0.5f, z1 = center.z + size.z * 0.5f;

            // +Z, -Z, +X, -X, +Y, -Y — каждая грань CCW снаружи.
            AddQuad(verts, tris, V(x0, y0, z1), V(x1, y0, z1), V(x1, y1, z1), V(x0, y1, z1));
            AddQuad(verts, tris, V(x1, y0, z0), V(x0, y0, z0), V(x0, y1, z0), V(x1, y1, z0));
            AddQuad(verts, tris, V(x1, y0, z1), V(x1, y0, z0), V(x1, y1, z0), V(x1, y1, z1));
            AddQuad(verts, tris, V(x0, y0, z0), V(x0, y0, z1), V(x0, y1, z1), V(x0, y1, z0));
            AddQuad(verts, tris, V(x0, y1, z1), V(x1, y1, z1), V(x1, y1, z0), V(x0, y1, z0));
            AddQuad(verts, tris, V(x0, y0, z0), V(x1, y0, z0), V(x1, y0, z1), V(x0, y0, z1));
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static void AddQuad(List<Vector3> verts, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
