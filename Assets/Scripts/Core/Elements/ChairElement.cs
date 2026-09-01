using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ChairElement : KitchenElement, ITabletop
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
        private bool _applying;

        [SerializeField] private int _cornerRadiusMM;
        [SerializeField] private int _seatHeightMM = AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT;
        [SerializeField] private string _seatMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => Mathf.Max(0, Mathf.Min(dimensionsMM.x, dimensionsMM.z) / 2);

        public static int MaxSeatHeightMM(int overallHeightMM)
            => Mathf.Max(1, overallHeightMM - MinBackrestHeightMM);

        public static int MinSeatHeightMM(int overallHeightMM)
            => Mathf.Min(SeatThicknessMM + MinLegHeightMM, MaxSeatHeightMM(overallHeightMM));

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

        private LegSet Legs => _legSet ??= new LegSet(transform, "Leg");

        private ChairBackrest Backrest
            => _backrest ??= new ChairBackrest(transform, BackrestChildName);

        private TabletopSurface Seat => _seat ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private int ClampCornerRadius(int value)
            => Mathf.Clamp(value, 0, MaxCornerRadiusMM(DimensionsMM));

        private int ClampSeatHeight(int value)
            => Mathf.Clamp(value, MinSeatHeightMM(DimensionsMM.y), MaxSeatHeightMM(DimensionsMM.y));

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _seatMaterialId, _legsMaterialId);

        public override void ApplyDimensions()
        {
            if (_applying) return;
            _applying = true;
            try
            {
                _cornerRadiusMM = ClampCornerRadius(_cornerRadiusMM);
                _seatHeightMM = ClampSeatHeight(_seatHeightMM);
                transform.localScale = Vector3.one;
                RebuildSeat();
                PlaceLegs();
                PlaceBackrest();
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

        public void SetTabletopMaterial(Material material)
        {
            Seat.SetMaterial(material);
            Backrest.SetMaterial(material);
        }

        public void SetLegsMaterial(Material material) => Legs.SetMaterial(material);

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren()
        {
            _legSet?.Destroy();
            _backrest?.Destroy();
        }
    }
}
