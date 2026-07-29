using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class RadiusTableElement : KitchenElement
    {
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;

        private readonly List<GameObject> _legs = new List<GameObject>();
        private Material? _tabletopMaterial;
        private Material? _legsMaterial;

        [SerializeField] private int _legInsetMM = 100;
        [SerializeField] private string _tabletopMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        public int LegInsetMM
        {
            get => _legInsetMM;
            set { _legInsetMM = Mathf.Max(0, value); ApplyDimensions(); }
        }

        public string TabletopMaterialId
        {
            get => _tabletopMaterialId;
            set { _tabletopMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        public string LegsMaterialId
        {
            get => _legsMaterialId;
            set { _legsMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        public new string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

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
            base.ApplyDimensions();
            transform.localScale = Vector3.one;

            var dims = DimensionsMM;
            int overallW = dims.x;
            int overallH = dims.y;
            int overallD = dims.z;

            int legH = Mathf.Max(1, overallH - TabletopThicknessMM);

            float toU = AppConstants.MM_TO_UNITS;
            float legCross = LegCrossSectionMM * toU;
            float topThicknessU = TabletopThicknessMM * toU;

            float legCenterY = (legH * 0.5f - overallH * 0.5f) * toU;
            float topCenterY = (overallH * 0.5f - TabletopThicknessMM * 0.5f) * toU;

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            var mesh = CapsuleTableMesh.Build(overallW * toU, topThicknessU, overallD * toU, topCenterY);
            meshFilter.sharedMesh = mesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_tabletopMaterial != null)
                meshRenderer.sharedMaterial = _tabletopMaterial;

            UpdateCollider(mesh);

            var legScale = new Vector3(legCross, legH * toU, legCross);
            var legPositions = CapsuleTableMesh.GetLegPositions(overallW, overallH, overallD, legCenterY, _legInsetMM);

            EnsureLegs(4);

            for (int i = 0; i < 4; i++)
            {
                _legs[i].transform.localPosition = legPositions[i];
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

        public override Vector3[] GetVertices()
        {
            var size = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);
            var half = size * 0.5f;
            var pos = transform.position;
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
