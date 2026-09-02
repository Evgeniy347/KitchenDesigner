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

        private LegSet? _legSet;
        private TabletopSurface? _seat;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _cornerRadiusMM;
        [SerializeField] private string _seatMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        public const string ShapeSquare = "square";
        public const string ShapeRounded = "rounded";
        public const string ShapeRound = "round";

        public string ShapeName =>
            _cornerRadiusMM <= 0 ? ShapeSquare
            : _cornerRadiusMM >= FurnitureLayout.MaxCornerRadiusMM(DimensionsMM) ? ShapeRound
            : ShapeRounded;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        [Undoable]
        public int CornerRadiusMM
        {
            get => _cornerRadiusMM;
            set
            {
                value = FurnitureLayout.ClampCornerRadiusMM(DimensionsMM, value);
                if (_cornerRadiusMM == value) return;
                _cornerRadiusMM = value;
                ApplyDimensions();
            }
        }

        [NotUndoable(TabletopDecor.TabletopSlotReason)]
        public string TabletopMaterialId
        {
            get => _seatMaterialId;
            set { _seatMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
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

        private LegSet Legs => _legSet ??= new LegSet(transform, "Leg");

        private TabletopSurface Seat => _seat ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _seatMaterialId, _legsMaterialId);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _cornerRadiusMM = FurnitureLayout.ClampCornerRadiusMM(DimensionsMM, _cornerRadiusMM);
            transform.localScale = Vector3.one;
            RebuildSeat();
            PlaceLegs();
            MaterialManager.RefreshTiling(this);
        }

        private void RebuildSeat()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            Seat.Rebuild(dims.x * toU, dims.z * toU, _cornerRadiusMM * toU,
                SeatThicknessMM * toU, FurnitureLayout.TopCentreY(dims.y, SeatThicknessMM));
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

            Legs.Place(footprint, legCentreYU, legScale);
        }

        public void SetTabletopMaterial(Material material) => Seat.SetMaterial(material);

        public void SetLegsMaterial(Material material) => Legs.SetMaterial(material);

        public string TabletopSlotLabel => TabletopDecor.SeatLabel;

        public string LegsSlotLabel => TabletopDecor.LegsLabel;

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren() => _legSet?.Destroy();
    }
}
