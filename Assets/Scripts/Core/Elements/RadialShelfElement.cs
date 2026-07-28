using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Радиусная полка — прямоугольная доска с одним скруглённым углом.
    /// Размеры: x = ширина, y = толщина, z = глубина; меш центрирован на pivot,
    /// скруглён угол (+X, +Z) радиусом CornerRadius (1..min(ширина, глубина)).</summary>
    public class RadialShelfElement : KitchenElement
    {
        [SerializeField] private int _cornerRadius = AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
        private Mesh? _ownedMesh;
        private bool _applying;

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                value = ClampCornerRadius(value);
                if (_cornerRadius == value) return;
                _cornerRadius = value;
                RebuildMesh();
            }
        }

        private int ClampCornerRadius(int value)
        {
            var dims = DimensionsMM;
            int max = Mathf.Max(1, Mathf.Min(dims.x, dims.z));
            return Mathf.Clamp(value, 1, max);
        }

        public override void ApplyDimensions()
        {
            if (_applying) return;
            _applying = true;
            try
            {
                // Ширина/толщина/глубина хранятся раздельно; радиус угла лишь
                // клампится, чтобы дуга помещалась в доску.
                _cornerRadius = ClampCornerRadius(_cornerRadius);

                // Меш строится в мировых единицах — localScale остаётся единичным.
                transform.localScale = Vector3.one;
                RebuildMesh();
            }
            finally
            {
                _applying = false;
            }
        }

        private void RebuildMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            var dims = DimensionsMM;
            var mesh = RadialShelfMesh.Build(
                dims.x * AppConstants.MM_TO_UNITS,
                dims.z * AppConstants.MM_TO_UNITS,
                dims.y * AppConstants.MM_TO_UNITS,
                _cornerRadius * AppConstants.MM_TO_UNITS);
            if (_ownedMesh != null) DestroyImmediate(_ownedMesh);
            _ownedMesh = mesh;
            meshFilter.sharedMesh = mesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            UpdateCollider(mesh);
        }

        private void UpdateCollider(Mesh mesh)
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is MeshCollider))
                DestroyImmediate(existing);

            var meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.convex = true;
            }
            meshCollider.sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            if (_ownedMesh != null)
            {
                DestroyImmediate(_ownedMesh);
                _ownedMesh = null;
            }
        }
    }
}
