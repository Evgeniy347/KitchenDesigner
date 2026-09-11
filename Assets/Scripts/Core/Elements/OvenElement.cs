using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class OvenElement : KitchenElement, IFixedSizeElement, IOpenable, IPaintsItself,
        IQuantifies
    {
        public override ElementFront Front =>
            ElementFront.Parts(OvenBody.PartName(OvenBody.IdxGlass),
                OvenBody.PartName(OvenBody.IdxPanel),
                OvenBody.PartName(OvenBody.IdxHandle));

        public override bool CanFollowAnAttachParent => false;

        public override string DisplayTypeName => MODEL;

        public bool IsClosedPose => !IsOpen && !IsAnimating;

        public string OpenActionLabel => IsOpen ? OpenLabels.CloseDoor : OpenLabels.OpenDoor;

        public void CycleOpenState() => ToggleOpen();

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        public const string MODEL = "Bosch HBA514BB3";

        public static Vector3Int ModelDimensionsMM => OvenBody.ModelDimensionsMM;

        public const float DOOR_OPEN_ANGLE_DEG = DropDoor.OPEN_ANGLE_DEG;

        [SerializeField] private bool _open;

        private ApplianceBoxes? _boxes;
        private DropDoor? _door;

        private ApplianceBoxes Boxes => _boxes ??= new ApplianceBoxes(transform, OvenBody.PartName);

        private DropDoor Door =>
            _door ??= new DropDoor(Boxes, OvenBody.BodyPartCount, OvenBody.DoorPartsMM,
                () => OvenBody.HingeLocalMM);

        public bool HasFixedSize => true;

        public bool IsOpen => _open;

        public float DoorProgress => Door.Progress;

        public bool IsAnimating => Door.IsAnimatingTowards(_open);

        protected override Vector3 EffectiveScale => new Vector3(
            OvenBody.BODY_WIDTH_MM * AppConstants.MM_TO_UNITS,
            OvenBody.BODY_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            OvenBody.BODY_DEPTH_MM * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, OvenBody.BODY_CENTER_Y_MM, OvenBody.BODY_CENTER_Z_MM)
                * AppConstants.MM_TO_UNITS;

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            Data.DimensionsMM = ModelDimensionsMM;

            var dims = ModelDimensionsMM;
            ApplianceCollider.FitBox(gameObject, new Vector3(dims.x, dims.y, dims.z), Vector3.zero);

            if (SuppressVisualRebuild) return;
            RebuildGeometry();
        }

        public static Quaternion DoorLocalRotation(float progress) => DropDoor.LocalRotation(progress);

        private void RebuildGeometry()
        {
            Boxes.Ensure(OvenBody.PartCount);

            var body = OvenBody.BodyPartsMM();
            for (int i = 0; i < OvenBody.BodyPartCount; i++)
                Boxes.Place(i, body[i].centerMM, body[i].sizeMM, Quaternion.identity);

            Door.ApplyPose();
            ApplyMaterials();
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (Door.IsAnimatingTowards(open))
            {
                enabled = true;
                FrameRateManager.KeepAwake(DropDoor.OPEN_SECONDS + DropDoor.KEEP_AWAKE_MARGIN_SECONDS);
            }
        }

        public void ToggleOpen() => SetOpen(!_open);

        public void ForceClose()
        {
            bool wasOpen = _open;
            _open = false;
            if (!Door.ForceClose() && !wasOpen) return;
            Door.ApplyPose();
        }

        internal void Update()
        {
            StepDoor(Time.deltaTime);
            if (Mathf.Approximately(Door.Progress, _open ? 1f : 0f)) enabled = false;
        }

        public void StepDoor(float dt)
        {
            if (!Door.Step(dt, _open, MaxSafeDoorProgress)) return;
            Door.ApplyPose();
        }

        private float MaxSafeDoorProgress() => OpeningCollision.FindMaxProgress(this, GetOpenBoxes);

        public void GetOpenBoxes(float progress, List<OrientedBox> into) =>
            Door.WorldBoxes(transform, progress, into);

        public override MeshRenderer? DecorRenderer => Boxes.RendererOf(OvenBody.IdxFacade);

        private void ApplyMaterials()
        {
            var facade = Skin();
            for (int i = 0; i < OvenBody.PartCount; i++)
                Boxes.SetMaterial(i, i switch
                {
                    OvenBody.IdxFacade => facade,
                    OvenBody.IdxGlass => ApplianceMaterials.OvenGlass,
                    OvenBody.IdxPanel => ApplianceMaterials.OvenPanel,
                    OvenBody.IdxHandle => ApplianceMaterials.OvenHandle,
                    _ => ApplianceMaterials.OvenBody,
                });
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return ApplianceMaterials.OvenFacade;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : ApplianceMaterials.OvenFacade;
        }

        public void SetMaterial(Material material) => Boxes.SetMaterial(OvenBody.IdxFacade,
            SanitaryDecor.ChosenOrFactory(MaterialId, material,
                ApplianceMaterials.OvenFacade));

        public void DestroyChildren() => Boxes.Destroy();

        protected override void OnElementDestroyed()
        {
            DestroyChildren();
        }
    }
}
