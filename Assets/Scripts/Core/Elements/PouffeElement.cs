using UnityEngine;

namespace KitchenDesigner.Core
{
    public class PouffeElement : KitchenElement, IHasTwoDecorSlots
    {
        public override string DisplayTypeName => "Пуфик";

        public const int DefaultWidthMM = PouffeLayout.DefaultWidthMM;
        public const int DefaultHeightMM = PouffeLayout.DefaultHeightMM;
        public const int DefaultDepthMM = PouffeLayout.DefaultDepthMM;
        public const int DefaultCornerRadiusMM = PouffeLayout.DefaultCornerRadiusMM;
        public const int DefaultSeatThicknessMM = PouffeLayout.DefaultSeatThicknessMM;
        public const string SeatChildName = PouffeLayout.SeatName;

        private TabletopSurface? _body;
        private FurniturePartSet? _seat;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _cornerRadiusMM = DefaultCornerRadiusMM;
        [SerializeField] private int _seatThicknessMM = DefaultSeatThicknessMM;
        [SerializeField] private string _bodyMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _seatMaterialId = MaterialCatalog.DefaultId;

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => PouffeLayout.MaxCornerRadiusMM(dimensionsMM);

        public static int MaxSeatThicknessMM(int overallHeightMM)
            => PouffeLayout.MaxSeatThicknessMM(overallHeightMM);

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        [Undoable]
        public int CornerRadiusMM
        {
            get => _cornerRadiusMM;
            set
            {
                value = PouffeLayout.ClampCornerRadiusMM(DimensionsMM, value);
                if (_cornerRadiusMM == value) return;
                _cornerRadiusMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int SeatThicknessMM
        {
            get => _seatThicknessMM;
            set
            {
                value = PouffeLayout.ClampSeatThicknessMM(DimensionsMM.y, value);
                if (_seatThicknessMM == value) return;
                _seatThicknessMM = value;
                ApplyDimensions();
            }
        }

        [NotUndoable(TabletopDecor.TabletopSlotReason)]
        public string PrimaryMaterialId
        {
            get => _bodyMaterialId;
            set { _bodyMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.LegsSlotReason)]
        public string SecondaryMaterialId
        {
            get => _seatMaterialId;
            set { _seatMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => PrimaryMaterialId;
            set => PrimaryMaterialId = value;
        }

        private TabletopSurface Body => _body ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private FurniturePartSet Seat => _seat ??= new FurniturePartSet(transform);

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _bodyMaterialId, _seatMaterialId);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _cornerRadiusMM = PouffeLayout.ClampCornerRadiusMM(DimensionsMM, _cornerRadiusMM);
            _seatThicknessMM = PouffeLayout.ClampSeatThicknessMM(DimensionsMM.y, _seatThicknessMM);
            transform.localScale = Vector3.one;
            RebuildBody();
            Seat.Place(new[]
                { PouffeLayout.Seat(DimensionsMM, _cornerRadiusMM, _seatThicknessMM) });
            MaterialManager.RefreshTiling(this);
        }

        private void RebuildBody()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            Body.Rebuild(dims.x * toU, dims.z * toU, _cornerRadiusMM * toU,
                PouffeLayout.BodyHeightMM(dims.y, _seatThicknessMM) * toU,
                PouffeLayout.BodyCentreY(dims.y, _seatThicknessMM));
        }

        public void SetPrimaryMaterial(Material material) => Body.SetMaterial(material);

        public void SetSecondaryMaterial(Material material) => Seat.SetMaterial(material);

        public string PrimarySlotLabel => TabletopDecor.UpholsteryLabel;

        public string SecondarySlotLabel => TabletopDecor.SeatLabel;

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren() => _seat?.Destroy();
    }
}
