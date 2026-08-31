using UnityEngine;

namespace KitchenDesigner.Core
{
    public class RadialShelfElement : KitchenElement
    {

        public override string DisplayTypeName => "Радиусная полка";
        [SerializeField] private int _cornerRadius = AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
        private bool _applying;

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public override bool SupportsGaps => true;

        [Undoable]
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
                _cornerRadius = ClampCornerRadius(_cornerRadius);

                transform.localScale = Vector3.one;
                RebuildMesh();

                MaterialManager.RefreshTiling(this);
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
            AdoptOwnedMesh(mesh);
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
