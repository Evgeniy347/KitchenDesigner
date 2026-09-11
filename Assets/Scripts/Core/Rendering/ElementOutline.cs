using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BoxWireframe
    {
        public static readonly Vector3[] Corners =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
        };

        public static readonly int[] EdgeIndices =
        {
            0,1, 1,2, 2,3, 3,0,
            4,5, 5,6, 6,7, 7,4,
            0,4, 1,5, 2,6, 3,7,
        };

        public const int EdgeCount = 12;

        public static void WorldCorners(Matrix4x4 localToWorld, Vector3[] into)
        {
            for (int i = 0; i < Corners.Length; i++)
                into[i] = localToWorld.MultiplyPoint3x4(Corners[i]);
        }
    }

    [DisallowMultipleComponent]
    public class ElementOutline : MonoBehaviour
    {
        public const float EdgeThicknessMeters = 0.004f;

        internal const string OutlineRootName = "__Outline";

        internal const string PrimaryShaderName = "Hidden/KD/UnlitColor";

        internal const string PrimaryShaderResourcePath = "Shaders/UnlitColor";

        internal static readonly string[] UnlitShaderChain =
        {
            PrimaryShaderName,
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
        };

        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.1f, 1f);

        private static Material? _blackMat;
        private static Material? _selectedMat;

        private Transform? _root;
        private readonly Transform[] _edges = new Transform[BoxWireframe.EdgeCount];
        private readonly Vector3[] _corners = new Vector3[BoxWireframe.Corners.Length];
        private bool _visible;

        internal Transform? Root => _root;

        public static ElementOutline? For(KitchenElement element)
            => element != null ? element.GetComponent<ElementOutline>() : null;

        public static ElementOutline? Ensure(KitchenElement element)
        {
            if (element == null) return null;
            var outline = element.GetComponent<ElementOutline>();
            if (outline == null) outline = element.gameObject.AddComponent<ElementOutline>();
            outline.Build();
            return outline;
        }

        private void Build()
        {
            if (_root != null) return;

            var rootGo = new GameObject(OutlineRootName);
            _root = rootGo.transform;
            _root.SetParent(null, false);

            for (int i = 0; i < BoxWireframe.EdgeCount; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "Edge" + i;
                DropColliderSoClicksReachThePart(seg);

                var mr = seg.GetComponent<MeshRenderer>();
                mr.sharedMaterial = BlackMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                seg.transform.SetParent(_root, false);
                _edges[i] = seg.transform;
            }

            _root.gameObject.SetActive(false);
        }

        private static void DropColliderSoClicksReachThePart(GameObject seg)
        {
            var col = seg.GetComponent<Collider>();
            if (col != null) DestroyNow.The(col);
        }

        public void Show(bool selected)
        {
            if (_root == null) Build();
            var mat = selected ? SelectedMaterial() : BlackMaterial();
            if (mat != null)
                foreach (var e in _edges)
                    if (e != null) e.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (_root != null) _root.gameObject.SetActive(true);
            _visible = true;
            UpdateEdges();
        }

        public void Hide()
        {
            _visible = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            using var _ = PerfMarkers.ElementOutlineLateUpdate.Auto();
            if (_visible) UpdateEdges();
        }

        private void OnDestroy()
        {
            if (_root != null) DestroyNow.The(_root.gameObject);
        }

        private void UpdateEdges()
        {
            if (_root == null) return;
            BoxWireframe.WorldCorners(transform.localToWorldMatrix, _corners);

            var idx = BoxWireframe.EdgeIndices;
            for (int e = 0; e < BoxWireframe.EdgeCount; e++)
            {
                var seg = _edges[e];
                if (seg == null) continue;
                Vector3 a = _corners[idx[e * 2]];
                Vector3 b = _corners[idx[e * 2 + 1]];
                Vector3 alongEdge = b - a;
                float edgeLength = alongEdge.magnitude;

                seg.position = (a + b) * 0.5f;
                seg.rotation = edgeLength > 1e-6f
                    ? Quaternion.LookRotation(alongEdge / edgeLength)
                    : Quaternion.identity;
                seg.localScale = new Vector3(EdgeThicknessMeters, EdgeThicknessMeters, edgeLength);
            }
        }

        internal static Shader? FindUnlitShader()
        {
            foreach (var name in UnlitShaderChain)
            {
                var found = Shader.Find(name);
                if (found != null) return found;
                if (name != PrimaryShaderName) continue;
                found = Resources.Load<Shader>(PrimaryShaderResourcePath);
                if (found != null) return found;
            }
            return null;
        }

        internal static Material? MakeUnlit(Color color)
        {
            var shader = FindUnlitShader();
            if (shader == null) return null;

            var m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        private static Material? BlackMaterial()
        {
            if (_blackMat == null) _blackMat = MakeUnlit(Color.black);
            return _blackMat;
        }

        private static Material? SelectedMaterial()
        {
            if (_selectedMat == null) _selectedMat = MakeUnlit(SelectedColor);
            return _selectedMat;
        }
    }
}
