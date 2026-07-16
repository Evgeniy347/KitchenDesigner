using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class Wall : MonoBehaviour
    {
        private bool _lowered;
        private float _fullScaleY;
        private float _fullPosY;
        private readonly List<WindowElement> _attachedWindows = new List<WindowElement>();
        private MeshFilter? _meshFilter;
        private Mesh? _customMesh;

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

        public void RegisterWindow(WindowElement window)
        {
            if (!_attachedWindows.Contains(window))
                _attachedWindows.Add(window);
            RebuildMesh();
        }

        public void UnregisterWindow(WindowElement window)
        {
            _attachedWindows.Remove(window);
            RebuildMesh();
        }

        public void RebuildMesh()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) return;

            _attachedWindows.RemoveAll(w => w == null);

            var el = GetComponent<KitchenElement>();
            var dims = el != null ? el.DimensionsMM : new Vector3Int(100, 2500, 2000);

            var cutouts = new List<WallMeshBuilder.WindowCutout>();
            foreach (var w in _attachedWindows)
            {
                if (w == null) continue;
                var localPos = transform.InverseTransformPoint(w.transform.position);
                var wDims = w.DimensionsMM;
                float halfWallW = dims.x * 0.001f * 0.5f;
                float halfWallH = dims.y * 0.001f * 0.5f;
                cutouts.Add(new WallMeshBuilder.WindowCutout
                {
                    centerNorm = new Vector2(
                        halfWallW > 0.001f ? localPos.x / halfWallW * 0.5f : 0f,
                        halfWallH > 0.001f ? localPos.y / halfWallH * 0.5f : 0f),
                    halfSizeNorm = new Vector2(
                        wDims.x * 0.001f * 0.5f / Mathf.Max(0.001f, halfWallW) * 0.5f,
                        wDims.y * 0.001f * 0.5f / Mathf.Max(0.001f, halfWallH) * 0.5f)
                });
            }

            if (_customMesh != null)
            {
                if (Application.isPlaying) Object.Destroy(_customMesh);
                else Object.DestroyImmediate(_customMesh);
            }

            _customMesh = WallMeshBuilder.Build(cutouts);
            _meshFilter.sharedMesh = _customMesh;

            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = _customMesh;
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
