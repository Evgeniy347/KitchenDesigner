using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Радиусная (угловая) полка — сектор цилиндра 90°.
    /// Размеры: x = радиус, y = толщина, z = радиус.</summary>
    public class RadialShelfElement : KitchenElement
    {
        [SerializeField] private int _radius = 300;
        private bool _applying;

        public int Radius
        {
            get => _radius;
            set
            {
                if (value < 1) value = 1;
                if (_radius == value) return;
                _radius = value;
                var dims = DimensionsMM;
                var thickness = dims.y > 0 ? dims.y : AppConstants.BOARD_THICKNESS_DEFAULT;
                DimensionsMM = new Vector3Int(_radius, thickness, _radius);
            }
        }

        public override void ApplyDimensions()
        {
            if (_applying) return;
            _applying = true;
            try
            {
                var dims = DimensionsMM;
                int r = Mathf.Max(1, Mathf.Max(dims.x, dims.z));
                _radius = r;
                if (dims.x != r || dims.z != r)
                    DimensionsMM = new Vector3Int(r, dims.y, r);

                // Меш строится в единичном размере — масштабирование через localScale.
                transform.localScale = new Vector3(
                    r * AppConstants.MM_TO_UNITS,
                    dims.y * AppConstants.MM_TO_UNITS,
                    r * AppConstants.MM_TO_UNITS);
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

            float radiusUnits = _radius * AppConstants.MM_TO_UNITS;
            float heightUnits = DimensionsMM.y * AppConstants.MM_TO_UNITS;
            var mesh = RadialShelfMesh.Build(radiusUnits, heightUnits);
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
    }
}
