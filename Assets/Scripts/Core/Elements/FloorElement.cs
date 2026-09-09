using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FloorElement : KitchenElement, IKeepsPlacementHeight
    {
        public override bool CanFollowAnAttachParent => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public const int DEFAULT_SIZE_MM = 3000;
        public const int DEFAULT_THICKNESS_MM = 100;

        private static readonly List<FloorElement> _active = new List<FloorElement>();
        public static IReadOnlyList<FloorElement> Active => _active;
        [SerializeField] private List<Vector2Int> _polygonLocalMm = new List<Vector2Int>();
        private Mesh? _polygonMesh;

        public IReadOnlyList<Vector2Int> PolygonLocalMm => _polygonLocalMm;

        public override Vector2Int DecorSurfaceMM
            => new Vector2Int(DimensionsMM.x, DimensionsMM.z);

        public void SetPolygonLocalMm(IReadOnlyList<Vector2Int> points)
        {
            _polygonLocalMm = points != null ? new List<Vector2Int>(points) : new List<Vector2Int>();
            var dims = DimensionsMM;
            IReadOnlyList<Vector2Int> renderPoints = _polygonLocalMm;
            if (_polygonLocalMm.Count < 3)
            {
                int hx = dims.x / 2, hz = dims.z / 2;
                renderPoints = new[]
                {
                    new Vector2Int(-hx, -hz), new Vector2Int(hx, -hz),
                    new Vector2Int(hx, hz), new Vector2Int(-hx, hz)
                };
            }
            var worldOriginMm = new Vector2(transform.position.x, transform.position.z) / AppConstants.MM_TO_UNITS;
            var mesh = FloorPolygonMesh.Build(renderPoints, dims, worldOriginMm);
            var filter = GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = mesh;
            var box = GetComponent<BoxCollider>();
            if (box != null) box.enabled = false;
            var collider = GetComponent<MeshCollider>();
            if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = mesh;
            if (_polygonMesh != null)
            {
                if (Application.isPlaying) Destroy(_polygonMesh); else DestroyImmediate(_polygonMesh);
            }
            _polygonMesh = mesh;
        }

        private void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
            RefreshBasePlateVisibility();
        }

        private void OnDisable()
        {
            _active.Remove(this);
            RefreshBasePlateVisibility(except: this);
        }

        protected override void OnElementDestroyed()
        {
            if (_polygonMesh == null) return;
            if (Application.isPlaying) Destroy(_polygonMesh); else DestroyImmediate(_polygonMesh);
            _polygonMesh = null;
        }

        public static void RefreshBasePlateVisibility(FloorElement? except = null)
        {
            int count = 0;
            foreach (var f in Object.FindObjectsByType<FloorElement>(FindObjectsSortMode.None))
                if (f != null && f != except && f.isActiveAndEnabled) count++;

            foreach (var bp in Object.FindObjectsByType<BasePlate>(FindObjectsSortMode.None))
            {
                var renderer = bp.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = count == 0;
            }
        }
    }
}
