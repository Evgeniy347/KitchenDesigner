using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ChairElement : KitchenElement, IHasTwoDecorSlots
    {
        public override string DisplayTypeName => "Стул";

        public const int DefaultWidthMM = 400;
        public const int DefaultHeightMM = 900;
        public const int DefaultDepthMM = 400;
        public const int SeatThicknessMM = 30;
        public const int LegCrossSectionMM = 40;
        public const int LegInsetMM = 30;
        public const int BackrestThicknessMM = 20;
        public const string BackrestChildName = "Backrest";
        public const int MinLegHeightMM = 50;
        public const int MinBackrestHeightMM = 50;

        private LegSet? _legSet;
        private ChairBackrest? _backrest;
        private TabletopSurface? _seat;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _cornerRadiusMM;
        [SerializeField] private int _seatHeightMM = AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT;
        [SerializeField] private string _seatMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        public static int MaxSeatHeightMM(int overallHeightMM)
            => Mathf.Max(1, overallHeightMM - MinBackrestHeightMM);

        public static int MinSeatHeightMM(int overallHeightMM)
            => Mathf.Min(SeatThicknessMM + MinLegHeightMM, MaxSeatHeightMM(overallHeightMM));

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

        [Undoable]
        public int SeatHeightMM
        {
            get => _seatHeightMM;
            set
            {
                value = ClampSeatHeight(value);
                if (_seatHeightMM == value) return;
                _seatHeightMM = value;
                ApplyDimensions();
            }
        }

        [NotUndoable(DecorSlots.PrimarySlotReason)]
        public string PrimaryMaterialId
        {
            get => _seatMaterialId;
            set { _seatMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
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

        private LegSet Legs => LegSet.For(ref _legSet, transform);

        private ChairBackrest Backrest
            => _backrest ??= new ChairBackrest(transform, BackrestChildName);

        private TabletopSurface Seat => _seat ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private int ClampSeatHeight(int value)
            => Mathf.Clamp(value, MinSeatHeightMM(DimensionsMM.y), MaxSeatHeightMM(DimensionsMM.y));

        private void ApplyMaterial()
            => DecorSlots.ApplyBothSlots(this, _seatMaterialId, _legsMaterialId);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _cornerRadiusMM = FurnitureLayout.ClampCornerRadiusMM(DimensionsMM, _cornerRadiusMM);
            _seatHeightMM = ClampSeatHeight(_seatHeightMM);
            transform.localScale = Vector3.one;
            RebuildSeat();
            PlaceLegs();
            PlaceBackrest();
            MaterialManager.RefreshTiling(this);
        }

        private void RebuildSeat()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            float centreYU = (_seatHeightMM - SeatThicknessMM * 0.5f - dims.y * 0.5f) * toU;
            Seat.Rebuild(dims.x * toU, dims.z * toU, _cornerRadiusMM * toU,
                SeatThicknessMM * toU, centreYU);
        }

        private void PlaceLegs()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            int legHeightMM = Mathf.Max(1, _seatHeightMM - SeatThicknessMM);
            float legCentreYU = (legHeightMM * 0.5f - dims.y * 0.5f) * toU;

            var footprint = RoundedRectSeating.LegCentres(dims.x * toU, dims.z * toU,
                _cornerRadiusMM * toU, LegInsetMM * toU, LegCrossSectionMM * toU);
            var legScale = new Vector3(LegCrossSectionMM * toU, legHeightMM * toU,
                LegCrossSectionMM * toU);

            Legs.Place(footprint, legCentreYU, legScale);
        }

        private void PlaceBackrest()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            int heightMM = Mathf.Max(1, dims.y - _seatHeightMM);
            int thicknessMM = Mathf.Min(BackrestThicknessMM, Mathf.Max(1, dims.z));

            float centreYU = (_seatHeightMM + heightMM * 0.5f - dims.y * 0.5f) * toU;
            float centreZU = (thicknessMM * 0.5f - dims.z * 0.5f) * toU;

            Backrest.Place(new Vector3(0f, centreYU, centreZU),
                new Vector3(dims.x * toU, heightMM * toU, thicknessMM * toU));
        }

        public void SetPrimaryMaterial(Material material)
        {
            Seat.SetMaterial(material);
            Backrest.SetMaterial(material);
        }

        public void SetSecondaryMaterial(Material material) => Legs.SetMaterial(material);

        public string PrimarySlotLabel => DecorSlots.SeatLabel;

        public string SecondarySlotLabel => DecorSlots.LegsLabel;

        public void SetMaterial(Material material) => DecorSlots.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren()
        {
            _legSet?.Destroy();
            _backrest?.Destroy();
        }
    }
}
