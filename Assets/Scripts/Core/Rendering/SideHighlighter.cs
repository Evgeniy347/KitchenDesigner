using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SideHighlighter
    {
        public const float BandFraction = 0.2f;

        public const float BandMaxMm = 50f;

        public const float LiftMm = 0.2f;

        internal const string PrimaryShaderName = "Hidden/OverlayLine";
        internal const string PrimaryShaderResourcePath = "Shaders/OverlayLine";

        internal static readonly string[] FallbackShaderNames =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
        };

        private static readonly List<GameObject> Quads = new List<GameObject>();
        private static Material? _material;
        private static GameObject? _root;
        private static KitchenElement? _shownFor;
        private static EdgeSide? _shownEdgeSide;
        private static int _shownFaceIndex = -1;
        private static bool _shownBands;
        private static Vector3 _shownPos;
        private static Quaternion _shownRot;
        private static Vector3Int _shownDims;

        public static bool IsShown(KitchenElement element, EdgeSide side) =>
            _shownFor == element && _shownEdgeSide == side && Quads.Count > 0;

        public static bool IsFaceShown(KitchenElement element, int faceIndex) =>
            _shownFor == element && _shownFaceIndex == faceIndex
            && !_shownBands && Quads.Count > 0;

        public static bool IsGapSideShown(KitchenElement element, GapSide side) =>
            _shownFor == element && _shownEdgeSide == null && _shownBands
            && _shownFaceIndex == GapSides.FaceIndex(side) && Quads.Count > 0;

        public static int QuadCount => Quads.Count;

        public static IReadOnlyList<GameObject> QuadObjects => Quads;

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

        public static void ShowFaceWithBands(KitchenElement element, int faceIndex)
        {
            Hide();
            Build(element, faceIndex, bands: true);
        }

        internal static int OppositeFaceOf(int faceIndex) =>
            faceIndex % 2 == 0 ? faceIndex + 1 : faceIndex - 1;

        private static bool Build(KitchenElement element, int faceIndex, bool bands)
        {
            if (element == null) return false;
            if (HighlightMaterial() == null) return false;

            var faces = element.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return false;

            var face = faces[faceIndex];
            AddQuad(face, face.size, Vector2.zero);

            if (bands)
            {
                int opposite = OppositeFaceOf(faceIndex);
                for (int i = 0; i < faces.Length; i++)
                {
                    if (i == faceIndex || i == opposite) continue;
                    AddBand(faces[i], face);
                }
            }

            _shownFor = element;
            _shownFaceIndex = faceIndex;
            _shownBands = bands;
            _shownEdgeSide = null;
            Remember(element);
            return true;
        }

        private static void Remember(KitchenElement element)
        {
            _shownPos = element.transform.position;
            _shownRot = element.transform.rotation;
            _shownDims = element.DimensionsMM;
        }

        public static void Sync()
        {
            if (Quads.Count == 0) return;

            if (_shownFor == null || !SceneVisibility.AnyRendererEnabled(_shownFor))
            {
                Hide();
                return;
            }

            var t = _shownFor.transform;
            if (t.position == _shownPos && t.rotation == _shownRot
                && _shownFor.DimensionsMM == _shownDims) return;

            var element = _shownFor;
            var side = _shownEdgeSide;
            int faceIndex = _shownFaceIndex;
            bool bands = _shownBands;
            Hide();
            if (Build(element, faceIndex, bands)) _shownEdgeSide = side;
        }

        public static void Hide()
        {
            foreach (var q in Quads)
                if (q != null) DestroyNow(q);
            Quads.Clear();
            _shownFor = null;
            _shownFaceIndex = -1;
            _shownBands = false;
            _shownEdgeSide = null;
        }

        private static void DestroyNow(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        internal static Transform Root()
        {
            if (_root == null)
            {
                _root = new GameObject("__EdgeSideHighlight") { hideFlags = HideFlags.DontSave };
                _root.transform.SetParent(null, worldPositionStays: false);
            }
            return _root.transform;
        }

        public static float BandDepthOn(float faceSpanUnits) =>
            Mathf.Min(faceSpanUnits * BandFraction, BandMaxMm * AppConstants.MM_TO_UNITS);

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
            AddQuad(face, size, shift);
        }

        private static void AddQuad(in Face face, Vector2 size, Vector2 shiftInFace)
        {
            var go = new GameObject("EdgeSideHighlight");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(Root(), worldPositionStays: false);

            go.transform.position = face.center
                + face.rightAxis * shiftInFace.x
                + face.upAxis * shiftInFace.y
                + face.normal * (LiftMm * AppConstants.MM_TO_UNITS);
            go.transform.rotation = Quaternion.LookRotation(-face.normal, face.upAxis);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = QuadMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = HighlightMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Quads.Add(go);
        }

        private static Mesh? _quad;

        internal static Mesh QuadMesh()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "EdgeSideHighlightQuad", hideFlags = HideFlags.DontSave };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            var c = UI.UIStyle.EdgeHighlight3D;
            _quad.colors = new[] { c, c, c, c };
            return _quad;
        }

        public static System.Func<Material?>? MaterialFactory;

        private static bool _shaderMissing;

        internal static Material? HighlightMaterial()
        {
            if (MaterialFactory != null) return MaterialFactory();
            if (_material != null) return _material;
            if (_shaderMissing) return null;

            var shader = FindHighlightShader();
            if (shader == null)
            {
                _shaderMissing = true;
                Debug.LogWarning("[EdgeSideHighlight] Шейдер накладки не найден — "
                    + "подсветка стороны под кромку не будет видна.");
                return null;
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            var color = UI.UIStyle.EdgeHighlight3D;
            _material.color = color;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", color);

            if (shader.name == PrimaryShaderName) return _material;

            MakeSeeThrough(_material);
            return _material;
        }

        internal static void MakeSeeThrough(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend"))
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        internal static Shader? FindHighlightShader()
        {
            var shader = Shader.Find(PrimaryShaderName);
            if (shader == null) shader = Resources.Load<Shader>(PrimaryShaderResourcePath);
            foreach (var name in FallbackShaderNames)
            {
                if (shader != null) break;
                shader = Shader.Find(name);
            }
            return shader;
        }
    }
}
