using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class Wall : MonoBehaviour
    {
        [SerializeField] private string _kind = "";
        public string Kind { get => _kind; set => _kind = value ?? ""; }
        [SerializeField] private WallMeshBuilder.EndShape _endShape = default;
        [SerializeField] private bool _hasEndShape;
        public WallMeshBuilder.EndShape EndShape => _hasEndShape ? _endShape : WallMeshBuilder.EndShape.Square;
        public void SetEndShape(WallMeshBuilder.EndShape shape)
        {
            _endShape = shape; _hasEndShape = true; RebuildMesh();
        }
        private bool _lowered;
        private float _fullScaleY;
        private float _fullPosY;
        private readonly List<WindowElement> _attachedWindows = new List<WindowElement>();
        private readonly List<DoorElement> _attachedDoors = new List<DoorElement>();
        private MeshFilter? _meshFilter;
        private Mesh? _customMesh;
        private Vector3 _syncPos;
        private Quaternion _syncRot = Quaternion.identity;
        private Vector3 _syncScale;
        private Vector3Int _syncDims;
        private bool _hasSyncSnapshot;

        public void SetLowered(bool lower, float loweredHeightUnits)
        {
            if (lower)
            {
                if (!_lowered)
                {
                    _fullScaleY = transform.localScale.y;
                    _fullPosY = transform.position.y;
                    _lowered = true;
                }
                ApplyLowered(loweredHeightUnits);
            }
            else if (_lowered)
            {
                var sc = transform.localScale; sc.y = _fullScaleY; transform.localScale = sc;
                var p = transform.position; p.y = _fullPosY; transform.position = p;
                _lowered = false;
            }
        }

        public void RestoreFull() => SetLowered(false, 0f);

        public bool IsLowered => _lowered;

        public float FullScaleY => _lowered ? _fullScaleY : transform.localScale.y;

        public Vector3 FullPosition
        {
            get
            {
                var p = transform.position;
                if (_lowered) p.y = _fullPosY;
                return p;
            }
        }

        private void ApplyLowered(float loweredHeightUnits)
        {
            float baseY = _fullPosY - _fullScaleY * 0.5f;
            var sc = transform.localScale; sc.y = loweredHeightUnits; transform.localScale = sc;
            var p = transform.position; p.y = baseY + loweredHeightUnits * 0.5f; transform.position = p;
        }

        public bool HasWindow(WindowElement window) => _attachedWindows.Contains(window);

        public IReadOnlyList<WindowElement> AttachedWindows => _attachedWindows;

        public void RegisterWindow(WindowElement window)
        {
            if (!_attachedWindows.Contains(window))
                _attachedWindows.Add(window);
            RebuildMesh();
            foreach (var w in _attachedWindows)
                if (w != null) w.RefreshGeometry();
            foreach (var d in _attachedDoors)
                if (d != null) d.RefreshGeometry();
        }

        public void UnregisterWindow(WindowElement window)
        {
            _attachedWindows.Remove(window);
            RebuildMesh();
            foreach (var w in _attachedWindows)
                if (w != null) w.RefreshGeometry();
            foreach (var d in _attachedDoors)
                if (d != null) d.RefreshGeometry();
        }

        public bool HasDoor(DoorElement door) => _attachedDoors.Contains(door);

        public IReadOnlyList<DoorElement> AttachedDoors => _attachedDoors;

        public void RegisterDoor(DoorElement door)
        {
            if (!_attachedDoors.Contains(door))
                _attachedDoors.Add(door);
            RebuildMesh();
            foreach (var w in _attachedWindows)
                if (w != null) w.RefreshGeometry();
            foreach (var d in _attachedDoors)
                if (d != null) d.RefreshGeometry();
        }

        public void UnregisterDoor(DoorElement door)
        {
            _attachedDoors.Remove(door);
            RebuildMesh();
            foreach (var w in _attachedWindows)
                if (w != null) w.RefreshGeometry();
            foreach (var d in _attachedDoors)
                if (d != null) d.RefreshGeometry();
        }

        public void SyncOpeningsIfChanged()
        {
            using var _ = PerfMarkers.WallSyncOpenings.Auto();
            if (_attachedWindows.Count == 0 && _attachedDoors.Count == 0) return;
            if (_hasSyncSnapshot && !GeometryChanged()) return;
            RebuildMesh();
        }

        private void LateUpdate() => SyncOpeningsIfChanged();

        private (Vector3 pos, Quaternion rot, Vector3 scale, Vector3Int dims) CurrentGeometry()
        {
            var scale = transform.localScale;
            scale.y = FullScaleY;
            var el = GetComponent<KitchenElement>();
            return (FullPosition, transform.rotation, scale,
                el != null ? el.DimensionsMM : Vector3Int.zero);
        }

        private bool GeometryChanged()
        {
            var g = CurrentGeometry();
            return (g.pos - _syncPos).sqrMagnitude > Tolerance.EpsilonSqr ||
                   (g.scale - _syncScale).sqrMagnitude > Tolerance.EpsilonSqr ||
                   g.dims != _syncDims ||
                   Quaternion.Angle(g.rot, _syncRot) > 0.01f;
        }

        public void RebuildMesh()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) return;

            _attachedWindows.RemoveAll(w => w == null);
            _attachedDoors.RemoveAll(d => d == null);

            var el = GetComponent<KitchenElement>();
            var dims = el != null ? el.DimensionsMM : new Vector3Int(100, 2500, 2000);

            bool thickAlongX = dims.x <= dims.z;
            float wallW = (thickAlongX ? dims.z : dims.x) * 0.001f;
            float wallH = FullScaleY > 0.001f ? FullScaleY : dims.y * 0.001f;

            var cutouts = new List<WallMeshBuilder.WindowCutout>();

            void AddCutout(KitchenElement opening)
            {
                if (opening == null) return;
                var oDims = opening.DimensionsMM;
                Vector3 lp = Quaternion.Inverse(transform.rotation) * (opening.transform.position - FullPosition);
                float u = (thickAlongX ? lp.z : lp.x) / Mathf.Max(0.001f, wallW);
                float v = lp.y / Mathf.Max(0.001f, wallH);
                cutouts.Add(new WallMeshBuilder.WindowCutout
                {
                    centerNorm = new Vector2(u, v),
                    halfSizeNorm = new Vector2(
                        oDims.x * 0.001f * 0.5f / Mathf.Max(0.001f, wallW),
                        oDims.y * 0.001f * 0.5f / Mathf.Max(0.001f, wallH))
                });
            }

            foreach (var w in _attachedWindows) AddCutout(w);
            foreach (var d in _attachedDoors) AddCutout(d);

            if (_customMesh != null)
            {
                if (Application.isPlaying) Object.Destroy(_customMesh);
                else Object.DestroyImmediate(_customMesh);
            }

            _customMesh = WallMeshBuilder.Build(cutouts, thickAlongX, EndShape);
            _meshFilter.sharedMesh = _customMesh;

            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = _customMesh;

            var g = CurrentGeometry();
            (_syncPos, _syncRot, _syncScale, _syncDims) = g;
            _hasSyncSnapshot = true;
        }

        private void OnDestroy()
        {
            if (_customMesh != null)
            {
                if (Application.isPlaying) Object.Destroy(_customMesh);
                else Object.DestroyImmediate(_customMesh);
                _customMesh = null;
            }
        }
    }
}
