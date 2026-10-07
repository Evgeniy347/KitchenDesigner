using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class GroundQuad : MonoBehaviour
    {
        private static readonly Color GroundColor = new Color(0.6f, 0.6f, 0.6f);

        private MeshRenderer? _renderer;
        private Mesh? _mesh;
        private bool _viewVisible = true;

        public static GroundQuad? Instance { get; private set; }

        public bool CoveredByUserFloor { get; private set; }

        public bool IsShown => _renderer != null && _renderer.enabled;

        public static bool ShownWith(bool coveredByUserFloor, bool visibleInView) =>
            !coveredByUserFloor && visibleInView;

        public static GroundQuad Create()
        {
            var go = new GameObject("GroundQuad");
            var quad = go.AddComponent<GroundQuad>();
            quad._mesh = BuildMesh(AppConstants.GROUND_QUAD_SIZE_MM * AppConstants.MM_TO_UNITS * 0.5f);
            go.AddComponent<MeshFilter>().sharedMesh = quad._mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) renderer.material = new Material(shader) { color = GroundColor };
            quad._renderer = renderer;
            Instance = quad;

            FloorElement.RefreshGroundVisibility();
            return quad;
        }

        public void SetCoveredByUserFloor(bool covered)
        {
            CoveredByUserFloor = covered;
            Apply();
        }

        public void ApplyViewVisibility(bool visibleInView)
        {
            _viewVisible = visibleInView;
            Apply();
        }

        private void Apply()
        {
            if (_renderer != null) _renderer.enabled = ShownWith(CoveredByUserFloor, _viewVisible);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyNow.The(_mesh);
        }

        private static Mesh BuildMesh(float half)
        {
            var mesh = new Mesh { name = "GroundQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half),
                new Vector3(half, 0f, half), new Vector3(half, 0f, -half),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
