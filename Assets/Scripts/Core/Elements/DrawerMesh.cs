using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DrawerMesh
    {
        public struct Box
        {
            public string name;
            public Vector3 minMM;
            public Vector3 sizeMM;
        }

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
                new Box
                {
                    name = "Дно",
                    minMM = new Vector3(clr, lift, d - bottomD),
                    sizeMM = new Vector3(bottomW, panel, bottomD),
                },
                new Box
                {
                    name = "Задник",
                    minMM = new Vector3((w - backW) * 0.5f, lift, backRear),
                    sizeMM = new Vector3(backW, backH, panel),
                },
            };
        }

        public static Mesh Build(int lwMM, DrawerType type, int nlMM)
        {
            var dims = new Vector3(
                Mathf.Max(1, lwMM),
                Mathf.Max(1, DrawerConstants.GetMinOpeningHeight(type)),
                Mathf.Max(1, nlMM));
            return BuildFromBoxes(ComputeBoxes(lwMM, type, nlMM), dims);
        }

        public static Mesh BuildFromBoxes(List<Box> boxes, Vector3 contourDimsMM)
        {
            var dims = new Vector3(
                Mathf.Max(1f, contourDimsMM.x),
                Mathf.Max(1f, contourDimsMM.y),
                Mathf.Max(1f, contourDimsMM.z));

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            foreach (var box in boxes)
            {
                Vector3 center = box.minMM + box.sizeMM * 0.5f;
                Vector3 c = new Vector3(
                    center.x / dims.x - 0.5f,
                    center.y / dims.y - 0.5f,
                    center.z / dims.z - 0.5f);
                Vector3 s = new Vector3(
                    box.sizeMM.x / dims.x,
                    box.sizeMM.y / dims.y,
                    box.sizeMM.z / dims.z);
                AddBox(verts, uvs, tris, c, s);
            }

            var mesh = new Mesh { name = "DrawerBox" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 size)
        {
            float x0 = center.x - size.x * 0.5f, x1 = center.x + size.x * 0.5f;
            float y0 = center.y - size.y * 0.5f, y1 = center.y + size.y * 0.5f;
            float z0 = center.z - size.z * 0.5f, z1 = center.z + size.z * 0.5f;

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud) =>
                AddQuad(verts, uvs, tris, a, b, c, d, ua, ub, uc, ud);

            Quad(V(x0, y0, z1), V(x1, y0, z1), V(x1, y1, z1), V(x0, y1, z1),
                new Vector2(x0 + 0.5f, y0 + 0.5f), new Vector2(x1 + 0.5f, y0 + 0.5f),
                new Vector2(x1 + 0.5f, y1 + 0.5f), new Vector2(x0 + 0.5f, y1 + 0.5f));
            Quad(V(x1, y0, z0), V(x0, y0, z0), V(x0, y1, z0), V(x1, y1, z0),
                new Vector2(x1 + 0.5f, y0 + 0.5f), new Vector2(x0 + 0.5f, y0 + 0.5f),
                new Vector2(x0 + 0.5f, y1 + 0.5f), new Vector2(x1 + 0.5f, y1 + 0.5f));
            Quad(V(x1, y0, z1), V(x1, y0, z0), V(x1, y1, z0), V(x1, y1, z1),
                new Vector2(z1 + 0.5f, y0 + 0.5f), new Vector2(z0 + 0.5f, y0 + 0.5f),
                new Vector2(z0 + 0.5f, y1 + 0.5f), new Vector2(z1 + 0.5f, y1 + 0.5f));
            Quad(V(x0, y0, z0), V(x0, y0, z1), V(x0, y1, z1), V(x0, y1, z0),
                new Vector2(z0 + 0.5f, y0 + 0.5f), new Vector2(z1 + 0.5f, y0 + 0.5f),
                new Vector2(z1 + 0.5f, y1 + 0.5f), new Vector2(z0 + 0.5f, y1 + 0.5f));
            Quad(V(x0, y1, z1), V(x1, y1, z1), V(x1, y1, z0), V(x0, y1, z0),
                new Vector2(x0 + 0.5f, z1 + 0.5f), new Vector2(x1 + 0.5f, z1 + 0.5f),
                new Vector2(x1 + 0.5f, z0 + 0.5f), new Vector2(x0 + 0.5f, z0 + 0.5f));
            Quad(V(x0, y0, z0), V(x1, y0, z0), V(x1, y0, z1), V(x0, y0, z1),
                new Vector2(x0 + 0.5f, z0 + 0.5f), new Vector2(x1 + 0.5f, z0 + 0.5f),
                new Vector2(x1 + 0.5f, z1 + 0.5f), new Vector2(x0 + 0.5f, z1 + 0.5f));
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
