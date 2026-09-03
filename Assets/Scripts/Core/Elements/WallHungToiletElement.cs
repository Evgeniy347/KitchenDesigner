using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class WallHungToiletElement : KitchenElement, IHasTwoDecorSlots, IFixedSizeElement,
        IWallMounted, IStandsOnFloor
    {
        public override string DisplayTypeName => "Унитаз подвесной";

        public const int DefaultWidthMM = WallHungToiletLayout.WidthMM;
        public const int DefaultHeightMM = WallHungToiletLayout.HeightMM;
        public const int DefaultDepthMM = WallHungToiletLayout.DepthMM;
        public const int DefaultSeatHeightMM = WallHungToiletLayout.DefaultSeatHeightMM;
        public const int DefaultFlushPlateHeightMM = WallHungToiletLayout.DefaultPlateBottomMM;

        private const int SeatBeforePlateOrder = -100;

        private FurniturePartSet? _ceramic;
        private FurniturePartSet? _chrome;
        private readonly RebuildGuard _rebuild = new RebuildGuard();
        private int _lastPoseVersion;

        [SerializeField] private int _seatHeightMM = WallHungToiletLayout.DefaultSeatHeightMM;
        [SerializeField] private int _flushPlateHeightMM =
            WallHungToiletLayout.DefaultPlateBottomMM;
        [SerializeField] private string _ceramicMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _plateMaterialId = MaterialCatalog.DefaultId;

        public static Vector3Int ModelDimensionsMM => WallHungToiletLayout.DimensionsMM;

        public static int MinSeatHeightMM => WallHungToiletLayout.MinSeatHeightMM;

        public static int MaxSeatHeightMM => WallHungToiletLayout.MaxSeatHeightMM;

        public static int MinFlushPlateHeightMM(int seatHeightMM) =>
            WallHungToiletLayout.MinPlateBottomMM(seatHeightMM);

        public static int MaxFlushPlateHeightMM => WallHungToiletLayout.MaxPlateBottomMM;

        public bool HasFixedSize => true;

        public override bool CanFollowAnAttachParent => false;

        public int BowlBottomMM => WallHungToiletLayout.BowlBottomMM(_seatHeightMM);

        protected override Vector3 EffectiveScale =>
            FurnitureLayout.PhysicalScale(WallHungToiletLayout.DimensionsMM);

        public override Vector2Int DecorSurfaceMM =>
            FurnitureLayout.TopSurfaceMM(WallHungToiletLayout.DimensionsMM);

        public override MeshRenderer? DecorRenderer =>
            Ceramic.RendererOf(WallHungToiletLayout.BowlName);

        [Undoable(Order = SeatBeforePlateOrder)]
        public int SeatHeightMM
        {
            get => _seatHeightMM;
            set
            {
                value = WallHungToiletLayout.ClampSeatHeightMM(value);
                if (_seatHeightMM == value) return;
                _seatHeightMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int FlushPlateHeightMM
        {
            get => _flushPlateHeightMM;
            set
            {
                value = WallHungToiletLayout.ClampPlateBottomMM(_seatHeightMM, value);
                if (_flushPlateHeightMM == value) return;
                _flushPlateHeightMM = value;
                ApplyDimensions();
            }
        }

        [NotUndoable(TabletopDecor.TabletopSlotReason)]
        public string PrimaryMaterialId
        {
            get => _ceramicMaterialId;
            set { _ceramicMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.LegsSlotReason)]
        public string SecondaryMaterialId
        {
            get => _plateMaterialId;
            set { _plateMaterialId = TabletopDecor.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(TabletopDecor.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => PrimaryMaterialId;
            set => PrimaryMaterialId = value;
        }

        public string PrimarySlotLabel => SanitaryDecor.CeramicLabel;

        public string SecondarySlotLabel => SanitaryDecor.PlateLabel;

        private FurniturePartSet Ceramic => _ceramic ??= new FurniturePartSet(transform);

        private FurniturePartSet Chrome => _chrome ??= new FurniturePartSet(transform);

        private void Start() => SnapToWall();

        internal void Update()
        {
            if (PoseVersion == _lastPoseVersion) return;
            _lastPoseVersion = PoseVersion;
            SnapToWall();
        }

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _seatHeightMM = WallHungToiletLayout.ClampSeatHeightMM(_seatHeightMM);
            _flushPlateHeightMM = WallHungToiletLayout.ClampPlateBottomMM(_seatHeightMM,
                _flushPlateHeightMM);

            transform.localScale = Vector3.one;
            Data.DimensionsMM = WallHungToiletLayout.DimensionsMM;

            var dims = WallHungToiletLayout.DimensionsMM;
            ApplianceCollider.FitBox(gameObject, new Vector3(dims.x, dims.y, dims.z),
                Vector3.zero);

            if (SuppressVisualRebuild) return;

            Ceramic.Place(WallHungToiletLayout.CeramicParts(_seatHeightMM));
            Chrome.Place(WallHungToiletLayout.ChromeParts(_seatHeightMM, _flushPlateHeightMM));
            ApplyMaterial();
            MaterialManager.RefreshTiling(this);
        }

        public void SnapToWall() => WallSeating.Seat(this, WallHungToiletLayout.DepthMM);

        public void SeatOnFloor(IReadOnlyList<KitchenElement> scene) =>
            FloorSeating.Seat(this, scene);

        public void SetPrimaryMaterial(Material material) => Ceramic.SetMaterial(
            SanitaryDecor.ChosenOrFactory(_ceramicMaterialId, material, SanitaryMaterials.Ceramic));

        public void SetSecondaryMaterial(Material material) => Chrome.SetMaterial(
            SanitaryDecor.ChosenOrFactory(_plateMaterialId, material, SanitaryMaterials.Chrome));

        public void SetMaterial(Material material) => TabletopDecor.SetBothSlots(this, material);

        private void ApplyMaterial()
            => TabletopDecor.ApplyBothSlots(this, _ceramicMaterialId, _plateMaterialId);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren()
        {
            _ceramic?.Destroy();
            _chrome?.Destroy();
        }
    }
}
