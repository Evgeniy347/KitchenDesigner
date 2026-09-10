using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TableElement : KitchenElement, IHasTwoDecorSlots, ISpecificationParts
    {

        public override string DisplayTypeName => "Стол";
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;

        private LegSet? _legSet;
        private GameObject? _tabletop;
        private Material? _tabletopMaterial;

        [SerializeField] private int _legInsetMM = 100;
        [SerializeField] private string _tabletopMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        [Undoable]
        public int LegInsetMM
        {
            get => _legInsetMM;
            set { _legInsetMM = Mathf.Max(0, value); ApplyDimensions(); }
        }

        [NotUndoable(DecorSlots.PrimarySlotReason)]
        public string PrimaryMaterialId
        {
            get => _tabletopMaterialId;
            set { _tabletopMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.SecondarySlotReason)]
        public string SecondaryMaterialId
        {
            get => _legsMaterialId;
            set { _legsMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => PrimaryMaterialId;
            set => PrimaryMaterialId = value;
        }

        public override MeshRenderer? DecorRenderer
            => _tabletop != null ? _tabletop.GetComponent<MeshRenderer>() : null;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        private LegSet Legs => LegSet.For(ref _legSet, transform);

        private void ApplyMaterial()
            => DecorSlots.ApplyBothSlots(this, _tabletopMaterialId, _legsMaterialId);

        public override void ApplyDimensions()
        {
            base.ApplyDimensions();

            var rootMf = GetComponent<MeshFilter>();
            if (rootMf != null) Object.DestroyImmediate(rootMf);
            var rootMr = GetComponent<MeshRenderer>();
            if (rootMr != null) Object.DestroyImmediate(rootMr);

            var dims = DimensionsMM;
            int overallW = dims.x;
            int overallH = dims.y;
            int overallD = dims.z;

            int legH = FurnitureLayout.LegHeightMM(overallH, TabletopThicknessMM);

            float toU = AppConstants.MM_TO_UNITS;
            float legCross = LegCrossSectionMM * toU;
            float topThicknessU = TabletopThicknessMM * toU;

            float psX = overallW * toU;
            float psY = overallH * toU;
            float psZ = overallD * toU;

            float legCenterY_world = FurnitureLayout.LegCentreY(overallH, TabletopThicknessMM);
            float topCenterY_world = FurnitureLayout.TopCentreY(overallH, TabletopThicknessMM);

            float halfW_world = overallW * 0.5f * toU;
            float halfD_world = overallD * 0.5f * toU;
            float legInset_world = legCross * 0.5f;
            float insetOffset_world = _legInsetMM * toU;

            float leftX_norm   = (-halfW_world + legInset_world + insetOffset_world) / psX;
            float rightX_norm  = ( halfW_world - legInset_world - insetOffset_world) / psX;
            float frontZ_norm  = (-halfD_world + legInset_world + insetOffset_world) / psZ;
            float backZ_norm   = ( halfD_world - legInset_world - insetOffset_world) / psZ;

            var footprint = new Vector2[]
            {
                new Vector2(leftX_norm,  frontZ_norm),
                new Vector2(rightX_norm, frontZ_norm),
                new Vector2(leftX_norm,  backZ_norm),
                new Vector2(rightX_norm, backZ_norm),
            };

            var legScale = new Vector3(legCross / psX, legH * toU / psY, legCross / psZ);

            Legs.Place(footprint, legCenterY_world / psY, legScale);

            var tabletop = EnsureTabletop();
            tabletop.transform.localPosition = new Vector3(0f, topCenterY_world / psY, 0f);
            tabletop.transform.localScale = new Vector3(1f, topThicknessU / psY, 1f);
            tabletop.transform.localRotation = Quaternion.identity;
        }

        private GameObject EnsureTabletop()
        {
            if (_tabletop != null) return _tabletop;

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Tabletop";
            top.transform.SetParent(transform, false);

            var topCollider = top.GetComponent<BoxCollider>();
            if (topCollider != null) Object.DestroyImmediate(topCollider);

            var renderer = top.GetComponent<MeshRenderer>();
            if (renderer != null && _tabletopMaterial != null)
                renderer.sharedMaterial = _tabletopMaterial;

            _tabletop = top;
            return top;
        }

        public void SetPrimaryMaterial(Material material)
        {
            _tabletopMaterial = material;
            var renderer = DecorRenderer;
            if (renderer != null) renderer.sharedMaterial = _tabletopMaterial;
        }

        public void SetSecondaryMaterial(Material material) => Legs.SetMaterial(material);

        public string PrimarySlotLabel => DecorSlots.TabletopLabel;

        public string SecondarySlotLabel => DecorSlots.LegsLabel;

        public void SetMaterial(Material material) => DecorSlots.SetBothSlots(this, material);

        public IEnumerable<AssembledFacadeMesh.Part> GetSpecParts()
        {
            var dims = DimensionsMM;
            yield return new AssembledFacadeMesh.Part
            {
                suffix = "Столешница",
                dimsMM = new Vector3Int(dims.x, TabletopThicknessMM, dims.z),
            };
        }

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren()
        {
            _legSet?.Destroy();
            if (_tabletop == null) return;
            if (Application.isPlaying)
                Object.Destroy(_tabletop);
            else
                Object.DestroyImmediate(_tabletop);
            _tabletop = null;
        }
    }
}
