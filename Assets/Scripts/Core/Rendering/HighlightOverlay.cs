using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class HighlightOverlay
    {
        public const float LiftMm = 0.2f;

        internal const string PrimaryShaderName = "Hidden/OverlayLine";
        internal const string PrimaryShaderResourcePath = "Shaders/OverlayLine";

        internal static readonly string[] FallbackShaderNames =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
        };

        public static Func<Material?>? MaterialFactory;

        private static readonly List<GameObject> Pieces = new List<GameObject>();
        private static readonly List<Mesh> OwnedMeshes = new List<Mesh>();
        private static Material? _material;
        private static GameObject? _root;
        private static Mesh? _quad;
        private static bool _shaderMissing;

        private static KitchenElement? _shownFor;
        private static Action? _rebuild;
        private static Action? _onHide;
        private static Vector3 _shownPos;
        private static Quaternion _shownRot;
        private static Vector3Int _shownDims;

        public static int PieceCount => Pieces.Count;

        public static IReadOnlyList<GameObject> PieceObjects => Pieces;

        public static KitchenElement? ShownFor => _shownFor;

        public static void Begin(KitchenElement element, Action rebuild, Action? onHide = null)
        {
            if (element == null) return;
            _shownFor = element;
            _rebuild = rebuild;
            _onHide = onHide;
            _shownPos = element.transform.position;
            _shownRot = element.transform.rotation;
            _shownDims = element.DimensionsMM;
        }

        public static void Hide()
        {
            foreach (var piece in Pieces)
                if (piece != null) DestroyNow(piece);
            Pieces.Clear();

            foreach (var mesh in OwnedMeshes)
                if (mesh != null) DestroyNow(mesh);
            OwnedMeshes.Clear();

            _shownFor = null;
            _rebuild = null;
            var onHide = _onHide;
            _onHide = null;
            onHide?.Invoke();
        }

        public static void Sync()
        {
            if (Pieces.Count == 0) return;

            if (_shownFor == null || !SceneVisibility.AnyRendererEnabled(_shownFor))
            {
                Hide();
                return;
            }

            var t = _shownFor.transform;
            if (t.position == _shownPos && t.rotation == _shownRot
                && _shownFor.DimensionsMM == _shownDims) return;

            _rebuild?.Invoke();
        }

        public static void AddQuad(in Face face, Vector2 size, Vector2 shiftInFace)
        {
            var go = NewPiece("EdgeSideHighlight");

            go.transform.position = face.center
                + face.rightAxis * shiftInFace.x
                + face.upAxis * shiftInFace.y
                + face.normal * (LiftMm * AppConstants.MM_TO_UNITS);
            go.transform.rotation = Quaternion.LookRotation(-face.normal, face.upAxis);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            Dress(go, QuadMesh());
        }

        public static void AddSleeve(Transform space, in HighlightSleeve sleeveMM)
        {
            if (space == null || sleeveMM.IsEmpty) return;

            float toU = AppConstants.MM_TO_UNITS;
            var from = space.TransformPoint(sleeveMM.FromMM * toU);
            var to = space.TransformPoint(sleeveMM.ToMM * toU);
            var along = to - from;
            float length = along.magnitude;
            if (length <= 0f) return;

            float radius = (sleeveMM.RadiusMM + LiftMm) * toU;
            var mesh = CylinderStackMesh.Build(new CylinderSection(radius, length));
            mesh.name = "PartHighlightSleeve";
            mesh.hideFlags = HideFlags.DontSave;
            Paint(mesh);
            OwnedMeshes.Add(mesh);

            var go = NewPiece("PartHighlightSleeve");
            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, along / length);
            go.transform.localScale = Vector3.one;

            Dress(go, mesh);
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
            _quad.normals = new[]
            {
                -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward,
            };
            _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            Paint(_quad);
            return _quad;
        }

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
            var color = Tint;
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

        private static Color Tint => UI.UIStyle.EdgeHighlight3D;

        private static void Paint(Mesh mesh)
        {
            var color = Tint;
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.colors = colors;
        }

        private static GameObject NewPiece(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Root(), worldPositionStays: false);
            return go;
        }

        private static void Dress(GameObject go, Mesh mesh)
        {
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = HighlightMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Pieces.Add(go);
        }

        private static void DestroyNow(UnityEngine.Object obj)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
