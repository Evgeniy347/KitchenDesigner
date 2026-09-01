using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class StoolElement : KitchenElement, ITabletop
    {
        public override string DisplayTypeName => "Табуретка";

        public const int DefaultWidthMM = 360;
        public const int DefaultHeightMM = 450;
        public const int DefaultDepthMM = 360;
        public const int SeatThicknessMM = 30;
        public const int LegCrossSectionMM = 40;
        public const int LegInsetMM = 30;

        private readonly List<GameObject> _legs = new List<GameObject>();
        private Material? _seatMaterial;
        private Material? _legsMaterial;
        private bool _applying;

        [SerializeField] private int _cornerRadiusMM;
        [SerializeField] private string _seatMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        public const string ShapeSquare = "square";
        public const string ShapeRounded = "rounded";
        public const string ShapeRound = "round";

        public string ShapeName =>
            _cornerRadiusMM <= 0 ? ShapeSquare
            : _cornerRadiusMM >= MaxCornerRadiusMM(DimensionsMM) ? ShapeRound
            : ShapeRounded;

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => Mathf.Max(0, Mathf.Min(dimensionsMM.x, dimensionsMM.z) / 2);

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public override Vector2Int DecorSurfaceMM
            => new Vector2Int(DimensionsMM.x, DimensionsMM.z);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        [Undoable]
        public int CornerRadiusMM
        {
            get => _cornerRadiusMM;
            set
            {
                value = ClampCornerRadius(value);
                if (_cornerRadiusMM == value) return;
                _cornerRadiusMM = value;
                ApplyDimensions();
            }
        }

        [NotUndoable("декор ставится через SetMaterialCommand (MaterialSlot.Tabletop)")]
        public string TabletopMaterialId
        {
            get => _seatMaterialId;
            set { _seatMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
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

        private int ClampCornerRadius(int value)
            => Mathf.Clamp(value, 0, MaxCornerRadiusMM(DimensionsMM));

        private void ApplyMaterial()
        {
            var seatDef = MaterialCatalog.Get(_seatMaterialId);
            var legsDef = MaterialCatalog.Get(_legsMaterialId);
            if (seatDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(seatDef);
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
                _cornerRadiusMM = ClampCornerRadius(_cornerRadiusMM);
                transform.localScale = Vector3.one;
                RebuildSeat();
                PlaceLegs();
                MaterialManager.RefreshTiling(this);
            }
            finally
            {
                _applying = false;
            }
        }

        private void RebuildSeat()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            float widthU = dims.x * toU;
            float depthU = dims.z * toU;
            float thicknessU = SeatThicknessMM * toU;
            float centreYU = FurnitureLayout.TopCentreY(dims.y, SeatThicknessMM);

            var profile = RoundedRectProfile.Uniform(widthU, depthU, _cornerRadiusMM * toU,
                RoundedRectProfile.DefaultSegments);
            var mesh = ProfileExtrusionMesh.Build(profile, widthU, depthU, thicknessU, centreYU);
            AdoptOwnedMesh(mesh);

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_seatMaterial != null) meshRenderer.sharedMaterial = _seatMaterial;

            UpdateCollider(mesh);
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

        private void PlaceLegs()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            int legHeightMM = FurnitureLayout.LegHeightMM(dims.y, SeatThicknessMM);
            float legCentreYU = FurnitureLayout.LegCentreY(dims.y, SeatThicknessMM);

            var footprint = RoundedRectSeating.LegCentres(dims.x * toU, dims.z * toU,
                _cornerRadiusMM * toU, LegInsetMM * toU, LegCrossSectionMM * toU);
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

        private void EnsureLegs(int count)
        {
            while (_legs.Count < count)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg" + (_legs.Count + 1);
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
            _seatMaterial = material;
            var seatRenderer = GetComponent<MeshRenderer>();
            if (seatRenderer != null) seatRenderer.sharedMaterial = _seatMaterial;
        }

        public void SetLegsMaterial(Material material)
        {
            _legsMaterial = material;
            foreach (var leg in _legs)
            {
                var renderer = leg.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = _legsMaterial;
            }
        }

        public void SetMaterial(Material material)
        {
            SetTabletopMaterial(material);
            SetLegsMaterial(material);
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
