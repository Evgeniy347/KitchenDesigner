using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SofaElement : KitchenElement, ITabletop
    {
        public override string DisplayTypeName => "Диван";

        public const int DefaultWidthMM = SofaLayout.DefaultWidthMM;
        public const int DefaultHeightMM = SofaLayout.DefaultHeightMM;
        public const int DefaultDepthMM = SofaLayout.DefaultDepthMM;
        public const int DefaultSeatHeightMM = SofaLayout.DefaultSeatHeightMM;
        public const int DefaultCornerRadiusMM = SofaLayout.DefaultCornerRadiusMM;
        public const int MinBaseHeightMM = SofaLayout.MinBaseHeightMM;
        public const int MinBackrestHeightMM = SofaLayout.MinBackrestHeightMM;

        private SofaUpholstery? _body;
        private SofaUpholstery? _cushions;
        private TabletopSurface? _base;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _cornerRadiusMM = DefaultCornerRadiusMM;
        [SerializeField] private int _seatHeightMM = DefaultSeatHeightMM;
        [SerializeField] private string _bodyMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _cushionMaterialId = MaterialCatalog.DefaultId;

        public static int MaxCornerRadiusMM(Vector3Int dimensionsMM)
            => Mathf.Max(0, Mathf.Min(dimensionsMM.x, dimensionsMM.z) / 2);

        public static int MaxSeatHeightMM(int overallHeightMM)
            => Mathf.Max(1, overallHeightMM - MinBackrestHeightMM);

        public static int MinSeatHeightMM(int overallHeightMM)
            => Mathf.Min(MinBaseHeightMM, MaxSeatHeightMM(overallHeightMM));

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

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

        [NotUndoable(TabletopDecor.TabletopSlotReason)]
        public string TabletopMaterialId
        {
            get => _bodyMaterialId;
            set { _bodyMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.LegsSlotReason)]
        public string LegsMaterialId
        {
            get => _cushionMaterialId;
            set { _cushionMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

        private SofaUpholstery Body => _body ??= new SofaUpholstery(transform);

        private SofaUpholstery Cushions => _cushions ??= new SofaUpholstery(transform);

        private TabletopSurface Base => _base ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private int ClampCornerRadius(int value)
            => Mathf.Clamp(value, 0, MaxCornerRadiusMM(DimensionsMM));

        private int ClampSeatHeight(int value)
            => Mathf.Clamp(value, MinSeatHeightMM(DimensionsMM.y), MaxSeatHeightMM(DimensionsMM.y));

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _bodyMaterialId, _cushionMaterialId);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _cornerRadiusMM = ClampCornerRadius(_cornerRadiusMM);
            _seatHeightMM = ClampSeatHeight(_seatHeightMM);
            transform.localScale = Vector3.one;
            RebuildBase();
            Body.Place(new[] { SofaLayout.BackRail(DimensionsMM, _seatHeightMM) });
            Cushions.Place(SofaLayout.Cushions(DimensionsMM, _seatHeightMM));
            MaterialManager.RefreshTiling(this);
        }

        private void RebuildBase()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            Base.Rebuild(dims.x * toU, dims.z * toU, _cornerRadiusMM * toU,
                _seatHeightMM * toU, SofaLayout.BaseCentreYMM(dims.y, _seatHeightMM) * toU);
        }

        public void SetTabletopMaterial(Material material)
        {
            Base.SetMaterial(material);
            Body.SetMaterial(material);
        }

        public void SetLegsMaterial(Material material) => Cushions.SetMaterial(material);

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren()
        {
            _body?.Destroy();
            _cushions?.Destroy();
        }
    }
}
