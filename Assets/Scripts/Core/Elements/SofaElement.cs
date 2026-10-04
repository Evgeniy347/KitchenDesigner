using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SofaElement : KitchenElement, IHasTwoDecorSlots, IQuantifies, IOpenable
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("подушки дивана стоят между подлокотниками: ни одна из них не защищена от собственного подлокотника, поэтому проверяемой лицевой детали у типа нет");

        public override string DisplayTypeName => Loc.T("elementType.sofa");

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        public const string DefaultUpholsteryId = "fabric_mustard";
        private static readonly MaterialDef BoxMaterial = new MaterialDef("sofa_box_laminate",
            "Sofa box", "Laminate", new Color(0.93f, 0.93f, 0.9f));
        private static readonly MaterialDef DrawerFrontMaterial = new MaterialDef("sofa_drawer_front",
            "Sofa drawer front", "Laminate", new Color(0.55f, 0.42f, 0.30f));

        public const int DefaultWidthMM = SofaLayout.DefaultWidthMM;
        public const int DefaultHeightMM = SofaLayout.DefaultHeightMM;
        public const int DefaultDepthMM = SofaLayout.DefaultDepthMM;
        public const int DefaultSeatHeightMM = SofaLayout.DefaultSeatHeightMM;
        public const int DefaultCornerRadiusMM = SofaLayout.DefaultCornerRadiusMM;
        public const int MinSeatHeightMM = SofaLayout.MinSeatHeightMM;
        public const int MaxSeatHeightMM = SofaLayout.MaxSeatHeightMM;

        private SofaRig? _rig;
        private OwnedMeshBody? _box;
        private readonly SofaUnfoldMotion _motion = new SofaUnfoldMotion();
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _cornerRadiusMM = DefaultCornerRadiusMM;
        [SerializeField] private int _seatHeightMM = DefaultSeatHeightMM;
        [SerializeField] private string _bodyMaterialId = DefaultUpholsteryId;
        [SerializeField] private string _cushionMaterialId = DefaultUpholsteryId;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => SofaLayout.SeatSurfaceMM(DimensionsMM);

        public override MeshRenderer? DecorRenderer => Rig.SeatRenderer;

        public SofaStage UnfoldStage => _motion.Target;

        public bool IsOpen => _motion.Target != SofaStage.Folded;

        public bool IsClosedPose => true;

        public string OpenActionLabel => _motion.Target switch
        {
            SofaStage.Folded => OpenLabels.SofaExtend,
            SofaStage.Extended => OpenLabels.SofaUnfold,
            _ => OpenLabels.SofaFold,
        };

        [Undoable]
        public int CornerRadiusMM
        {
            get => _cornerRadiusMM;
            set
            {
                value = SofaLayout.ClampCornerRadiusMM(DimensionsMM, value);
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
                value = SofaLayout.ClampSeatHeightMM(value);
                if (_seatHeightMM == value) return;
                _seatHeightMM = value;
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
            get => _cushionMaterialId;
            set { _cushionMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => PrimaryMaterialId;
            set => PrimaryMaterialId = value;
        }

        private SofaRig Rig => _rig ??= new SofaRig(transform);

        private OwnedMeshBody Box => _box ??= new OwnedMeshBody(gameObject, AdoptOwnedMesh);

        public void ToggleOpen() => GoToStage(IsOpen ? SofaStage.Folded : SofaStage.Bed);

        public void CycleOpenState() => GoToStage(SofaUnfold.Next(_motion.Target));

        public void ForceClose() { }

        public void GoToStage(SofaStage stage)
        {
            if (stage == _motion.Target) return;
            _motion.GoTo(stage);
            enabled = true;
            FrameRateManager.KeepAwake(
                _motion.RemainingSeconds + AppConstants.OPENING_KEEP_AWAKE_MARGIN_SECONDS);
        }

        public void SnapToStage(SofaStage stage)
        {
            _motion.Snap(stage);
            ApplyPose();
        }

        public bool Advance(float seconds)
        {
            float before = _motion.Progress;
            bool moving = _motion.Advance(seconds);
            if (_motion.Progress != before) ApplyPose();
            return moving;
        }

        private void Update()
        {
            if (!Advance(Time.deltaTime)) enabled = false;
        }

        private void ApplyMaterial()
            => DecorSlots.ApplyBothSlots(this, _bodyMaterialId, _cushionMaterialId);

        private void ApplyPose()
            => Rig.ApplyPose(SofaUnfold.PoseAt(_motion.Progress, DimensionsMM.z, _seatHeightMM));

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            var dims = SofaLayout.Normalise(DimensionsMM);
            if (dims != DimensionsMM) Data.DimensionsMM = dims;
            _cornerRadiusMM = SofaLayout.ClampCornerRadiusMM(dims, _cornerRadiusMM);
            _seatHeightMM = SofaLayout.ClampSeatHeightMM(_seatHeightMM);
            transform.localScale = Vector3.one;
            RebuildBox(dims);
            Rig.Build(dims, _seatHeightMM, _cornerRadiusMM);
            ApplyPose();
            MaterialManager.RefreshTiling(this);
        }

        private void RebuildBox(Vector3Int dims)
        {
            Box.Rebuild(SofaBoxMesh.Build(
                SofaBoxLayout.Panels(dims, _seatHeightMM, _cornerRadiusMM)));

            var material = MaterialManager.GetSharedMaterial(BoxMaterial);
            if (material != null) Box.SetMaterial(material);
            var front = MaterialManager.GetSharedMaterial(DrawerFrontMaterial);
            if (front != null) Rig.SetDrawerFronts(front);
        }

        public void SetPrimaryMaterial(Material material) => Rig.SetUpholstery(material);

        public void SetSecondaryMaterial(Material material) => Rig.SetCushions(material);

        public string PrimarySlotLabel => DecorSlots.UpholsteryLabel;

        public string SecondarySlotLabel => DecorSlots.CushionsLabel;

        public void SetMaterial(Material material) => DecorSlots.SetBothSlots(this, material);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren() => _rig?.Destroy();
    }
}
