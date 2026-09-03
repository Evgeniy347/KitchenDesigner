using UnityEngine;

namespace KitchenDesigner.Core
{
    public class RadiusTableElement : KitchenElement, IHasTwoDecorSlots
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

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        private LegSet Legs => _legSet ??= new LegSet(transform, "Leg");

        private TabletopSurface Tabletop
            => _tabletop ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private void ApplyMaterial()
            => DecorSlots.ApplyBothSlots(this, _tabletopMaterialId, _legsMaterialId);

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

        public void SetPrimaryMaterial(Material material) => Tabletop.SetMaterial(material);

        public void SetSecondaryMaterial(Material material) => Legs.SetMaterial(material);

        public string PrimarySlotLabel => DecorSlots.TabletopLabel;

        public string SecondarySlotLabel => DecorSlots.LegsLabel;

        public void SetMaterial(Material material) => DecorSlots.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren() => _legSet?.Destroy();
    }
}
