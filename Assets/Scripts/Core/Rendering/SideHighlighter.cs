using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SideHighlighter
    {
        public const float BandFraction = PartHighlightBands.BandFraction;

        public const float BandMaxMm = PartHighlightBands.BandMaxMM;

        public const float LiftMm = HighlightOverlay.LiftMm;

        internal const string PrimaryShaderName = HighlightOverlay.PrimaryShaderName;
        internal const string PrimaryShaderResourcePath = HighlightOverlay.PrimaryShaderResourcePath;

        internal static string[] FallbackShaderNames => HighlightOverlay.FallbackShaderNames;

        public static System.Func<Material?>? MaterialFactory
        {
            get => HighlightOverlay.MaterialFactory;
            set => HighlightOverlay.MaterialFactory = value;
        }

        private static EdgeSide? _shownEdgeSide;
        private static int _shownFaceIndex = -1;
        private static bool _shownBands;

        public static bool IsShown(KitchenElement element, EdgeSide side) =>
            Owns(element) && _shownEdgeSide == side;

        public static bool IsFaceShown(KitchenElement element, int faceIndex) =>
            Owns(element) && _shownFaceIndex == faceIndex && !_shownBands;

        public static bool IsGapSideShown(KitchenElement element, GapSide side) =>
            Owns(element) && _shownEdgeSide == null && _shownBands
            && _shownFaceIndex == GapSides.FaceIndex(side);

        public static int QuadCount => HighlightOverlay.PieceCount;

        public static IReadOnlyList<GameObject> QuadObjects => HighlightOverlay.PieceObjects;

        public static void ShowEdgeSide(KitchenElement element, EdgeSide side)
        {
            Hide();
            if (element == null) return;

            var layout = EdgeBanding.LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return;

            if (!Build(element, layout.FaceIndex(side), bands: true)) return;
            _shownEdgeSide = side;
        }

        public static void ShowGapSide(KitchenElement element, GapSide side)
        {
            Hide();
            Build(element, GapSides.FaceIndex(side), bands: true);
        }

        public static void ShowFace(KitchenElement element, int faceIndex)
        {
            Hide();
            Build(element, faceIndex, bands: false);
        }

        public static void Sync() => HighlightOverlay.Sync();

        public static void Hide() => HighlightOverlay.Hide();

        internal static int OppositeFaceOf(int faceIndex) =>
            faceIndex % 2 == 0 ? faceIndex + 1 : faceIndex - 1;

        public static float BandDepthOn(float faceSpanUnits) =>
            Mathf.Min(faceSpanUnits * BandFraction, BandMaxMm * AppConstants.MM_TO_UNITS);

        internal static Transform Root() => HighlightOverlay.Root();

        internal static Mesh QuadMesh() => HighlightOverlay.QuadMesh();

        internal static Material? HighlightMaterial() => HighlightOverlay.HighlightMaterial();

        internal static void MakeSeeThrough(Material m) => HighlightOverlay.MakeSeeThrough(m);

        internal static Shader? FindHighlightShader() => HighlightOverlay.FindHighlightShader();

        private static bool Owns(KitchenElement element) =>
            element != null && HighlightOverlay.ShownFor == element
            && HighlightOverlay.PieceCount > 0;

        private static bool Build(KitchenElement element, int faceIndex, bool bands)
        {
            if (element == null) return false;
            if (HighlightOverlay.HighlightMaterial() == null) return false;

            var faces = element.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return false;

            var face = faces[faceIndex];
            HighlightOverlay.AddQuad(face, face.size, Vector2.zero);

            if (bands)
            {
                int opposite = OppositeFaceOf(faceIndex);
                for (int i = 0; i < faces.Length; i++)
                {
                    if (i == faceIndex || i == opposite) continue;
                    AddBand(faces[i], face);
                }
            }

            _shownFaceIndex = faceIndex;
            _shownBands = bands;
            _shownEdgeSide = null;
            HighlightOverlay.Begin(element, () => Rebuild(element, faceIndex, bands), Forget);
            return true;
        }

        private static void Rebuild(KitchenElement element, int faceIndex, bool bands)
        {
            var side = _shownEdgeSide;
            Hide();
            if (Build(element, faceIndex, bands)) _shownEdgeSide = side;
        }

        private static void Forget()
        {
            _shownFaceIndex = -1;
            _shownBands = false;
            _shownEdgeSide = null;
        }

        private static void AddBand(in Face face, in Face end)
        {
            float alongRight = Vector3.Dot(end.normal, face.rightAxis);
            float alongUp = Vector3.Dot(end.normal, face.upAxis);
            bool awayFromEndIsU = Mathf.Abs(alongRight) >= Mathf.Abs(alongUp);

            float faceSpan = awayFromEndIsU ? face.size.x : face.size.y;
            float depth = BandDepthOn(faceSpan);
            if (depth <= 0f) return;

            float towardsEnd = awayFromEndIsU ? Mathf.Sign(alongRight) : Mathf.Sign(alongUp);
            float offset = (faceSpan - depth) * 0.5f * towardsEnd;

            var size = awayFromEndIsU
                ? new Vector2(depth, face.size.y)
                : new Vector2(face.size.x, depth);
            var shift = awayFromEndIsU ? new Vector2(offset, 0f) : new Vector2(0f, offset);
            HighlightOverlay.AddQuad(face, size, shift);
        }
    }
}
