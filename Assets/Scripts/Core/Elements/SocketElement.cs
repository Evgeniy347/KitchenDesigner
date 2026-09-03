using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SocketElement : KitchenElement, IHasTwoDecorSlots, IWallMounted, IWallDevice,
        IKeepsPlacementHeight
    {
        public override string DisplayTypeName => "Розетка";

        public const int DefaultWidthMM = WallDeviceLayout.DefaultPlateWidthMM;
        public const int DefaultHeightMM = WallDeviceLayout.DefaultPlateHeightMM;
        public const int DefaultDepthMM = WallDeviceLayout.DefaultProtrusionMM;

        private readonly RebuildGuard _rebuild = new RebuildGuard();
        private WallDeviceParts? _parts;
        private int _lastPoseVersion;

        [SerializeField] private int _plateWidthMM = WallDeviceLayout.DefaultPlateWidthMM;
        [SerializeField] private int _plateHeightMM = WallDeviceLayout.DefaultPlateHeightMM;
        [SerializeField] private int _protrusionMM = WallDeviceLayout.DefaultProtrusionMM;
        [SerializeField] private int _postCount = WallDeviceLayout.DefaultPostCount;
        [SerializeField] private string _bodyMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _contactMaterialId = MaterialCatalog.DefaultId;

        public override bool CanFollowAnAttachParent => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override MeshRenderer? DecorRenderer
            => Parts.Body.RendererOf(WallDeviceLayout.RimTopName + "0");

        [Undoable]
        public int PlateWidthMM
        {
            get => _plateWidthMM;
            set
            {
                value = WallDeviceLayout.ClampPlateSideMM(value);
                if (_plateWidthMM == value) return;
                _plateWidthMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int PlateHeightMM
        {
            get => _plateHeightMM;
            set
            {
                value = WallDeviceLayout.ClampPlateSideMM(value);
                if (_plateHeightMM == value) return;
                _plateHeightMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int ProtrusionMM
        {
            get => _protrusionMM;
            set
            {
                value = WallDeviceLayout.ClampProtrusionMM(value);
                if (_protrusionMM == value) return;
                _protrusionMM = value;
                ApplyDimensions();
                SnapToWall();
            }
        }

        [Undoable]
        public int PostCount
        {
            get => _postCount;
            set
            {
                value = WallDeviceLayout.ClampPostCount(value);
                if (_postCount == value) return;
                _postCount = value;
                ApplyDimensions();
            }
        }

        [NotUndoable(DecorSlots.PrimarySlotReason)]
        public string PrimaryMaterialId
        {
            get => _bodyMaterialId;
            set { _bodyMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.SecondarySlotReason)]
        public string SecondaryMaterialId
        {
            get => _contactMaterialId;
            set { _contactMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => PrimaryMaterialId;
            set => PrimaryMaterialId = value;
        }

        public string PrimarySlotLabel => WallDeviceDecor.PlateLabel;

        public string SecondarySlotLabel => WallDeviceDecor.ContactLabel;

        public WallDeviceSpec Spec => WallDeviceSpec.Of(this);

        private WallDeviceParts Parts => _parts ??= new WallDeviceParts(this);

        private void Start() => SnapToWall();

        internal void Update()
        {
            if (PoseVersion == _lastPoseVersion) return;
            _lastPoseVersion = PoseVersion;
            SnapToWall();
        }

        public void SnapToWall() => WallSeating.Seat(this, WallDeviceLayout
            .ClampProtrusionMM(_protrusionMM));

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _plateWidthMM = WallDeviceLayout.ClampPlateSideMM(_plateWidthMM);
            _plateHeightMM = WallDeviceLayout.ClampPlateSideMM(_plateHeightMM);
            _protrusionMM = WallDeviceLayout.ClampProtrusionMM(_protrusionMM);
            _postCount = WallDeviceLayout.ClampPostCount(_postCount);

            Parts.Resize(this);
            if (SuppressVisualRebuild) return;

            Parts.Place(
                SocketLayout.BodyParts(_plateWidthMM, _plateHeightMM, _protrusionMM, _postCount),
                SocketLayout.ContactParts(_plateWidthMM, _plateHeightMM, _protrusionMM,
                    _postCount));
            ApplyMaterial();
            MaterialManager.RefreshTiling(this);
        }

        public void SetPrimaryMaterial(Material material) => Parts.Body.SetMaterial(
            SanitaryDecor.ChosenOrFactory(_bodyMaterialId, material, WallDeviceMaterials.Plastic));

        public void SetSecondaryMaterial(Material material) => Parts.Accent.SetMaterial(
            SanitaryDecor.ChosenOrFactory(_contactMaterialId, material,
                WallDeviceMaterials.Contact));

        public void SetMaterial(Material material) => DecorSlots.SetBothSlots(this, material);

        private void ApplyMaterial()
            => DecorSlots.ApplyBothSlots(this, _bodyMaterialId, _contactMaterialId);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren() => _parts?.Destroy();
    }
}
