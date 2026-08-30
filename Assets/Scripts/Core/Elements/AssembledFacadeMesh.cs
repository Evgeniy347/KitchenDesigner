using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum AssembledFill
    {
        Blind = 0,
        Glass = 1,
        Open = 2,
    }

    public static class AssembledFacadeMesh
    {
        public static float Fraction(int sideMM, int frameMM)
            => Mathf.Clamp((float)frameMM / Mathf.Max(1, sideMM), 0.02f, 0.48f);

        public struct Part
        {
            public string suffix;
            public Vector3Int dimsMM;
            public string materialKind;
        }

        public static List<Part> ComputeParts(Vector3Int dims, AssembledFill fill,
            int frameMM, int glassDeductMM)
        {
            int L = dims.x, H = dims.y, T = dims.z, A = frameMM;
            int B = Mathf.Max(1, L - 2 * A);
            int C = Mathf.Max(1, H - 2 * A);

            var parts = new List<Part>
            {
                new Part { suffix = "Стойка",      dimsMM = new Vector3Int(A, H, T) },
                new Part { suffix = "Стойка",      dimsMM = new Vector3Int(A, H, T) },
                new Part { suffix = "Перекладина", dimsMM = new Vector3Int(B, A, T) },
                new Part { suffix = "Перекладина", dimsMM = new Vector3Int(B, A, T) },
            };

            if (fill == AssembledFill.Blind)
                parts.Add(new Part { suffix = "Панель", dimsMM = new Vector3Int(B, C, T) });
            else if (fill == AssembledFill.Glass)
                parts.Add(new Part
                {
                    suffix = "Стекло",
                    dimsMM = new Vector3Int(
                        Mathf.Max(1, L - glassDeductMM),
                        Mathf.Max(1, H - glassDeductMM),
                        AppConstants.ASSEMBLED_GLASS_THICKNESS_MM),
                    materialKind = "Стекло"
                });
            return parts;
        }

        public static Mesh Build(Vector3Int dims, AssembledFill fill, int grooveCount, int frameMM)
        {
            float fx = Fraction(dims.x, frameMM);
            float fy = Fraction(dims.y, frameMM);
            float innerHalfWidth = 0.5f - fx;
            float innerHalfHeight = 0.5f - fy;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var frame = new List<int>();
            var grooves = new List<int>();

            AddBox(verts, uvs, frame, new Vector3(-(0.5f - fx / 2f), 0f, 0f), new Vector3(fx, 1f, 1f));
            AddBox(verts, uvs, frame, new Vector3(0.5f - fx / 2f, 0f, 0f), new Vector3(fx, 1f, 1f));

            float railW = 2f * innerHalfWidth;
            float gw = Mathf.Min(railW * 0.4f, (float)AppConstants.ASSEMBLED_GROOVE_MM / Mathf.Max(1, dims.x));
            float gd = Mathf.Min(0.9f, (float)AppConstants.ASSEMBLED_GROOVE_MM / Mathf.Max(1, dims.z));
            float[] grooveX = grooveCount > 0
                ? new[] { -(innerHalfWidth - gw / 2f), innerHalfWidth - gw / 2f }
                : System.Array.Empty<float>();
            AddGroovedRail(verts, uvs, frame, grooves,
                new Vector3(0f, 0.5f - fy / 2f, 0f), new Vector3(railW, fy, 1f), grooveX, gw, gd);
            AddGroovedRail(verts, uvs, frame, grooves,
                new Vector3(0f, -(0.5f - fy / 2f), 0f), new Vector3(railW, fy, 1f), grooveX, gw, gd);

            if (fill == AssembledFill.Blind)
            {
                const float panelThickness = 0.5f;
                AddBox(verts, uvs, frame, new Vector3(0f, 0f, -(0.5f - panelThickness / 2f)),
                    new Vector3(2f * innerHalfWidth, 2f * innerHalfHeight, panelThickness));
            }

            var mesh = new Mesh { name = "AssembledFacade" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(frame, 0);
            mesh.SetTriangles(grooves, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.SetUVs(0, uvs);
            return mesh;
        }

        private static void AddBox(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 half)
        {
            float x0 = center.x - half.x * 0.5f, x1 = center.x + half.x * 0.5f;
            float y0 = center.y - half.y * 0.5f, y1 = center.y + half.y * 0.5f;
            float z0 = center.z - half.z * 0.5f, z1 = center.z + half.z * 0.5f;

            AddQuad(verts, uvs, tris, V(x0,y0,z1), V(x1,y0,z1), V(x1,y1,z1), V(x0,y1,z1),
                Uv(V(x0,y0,z1)), Uv(V(x1,y0,z1)), Uv(V(x1,y1,z1)), Uv(V(x0,y1,z1)));
            AddQuad(verts, uvs, tris, V(x1,y0,z0), V(x0,y0,z0), V(x0,y1,z0), V(x1,y1,z0),
                Uv(V(x1,y0,z0)), Uv(V(x0,y0,z0)), Uv(V(x0,y1,z0)), Uv(V(x1,y1,z0)));
            AddQuad(verts, uvs, tris, V(x1,y0,z1), V(x1,y0,z0), V(x1,y1,z0), V(x1,y1,z1),
                UvX(V(x1,y0,z1)), UvX(V(x1,y0,z0)), UvX(V(x1,y1,z0)), UvX(V(x1,y1,z1)));
            AddQuad(verts, uvs, tris, V(x0,y0,z0), V(x0,y0,z1), V(x0,y1,z1), V(x0,y1,z0),
                UvX(V(x0,y0,z0)), UvX(V(x0,y0,z1)), UvX(V(x0,y1,z1)), UvX(V(x0,y1,z0)));
            AddQuad(verts, uvs, tris, V(x0,y1,z1), V(x1,y1,z1), V(x1,y1,z0), V(x0,y1,z0),
                UvY(V(x0,y1,z1)), UvY(V(x1,y1,z1)), UvY(V(x1,y1,z0)), UvY(V(x0,y1,z0)));
            AddQuad(verts, uvs, tris, V(x0,y0,z0), V(x1,y0,z0), V(x1,y0,z1), V(x0,y0,z1),
                UvY(V(x0,y0,z0)), UvY(V(x1,y0,z0)), UvY(V(x1,y0,z1)), UvY(V(x0,y0,z1)));
        }

        private static void AddGroovedRail(List<Vector3> verts, List<Vector2> uvs,
            List<int> frame, List<int> grooves,
            Vector3 center, Vector3 size, float[] grooveX, float gw, float gd)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            float x0 = center.x - hx, x1 = center.x + hx;
            float y0 = center.y - hy, y1 = center.y + hy;
            float zb = center.z - hz, zf = center.z + hz;

            AddQuad(verts, uvs, frame, V(x1,y0,zb), V(x0,y0,zb), V(x0,y1,zb), V(x1,y1,zb),
                Uv(V(x1,y0,zb)), Uv(V(x0,y0,zb)), Uv(V(x0,y1,zb)), Uv(V(x1,y1,zb)));
            AddQuad(verts, uvs, frame, V(x1,y0,zf), V(x1,y0,zb), V(x1,y1,zb), V(x1,y1,zf),
                UvX(V(x1,y0,zf)), UvX(V(x1,y0,zb)), UvX(V(x1,y1,zb)), UvX(V(x1,y1,zf)));
            AddQuad(verts, uvs, frame, V(x0,y0,zb), V(x0,y0,zf), V(x0,y1,zf), V(x0,y1,zb),
                UvX(V(x0,y0,zb)), UvX(V(x0,y0,zf)), UvX(V(x0,y1,zf)), UvX(V(x0,y1,zb)));
            AddQuad(verts, uvs, frame, V(x0,y1,zf), V(x1,y1,zf), V(x1,y1,zb), V(x0,y1,zb),
                UvY(V(x0,y1,zf)), UvY(V(x1,y1,zf)), UvY(V(x1,y1,zb)), UvY(V(x0,y1,zb)));
            AddQuad(verts, uvs, frame, V(x0,y0,zb), V(x1,y0,zb), V(x1,y0,zf), V(x0,y0,zf),
                UvY(V(x0,y0,zb)), UvY(V(x1,y0,zb)), UvY(V(x1,y0,zf)), UvY(V(x0,y0,zf)));

            if (grooveX == null || grooveX.Length == 0)
            {
                AddQuad(verts, uvs, frame, V(x0,y0,zf), V(x1,y0,zf), V(x1,y1,zf), V(x0,y1,zf),
                    Uv(V(x0,y0,zf)), Uv(V(x1,y0,zf)), Uv(V(x1,y1,zf)), Uv(V(x0,y1,zf)));
                return;
            }

            float m = (y1 - y0) * 0.15f;
            float gy0 = y0 + m, gy1 = y1 - m;
            float gz = zf - gd;

            AddQuad(verts, uvs, frame, V(x0,y0,zf), V(x1,y0,zf), V(x1,gy0,zf), V(x0,gy0,zf),
                Uv(V(x0,y0,zf)), Uv(V(x1,y0,zf)), Uv(V(x1,gy0,zf)), Uv(V(x0,gy0,zf)));
            AddQuad(verts, uvs, frame, V(x0,gy1,zf), V(x1,gy1,zf), V(x1,y1,zf), V(x0,y1,zf),
                Uv(V(x0,gy1,zf)), Uv(V(x1,gy1,zf)), Uv(V(x1,y1,zf)), Uv(V(x0,y1,zf)));

            var gs = (float[])grooveX.Clone();
            System.Array.Sort(gs);
            float cx = x0;
            foreach (var gcx in gs)
            {
                float ga = gcx - gw * 0.5f, gb = gcx + gw * 0.5f;
                if (ga > cx)
                    AddQuad(verts, uvs, frame, V(cx,gy0,zf), V(ga,gy0,zf), V(ga,gy1,zf), V(cx,gy1,zf),
                        Uv(V(cx,gy0,zf)), Uv(V(ga,gy0,zf)), Uv(V(ga,gy1,zf)), Uv(V(cx,gy1,zf)));

                AddQuad(verts, uvs, grooves, V(ga,gy0,gz), V(gb,gy0,gz), V(gb,gy1,gz), V(ga,gy1,gz),
                    Uv(V(ga,gy0,gz)), Uv(V(gb,gy0,gz)), Uv(V(gb,gy1,gz)), Uv(V(ga,gy1,gz)));
                AddQuad(verts, uvs, grooves, V(ga,gy0,zf), V(ga,gy0,gz), V(ga,gy1,gz), V(ga,gy1,zf),
                    UvX(V(ga,gy0,zf)), UvX(V(ga,gy0,gz)), UvX(V(ga,gy1,gz)), UvX(V(ga,gy1,zf)));
                AddQuad(verts, uvs, grooves, V(gb,gy0,gz), V(gb,gy0,zf), V(gb,gy1,zf), V(gb,gy1,gz),
                    UvX(V(gb,gy0,gz)), UvX(V(gb,gy0,zf)), UvX(V(gb,gy1,zf)), UvX(V(gb,gy1,gz)));
                AddQuad(verts, uvs, grooves, V(ga,gy0,zf), V(gb,gy0,zf), V(gb,gy0,gz), V(ga,gy0,gz),
                    UvY(V(ga,gy0,zf)), UvY(V(gb,gy0,zf)), UvY(V(gb,gy0,gz)), UvY(V(ga,gy0,gz)));
                AddQuad(verts, uvs, grooves, V(ga,gy1,gz), V(gb,gy1,gz), V(gb,gy1,zf), V(ga,gy1,zf),
                    UvY(V(ga,gy1,gz)), UvY(V(gb,gy1,gz)), UvY(V(gb,gy1,zf)), UvY(V(ga,gy1,zf)));
                cx = gb;
            }
            if (x1 > cx)
                AddQuad(verts, uvs, frame, V(cx,gy0,zf), V(x1,gy0,zf), V(x1,gy1,zf), V(cx,gy1,zf),
                    Uv(V(cx,gy0,zf)), Uv(V(x1,gy0,zf)), Uv(V(x1,gy1,zf)), Uv(V(cx,gy1,zf)));
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static Vector2 Uv(Vector3 v) => new Vector2(v.x + 0.5f, v.y + 0.5f);
        private static Vector2 UvX(Vector3 v) => new Vector2(v.z + 0.5f, v.y + 0.5f);
        private static Vector2 UvY(Vector3 v) => new Vector2(v.x + 0.5f, v.z + 0.5f);

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
