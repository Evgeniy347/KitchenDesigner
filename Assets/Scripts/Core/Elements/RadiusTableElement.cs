using UnityEngine;

namespace KitchenDesigner.Core
{
    public class RadiusTableElement : KitchenElement, ITabletop
    {

        public override string DisplayTypeName => "Радиусный стол";
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;
        public const int MinLegInsetFromContourMM = 50;

        private LegSet? _legSet;
        private TabletopSurface? _tabletop;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _legInsetMM = 100;
        [SerializeField] private string _tabletopMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        [Undoable]
        public int LegInsetMM
        {
            get => _legInsetMM;
            set { _legInsetMM = Mathf.Max(0, value); ApplyDimensions(); }
        }

        [NotUndoable(TabletopDecor.TabletopSlotReason)]
        public string TabletopMaterialId
        {
            get => _tabletopMaterialId;
            set { _tabletopMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.LegsSlotReason)]
        public string LegsMaterialId
        {
            get => _legsMaterialId;
            set { _legsMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        private LegSet Legs => _legSet ??= new LegSet(transform, "Leg");

        private TabletopSurface Tabletop
            => _tabletop ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _tabletopMaterialId, _legsMaterialId);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
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

        private void RebuildTabletop(Vector3Int dims, float widthU, float depthU, float radiusU)
        {
            Tabletop.Rebuild(widthU, depthU, radiusU,
                TabletopThicknessMM * AppConstants.MM_TO_UNITS,
                FurnitureLayout.TopCentreY(dims.y, TabletopThicknessMM));
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

            Legs.Place(footprint, legCentreYU, legScale);
        }

        public void SetTabletopMaterial(Material material) => Tabletop.SetMaterial(material);

        public void SetLegsMaterial(Material material) => Legs.SetMaterial(material);

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren() => _legSet?.Destroy();
    }
}
