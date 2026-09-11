using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class LaundryMachineElement : KitchenElement, IOpenable, IPaintsItself, IQuantifies
    {
        public static readonly Vector3 HingeAxis = Vector3.down;

        public const float DOOR_OPEN_ANGLE_DEG = DropDoor.OPEN_ANGLE_DEG;

        [SerializeField] private LaundryMachineKind _kind = LaundryMachineKind.Washer;
        [SerializeField] private bool _open;

        private ApplianceBoxes? _boxes;
        private DropDoor? _door;

        private ApplianceBoxes Boxes =>
            _boxes ??= new ApplianceBoxes(transform, LaundryMachineBody.PartName);

        private DropDoor Door => _door ??= new DropDoor(Boxes, LaundryMachineBody.BodyPartCount,
            () => LaundryMachineBody.DoorPartsMM(DimensionsMM),
            () => LaundryMachineBody.HingeLocalMM(DimensionsMM), HingeAxis);

        public override bool CanFollowAnAttachParent => false;

        public override string DisplayTypeName => LaundryMachineBody.NameOf(_kind);

        [Undoable]
        public LaundryMachineKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public bool IsOpen => _open;

        public float DoorProgress => Door.Progress;

        public bool IsAnimating => Door.IsAnimatingTowards(_open);

        public bool IsClosedPose => !IsOpen && !IsAnimating;

        public string OpenActionLabel => IsOpen ? OpenLabels.CloseDoor : OpenLabels.OpenDoor;

        public void CycleOpenState() => ToggleOpen();

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;

            var dims = DimensionsMM;
            ApplianceCollider.FitBox(gameObject, new Vector3(dims.x, dims.y, dims.z), Vector3.zero);

            if (SuppressVisualRebuild) return;
            RebuildGeometry();
        }

        private void RebuildGeometry()
        {
            Boxes.Ensure(LaundryMachineBody.PartCount);

            for (int i = 0; i < LaundryMachineBody.PartCount; i++)
                if (LaundryMachineBody.IsRound(i)) Boxes.SetMesh(i, HatchDiscMesh.Unit());

            var body = LaundryMachineBody.BodyPartsMM(DimensionsMM);
            for (int i = 0; i < LaundryMachineBody.BodyPartCount; i++)
                Boxes.Place(i, body[i].centerMM, body[i].sizeMM, Quaternion.identity);

            ApplyDoorPose();
            ApplyMaterials();
        }

        internal void ApplyDoorPose()
        {
            if (Boxes.Count < LaundryMachineBody.PartCount) return;
            Door.ApplyPose();
        }

        public Quaternion DoorLocalRotation(float progress) => Door.LocalRotationAt(progress);

        public void SetOpen(bool open)
        {
            _open = open;
            if (!Door.IsAnimatingTowards(open)) return;
            enabled = true;
            FrameRateManager.KeepAwake(DropDoor.OPEN_SECONDS + DropDoor.KEEP_AWAKE_MARGIN_SECONDS);
        }

        public void ToggleOpen() => SetOpen(!_open);

        public void ForceClose()
        {
            bool wasOpen = _open;
            _open = false;
            if (!Door.ForceClose() && !wasOpen) return;
            ApplyDoorPose();
        }

        internal void Update()
        {
            StepDoor(Time.deltaTime);
            if (Mathf.Approximately(Door.Progress, _open ? 1f : 0f)) enabled = false;
        }

        public void StepDoor(float dt)
        {
            if (!Door.Step(dt, _open, MaxSafeDoorProgress)) return;
            ApplyDoorPose();
        }

        private float MaxSafeDoorProgress() => OpeningCollision.FindMaxProgress(this, GetOpenBoxes);

        public void GetOpenBoxes(float progress, List<OrientedBox> into) =>
            Door.WorldBoxes(transform, progress, into);

        public override MeshRenderer? DecorRenderer =>
            Boxes.RendererOf(LaundryMachineBody.IdxFrontPanel);

        private void ApplyMaterials()
        {
            var front = Skin();
            for (int i = 0; i < LaundryMachineBody.PartCount; i++)
                Boxes.SetMaterial(i, i switch
                {
                    LaundryMachineBody.IdxControlPanel => ApplianceMaterials.LaundryPanel,
                    LaundryMachineBody.IdxHatchGlass => ApplianceMaterials.LaundryGlass,
                    _ => front,
                });
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return ApplianceMaterials.LaundryBody;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : ApplianceMaterials.LaundryBody;
        }

        public void SetMaterial(Material material)
        {
            var front = SanitaryDecor.ChosenOrFactory(MaterialId, material,
                ApplianceMaterials.LaundryBody);
            Boxes.SetMaterial(LaundryMachineBody.IdxShell, front);
            Boxes.SetMaterial(LaundryMachineBody.IdxFrontPanel, front);
            Boxes.SetMaterial(LaundryMachineBody.IdxHatchRim, front);
        }

        public void DestroyChildren() => Boxes.Destroy();

        protected override void OnElementDestroyed()
        {
            DestroyChildren();
        }
    }
}
