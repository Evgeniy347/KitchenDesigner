using UnityEngine;

namespace KitchenDesigner.Core
{
    public class BedElement : KitchenElement, ITabletop
    {
        public override string DisplayTypeName => "Кровать";

        public const int DefaultWidthMM = BedLayout.DoubleWidthMM;
        public const int DefaultHeightMM = BedLayout.DefaultHeightWithHeadboardMM;
        public const int DefaultDepthMM = BedLayout.LengthMM;

        public const string SizeSingle = "single";
        public const string SizeDouble = "double";

        private const int ResetsDimensionsOrder = -200;

        private LegSet? _legSet;
        private BedPartSet? _bedding;
        private BedPartSet? _carcass;
        private TabletopSurface? _frame;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private bool _isDouble = true;
        [SerializeField] private bool _hasHeadboard = true;
        [SerializeField] private string _frameMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _beddingMaterialId = MaterialCatalog.DefaultId;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public string SizeName => _isDouble ? SizeDouble : SizeSingle;

        public int PillowCount => BedLayout.PillowCount(_isDouble);

        public int LegCount => BedLayout.LegCount(_isDouble);

        [Undoable(Order = ResetsDimensionsOrder)]
        public bool IsDouble
        {
            get => _isDouble;
            set
            {
                if (_isDouble == value) return;
                _isDouble = value;
                DimensionsMM = BedLayout.DefaultDimensions(_isDouble, _hasHeadboard);
            }
        }

        [Undoable(Order = ResetsDimensionsOrder)]
        public bool HasHeadboard
        {
            get => _hasHeadboard;
            set
            {
                if (_hasHeadboard == value) return;
                _hasHeadboard = value;
                var dims = DimensionsMM;
                DimensionsMM = new Vector3Int(dims.x, BedLayout.HeightFor(_hasHeadboard), dims.z);
            }
        }

        [NotUndoable(TabletopDecor.TabletopSlotReason)]
        public string TabletopMaterialId
        {
            get => _frameMaterialId;
            set { _frameMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.LegsSlotReason)]
        public string LegsMaterialId
        {
            get => _beddingMaterialId;
            set { _beddingMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

        private LegSet Legs => _legSet ??= new LegSet(transform, BedLayout.LegNamePrefix);

        private BedPartSet Bedding => _bedding ??= new BedPartSet(transform);

        private BedPartSet Carcass => _carcass ??= new BedPartSet(transform);

        private TabletopSurface Frame => _frame ??= new TabletopSurface(gameObject, AdoptOwnedMesh);

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _frameMaterialId, _beddingMaterialId);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            ClampHeight();
            transform.localScale = Vector3.one;
            RebuildFrame();
            PlaceLegs();
            PlaceBedding();
            PlaceHeadboard();
            MaterialManager.RefreshTiling(this);
        }

        private void ClampHeight()
        {
            var dims = DimensionsMM;
            int min = BedLayout.MinHeightMM(_hasHeadboard);
            if (dims.y >= min) return;
            DimensionsMM = new Vector3Int(dims.x, min, dims.z);
        }

        private void RebuildFrame()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            Frame.Rebuild(dims.x * toU, dims.z * toU, BedLayout.FrameCornerRadiusMM * toU,
                BedLayout.FrameHeightMM * toU, BedLayout.FrameCentreYMM(dims.y) * toU);
        }

        private void PlaceLegs()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = DimensionsMM;
            var footprint = BedLayout.LegCentresMM(dims, _isDouble);
            for (int i = 0; i < footprint.Length; i++) footprint[i] *= toU;

            Legs.Place(footprint, BedLayout.LegCentreYMM(dims.y) * toU,
                new Vector3(BedLayout.LegCrossSectionMM * toU, BedLayout.LegHeightMM * toU,
                    BedLayout.LegCrossSectionMM * toU));
        }

        private void PlaceBedding()
        {
            var dims = DimensionsMM;
            Bedding.SoftSlab(BedLayout.MattressName, BedLayout.MattressCentreMM(dims),
                BedLayout.MattressSizeMM(dims), BedLayout.MattressPlanRadiusMM,
                BedLayout.MattressFilletMM);

            var centres = BedLayout.PillowCentresMM(dims, _isDouble);
            var size = BedLayout.PillowSizeMM(dims, _isDouble);
            for (int i = 0; i < centres.Length; i++)
                Bedding.Cushion(BedLayout.PillowName(i), centres[i], size,
                    BedLayout.PillowCornerRadiusMM);
            for (int i = centres.Length; i < BedLayout.DoublePillowCount; i++)
                Bedding.Remove(BedLayout.PillowName(i));
        }

        private void PlaceHeadboard()
        {
            if (!_hasHeadboard)
            {
                Carcass.Remove(BedLayout.HeadboardName);
                return;
            }

            var dims = DimensionsMM;
            float panelHeight = BedLayout.HeadboardPanelHeightMM(dims.y);
            float radius = BedLayout.FittedRadiusMM(BedLayout.HeadboardCornerRadiusMM,
                dims.x, panelHeight);

            Carcass.Extrusion(BedLayout.HeadboardName, BedLayout.HeadboardCentreMM(dims),
                BedLayout.HeadboardEulerAngles, dims.x, panelHeight,
                BedLayout.HeadboardThicknessMM, radius);
        }

        public void SetTabletopMaterial(Material material)
        {
            Frame.SetMaterial(material);
            Carcass.SetMaterial(material);
            Legs.SetMaterial(material);
        }

        public void SetLegsMaterial(Material material) => Bedding.SetMaterial(material);

        public string TabletopSlotLabel => TabletopDecor.FrameLabel;

        public string LegsSlotLabel => TabletopDecor.MattressLabel;

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren()
        {
            _legSet?.Destroy();
            _bedding?.Destroy();
            _carcass?.Destroy();
        }
    }
}
