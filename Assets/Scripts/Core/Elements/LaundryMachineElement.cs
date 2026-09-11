using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class LaundryMachineElement : KitchenElement, IOpenable, IPaintsItself, IQuantifies
    {
        public override ElementFront Front =>
            ElementFront.Parts(LaundryMachineBody.PartName(LaundryMachineBody.IdxControlPanel),
                LaundryMachineBody.PartName(LaundryMachineBody.IdxHatchGlass));

        public static readonly Vector3 HingeAxis = Vector3.down;

        public const float DOOR_OPEN_ANGLE_DEG = DropDoor.OPEN_ANGLE_DEG;

        [SerializeField] private LaundryMachineKind _kind = LaundryMachineKind.Washer;
        [SerializeField] private bool _open;

        private ApplianceBoxes? _boxes;
        private DropDoor? _door;
        private Mesh? _shellMesh;

        private ApplianceBoxes Boxes =>
            _boxes ??= new ApplianceBoxes(transform, LaundryMachineBody.PartName);

        private DropDoor Door => _door ??= new DropDoor(Boxes, LaundryMachineBody.BodyPartCount,
            () => LaundryMachineBody.DoorPartsMM(_kind, DimensionsMM),
            () => LaundryMachineBody.HingeLocalMM(_kind, DimensionsMM), HingeAxis);

        public override bool CanFollowAnAttachParent => false;

        public override string DisplayTypeName => LaundryMachineBody.NameOf(_kind);

        [Undoable]
        public LaundryMachineKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                ApplyDimensions();
            }
        }

        public float HatchDiameterMM => LaundryMachineBody.HatchDiameterMM(_kind, DimensionsMM);

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

        public override Vector2Int DecorSurfaceMM =>
            new Vector2Int(DimensionsMM.x, DimensionsMM.y);

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

            RebuildShellMesh();

            var body = LaundryMachineBody.BodyPartsMM(_kind, DimensionsMM);
            Boxes.PlaceUnscaled(LaundryMachineBody.IdxShell, body[LaundryMachineBody.IdxShell].centerMM,
                Quaternion.identity);
            for (int i = 1; i < LaundryMachineBody.BodyPartCount; i++)
                Boxes.Place(i, body[i].centerMM, body[i].sizeMM, Quaternion.identity);

            ApplyDoorPose();
            ApplyMaterials();
        }

        private void RebuildShellMesh()
        {
            DisposeShellMesh();
            _shellMesh = DrumRecessMesh.Build(
                LaundryMachineBody.ShellSizeMM(DimensionsMM),
                LaundryMachineBody.BoreCenterInShellMM(DimensionsMM),
                LaundryMachineBody.DrumDiameterMM(_kind, DimensionsMM),
                LaundryMachineBody.DrumDepthMM(DimensionsMM));
            Boxes.SetMesh(LaundryMachineBody.IdxShell, _shellMesh);
        }

        private void DisposeShellMesh()
        {
            if (_shellMesh == null) return;
            if (Application.isPlaying) Destroy(_shellMesh);
            else DestroyImmediate(_shellMesh);
            _shellMesh = null;
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
            Boxes.RendererOf(LaundryMachineBody.IdxShell);

        private void ApplyMaterials()
        {
            var front = Skin();
            for (int i = 0; i < LaundryMachineBody.PartCount; i++)
                Boxes.SetMaterial(i, i switch
                {
                    LaundryMachineBody.IdxControlPanel => ApplianceMaterials.LaundryPanel,
                    LaundryMachineBody.IdxDrumBack => ApplianceMaterials.LaundryDrum,
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
            Boxes.SetMaterial(LaundryMachineBody.IdxHatchRim, front);
        }

        public void DestroyChildren() => Boxes.Destroy();

        protected override void OnElementDestroyed()
        {
            DestroyChildren();
            DisposeShellMesh();
        }
    }
}
