using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum OverlaySide
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3,
        E = 4,
        F = 5,
        All = 6,
    }

    [System.Serializable]
    public struct TextureOverlaySpec : System.IEquatable<TextureOverlaySpec>
    {
        public const int MIN_SIZE_MM = 10;

        public OverlaySide side;
        public string materialId;
        public int u0MM, v0MM;
        public int widthMM, heightMM;

        public TextureOverlaySpec(OverlaySide side, string? materialId,
            int u0MM = 0, int v0MM = 0, int widthMM = 0, int heightMM = 0)
        {
            this.side = side;
            this.materialId = string.IsNullOrEmpty(materialId) ? MaterialCatalog.DefaultId : materialId!;
            this.u0MM = u0MM;
            this.v0MM = v0MM;
            this.widthMM = widthMM;
            this.heightMM = heightMM;
        }

        public static TextureOverlaySpec FullFace(OverlaySide side, string? materialId)
            => new TextureOverlaySpec(side, materialId);

        public bool IsFullFace => widthMM <= 0 || heightMM <= 0;

        public string MaterialId =>
            string.IsNullOrEmpty(materialId) ? MaterialCatalog.DefaultId : materialId;

        public RectInt Resolve(Vector2Int faceMM)
        {
            int fw = Mathf.Max(0, faceMM.x);
            int fh = Mathf.Max(0, faceMM.y);
            if (IsFullFace) return new RectInt(0, 0, fw, fh);

            int x0 = Mathf.Clamp(u0MM, 0, fw);
            int y0 = Mathf.Clamp(v0MM, 0, fh);
            int x1 = Mathf.Clamp(u0MM + widthMM, 0, fw);
            int y1 = Mathf.Clamp(v0MM + heightMM, 0, fh);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        public TextureOverlaySpec WithRect(RectInt rect)
            => new TextureOverlaySpec(side, materialId, rect.x, rect.y,
                Mathf.Max(MIN_SIZE_MM, rect.width), Mathf.Max(MIN_SIZE_MM, rect.height));

        public TextureOverlaySpec WithSide(OverlaySide newSide)
            => new TextureOverlaySpec(newSide, materialId, u0MM, v0MM, widthMM, heightMM);

        public TextureOverlaySpec WithMaterial(string? newMaterialId)
            => new TextureOverlaySpec(side, newMaterialId, u0MM, v0MM, widthMM, heightMM);

        public static string SideLabel(OverlaySide side) => side switch
        {
            OverlaySide.A => "A",
            OverlaySide.B => "B",
            OverlaySide.C => "C",
            OverlaySide.D => "D",
            OverlaySide.E => "E",
            OverlaySide.F => "F",
            _ => "(все)",
        };

        public override string ToString() =>
            $"{SideLabel(side)}:{MaterialId}" + (IsFullFace ? "" : $"@{u0MM},{v0MM}+{widthMM}×{heightMM}");

        public bool Equals(TextureOverlaySpec other) =>
            side == other.side && MaterialId == other.MaterialId
            && u0MM == other.u0MM && v0MM == other.v0MM
            && widthMM == other.widthMM && heightMM == other.heightMM;

        public override bool Equals(object? obj) => obj is TextureOverlaySpec other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)side;
                h = h * 31 + MaterialId.GetHashCode();
                h = h * 31 + u0MM;
                h = h * 31 + v0MM;
                h = h * 31 + widthMM;
                h = h * 31 + heightMM;
                return h;
            }
        }
    }

    public static class TextureOverlayGeometry
    {
        public const int MAX_PER_ELEMENT = 12;

        public const int FACE_COUNT = 6;

        public static int[] FaceIndices(OverlaySide side) =>
            side == OverlaySide.All
                ? new[] { 0, 1, 2, 3, 4, 5 }
                : new[] { (int)side };

        public static Vector2Int FaceSizeMM(Vector3Int dimsMM, int faceIndex) => (faceIndex / 2) switch
        {
            0 => new Vector2Int(dimsMM.y, dimsMM.z),
            1 => new Vector2Int(dimsMM.x, dimsMM.z),
            _ => new Vector2Int(dimsMM.x, dimsMM.y),
        };
    }
}
