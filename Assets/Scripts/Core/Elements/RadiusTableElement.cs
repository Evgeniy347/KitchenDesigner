using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class RadiusTableElement : KitchenElement, ITabletop
    {

        public override string DisplayTypeName => "Радиусный стол";
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;
        public const int MinLegInsetFromContourMM = 50;

        private readonly List<GameObject> _legs = new List<GameObject>();
        private Material? _tabletopMaterial;
        private Material? _legsMaterial;
        private bool _applying;

        [SerializeField] private int _legInsetMM = 100;
        [SerializeField] private string _tabletopMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        [Undoable]
        public int LegInsetMM
        {
            get => _legInsetMM;
            set { _legInsetMM = Mathf.Max(0, value); ApplyDimensions(); }
        }

        [NotUndoable("декор ставится через SetMaterialCommand (MaterialSlot.Tabletop)")]
        public string TabletopMaterialId
        {
            get => _tabletopMaterialId;
            set { _tabletopMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        [NotUndoable("декор ставится через SetMaterialCommand (MaterialSlot.Legs)")]
        public string LegsMaterialId
        {
            get => _legsMaterialId;
            set { _legsMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        [NotUndoable("псевдоним TabletopMaterialId — см. его причину")]
        public override string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public override Vector2Int DecorSurfaceMM
            => new Vector2Int(DimensionsMM.x, DimensionsMM.z);

        private void ApplyMaterial()
        {
            var topDef = MaterialCatalog.Get(_tabletopMaterialId);
            var legsDef = MaterialCatalog.Get(_legsMaterialId);
            if (topDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(topDef);
                if (mat != null) SetTabletopMaterial(mat);
            }
            if (legsDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(legsDef);
                if (mat != null) SetLegsMaterial(mat);
            }
        }

        public override void ApplyDimensions()
        {
            if (_applying) return;
            _applying = true;
            try
            {
                transform.localScale = Vector3.one;

                float toU = AppConstants.MM_TO_UNITS;
                var dims = DimensionsMM;
                float widthU = dims.x * toU;
                float depthU = dims.z * toU;
                float radiusU = Mathf.Min(widthU, depthU) * 0.5f;

                RebuildTabletop(dims, widthU, depthU, radiusU);
                PlaceLegs(dims, widthU, depthU, radiusU);

                MaterialManager.RefreshTiling(this);
            }
            finally
            {
                _applying = false;
            }
        }

        private void RebuildTabletop(Vector3Int dims, float widthU, float depthU, float radiusU)
        {
            float thicknessU = TabletopThicknessMM * AppConstants.MM_TO_UNITS;
            float centreYU = FurnitureLayout.TopCentreY(dims.y, TabletopThicknessMM);

            var profile = RoundedRectProfile.Uniform(widthU, depthU, radiusU,
                RoundedRectProfile.DefaultSegments);
            var mesh = ProfileExtrusionMesh.Build(profile, widthU, depthU, thicknessU, centreYU);
            AdoptOwnedMesh(mesh);

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_tabletopMaterial != null) meshRenderer.sharedMaterial = _tabletopMaterial;

            UpdateCollider(mesh);
        }

        private void PlaceLegs(Vector3Int dims, float widthU, float depthU, float radiusU)
        {
            float toU = AppConstants.MM_TO_UNITS;
            int legHeightMM = FurnitureLayout.LegHeightMM(dims.y, TabletopThicknessMM);
            float legCentreYU = FurnitureLayout.LegCentreY(dims.y, TabletopThicknessMM);

            float insetU = Mathf.Max(_legInsetMM, MinLegInsetFromContourMM) * toU;
            float legDiagonalU = LegCrossSectionMM * toU * Mathf.Sqrt(2f);

            var footprint = RoundedRectSeating.LegCentres(widthU, depthU, radiusU, insetU,
                legDiagonalU);
            var legScale = new Vector3(LegCrossSectionMM * toU, legHeightMM * toU,
                LegCrossSectionMM * toU);

            EnsureLegs(footprint.Length);

            for (int i = 0; i < footprint.Length; i++)
            {
                _legs[i].transform.localPosition =
                    new Vector3(footprint[i].x, legCentreYU, footprint[i].y);
                _legs[i].transform.localScale = legScale;
                _legs[i].transform.localRotation = Quaternion.identity;
            }
        }

        private void UpdateCollider(Mesh mesh)
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is MeshCollider))
                Object.DestroyImmediate(existing);

            var meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.sharedMesh = mesh;
        }

        private void EnsureLegs(int count)
        {
            while (_legs.Count < count)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg{_legs.Count + 1}";
                leg.transform.SetParent(transform, false);

                var collider = leg.GetComponent<BoxCollider>();
                if (collider != null) Object.DestroyImmediate(collider);

                var renderer = leg.GetComponent<MeshRenderer>();
                if (renderer != null && _legsMaterial != null)
                    renderer.sharedMaterial = _legsMaterial;

                _legs.Add(leg);
            }
        }

        public void SetTabletopMaterial(Material material)
        {
            _tabletopMaterial = material;
            var rootRenderer = GetComponent<MeshRenderer>();
            if (rootRenderer != null)
                rootRenderer.sharedMaterial = _tabletopMaterial;
        }

        public void SetLegsMaterial(Material material)
        {
            _legsMaterial = material;
            foreach (var leg in _legs)
            {
                var renderer = leg.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = _legsMaterial;
            }
        }

        public void SetMaterial(Material material)
        {
            SetTabletopMaterial(material);
            SetLegsMaterial(material);
        }

        public override Face[] GetFaces() => GetFacesAt(transform.position);

        public override Face[] GetFacesAt(Vector3 position)
        {
            var size = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);
            var pos = position;
            var rot = ValidationRotation;
            var half = size * 0.5f;

            var axes = new Vector3[] { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };

            var faceDims = new Vector2[]
            {
                new Vector2(size.y, size.z),
                new Vector2(size.x, size.z),
                new Vector2(size.x, size.y),
            };

            var offsets = new Vector3[]
            {
                 axes[0] * half.x, -axes[0] * half.x,
                 axes[1] * half.y, -axes[1] * half.y,
                 axes[2] * half.z, -axes[2] * half.z,
            };

            var normals = new Vector3[]
            {
                 axes[0], -axes[0],
                 axes[1], -axes[1],
                 axes[2], -axes[2],
            };

            var rightAxis = new Vector3[] { axes[1], axes[1], axes[0], axes[0], axes[0], axes[0] };
            var upAxis = new Vector3[] { axes[2], axes[2], axes[2], axes[2], axes[1], axes[1] };

            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
            {
                int dimIdx = i / 2;
                faces[i] = new Face(
                    pos + offsets[i],
                    normals[i],
                    faceDims[dimIdx],
                    rightAxis[i],
                    upAxis[i]
                );
            }
            return faces;
        }

        public override Vector3[] GetVertices() => GetVerticesAt(transform.position);

        public override Vector3[] GetVerticesAt(Vector3 position)
        {
            var size = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);
            var half = size * 0.5f;
            var pos = position;
            var rot = transform.rotation;

            var localCorners = new Vector3[]
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z),
                new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z),
                new Vector3(-half.x,  half.y,  half.z),
            };

            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = pos + rot * localCorners[i];
            return result;
        }

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren()
        {
            foreach (var leg in _legs)
                if (leg != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(leg);
                    else
                        Object.DestroyImmediate(leg);
                }
            _legs.Clear();
        }
    }
}
