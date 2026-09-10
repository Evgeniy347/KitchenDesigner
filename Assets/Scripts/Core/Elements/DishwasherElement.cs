using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class DishwasherElement : KitchenElement, IFixedSizeElement, IFacadeHost, IOpenable,
        IPaintsItself, IQuantifies
    {
        public override bool CanFollowAnAttachParent => false;

        public override string DisplayTypeName => MODEL;

        public bool IsClosedPose => !IsOpen && !IsAnimating;

        public string OpenActionLabel => IsOpen ? OpenLabels.CloseDoor : OpenLabels.OpenDoor;

        public void CycleOpenState() => ToggleOpen();

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        public const string MODEL = DishwasherBody.MODEL;

        public const int BODY_WIDTH_MM = DishwasherBody.BODY_WIDTH_MM;
        public const int BODY_DEPTH_MM = DishwasherBody.BODY_DEPTH_MM;

        public const int HEIGHT_MIN_MM = DishwasherBody.HEIGHT_MIN_MM;
        public const int HEIGHT_MAX_MM = DishwasherBody.HEIGHT_MAX_MM;

        public const int BODY_HEIGHT_MM = DishwasherBody.BODY_HEIGHT_MM;

        public const int NICHE_WIDTH_MM = DishwasherBody.NICHE_WIDTH_MM;
        public const int NICHE_MIN_DEPTH_MM = DishwasherBody.NICHE_MIN_DEPTH_MM;

        public const int FACADE_WIDTH_MM = DishwasherBody.FACADE_WIDTH_MM;
        public const int FACADE_MIN_HEIGHT_MM = DishwasherBody.FACADE_MIN_HEIGHT_MM;
        public const int FACADE_MAX_HEIGHT_MM = DishwasherBody.FACADE_MAX_HEIGHT_MM;
        public const int FACADE_NOMINAL_HEIGHT_MM = DishwasherBody.FACADE_NOMINAL_HEIGHT_MM;

        public const int PLINTH_MIN_MM = DishwasherBody.PLINTH_MIN_MM;
        public const int PLINTH_MAX_MM = DishwasherBody.PLINTH_MAX_MM;
        public const int PLINTH_NICHE_MM = DishwasherBody.PLINTH_NICHE_MM;
        public const int PLINTH_SETBACK_MM = DishwasherBody.PLINTH_SETBACK_MM;

        public const int FEET_PROTRUSION_MM = DishwasherBody.FEET_PROTRUSION_MM;
        public const int FEET_ADJUST_MM = DishwasherBody.FEET_ADJUST_MM;

        public const int BASE_HEIGHT_MM = DishwasherBody.BASE_HEIGHT_MM;
        public const int BASE_SETBACK_MM = DishwasherBody.BASE_SETBACK_MM;
        public const int BASE_DEPTH_MM = DishwasherBody.BASE_DEPTH_MM;

        public const int TANK_HEIGHT_MM = DishwasherBody.TANK_HEIGHT_MM;

        public const float FACADE_MOUNT_GAP_MM = DishwasherBody.FACADE_MOUNT_GAP_MM;

        public static Vector3 HingeLocalMM => DishwasherBody.HingeLocalMM;

        public const int CONTROL_PANEL_HEIGHT_MM = DishwasherBody.CONTROL_PANEL_HEIGHT_MM;
        public const float OVERLAY_THICKNESS_MM = DishwasherBody.OVERLAY_THICKNESS_MM;

        public const int BODY_WALL_MM = DishwasherBody.BODY_WALL_MM;
        public const int DOOR_THICKNESS_MM = DishwasherBody.DOOR_THICKNESS_MM;
        public const float DOOR_SLAB_THICKNESS_MM = DishwasherBody.DOOR_SLAB_THICKNESS_MM;

        public const float DOOR_OPEN_ANGLE_DEG = DropDoor.OPEN_ANGLE_DEG;

        public static Vector3Int ModelDimensionsMM => DishwasherBody.ModelDimensionsMM;

        public static int PlinthForFacade(int facadeHeightMM) =>
            DishwasherBody.PlinthForFacade(facadeHeightMM);

        public static bool IsFacadeHeightValid(int facadeHeightMM) =>
            DishwasherBody.IsFacadeHeightValid(facadeHeightMM);

        private const int IdxBase = DishwasherBody.IdxBase;
        private const int IdxDoor = DishwasherBody.IdxDoor;
        private const int IdxPanel = DishwasherBody.IdxPanel;
        private const int BodyPartCount = DishwasherBody.BodyPartCount;
        private const int ChildCount = DishwasherBody.ChildCount;

        [SerializeField] private string _attachedFacadeName = "";
        [SerializeField] private bool _open;

        private ApplianceBoxes? _boxes;
        private DropDoor? _door;

        private ApplianceBoxes Boxes => _boxes ??= new ApplianceBoxes(transform, DishwasherBody.ChildName);

        private DropDoor Door =>
            _door ??= new DropDoor(Boxes, BodyPartCount, DishwasherBody.DoorPartsMM,
                () => DishwasherBody.HingeLocalMM);

        public bool HasFixedSize => true;

        [NotUndoable("обратная ссылка на фасад, ведёт DrawerLinks")]
        public string AttachedFacadeName
        {
            get => _attachedFacadeName;
            set => _attachedFacadeName = value ?? "";
        }

        public FacadeElement? FindAttachedFacade()
        {
            if (string.IsNullOrEmpty(_attachedFacadeName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is FacadeElement f && f.PartName == _attachedFacadeName) return f;
            return null;
        }

        public float FacadeMountGapMm => FACADE_MOUNT_GAP_MM;

        public void OnAttachedFacadeChanged(FacadeElement? oldFacade, FacadeElement? newFacade)
        {
            if (oldFacade != null && oldFacade.IsPassenger) oldFacade.IsPassenger = false;
            if (newFacade != null)
            {
                newFacade.CaptureClosedPose();
                newFacade.IsPassenger = true;
            }
        }

        public bool IsOpen => _open;

        public float DoorProgress => Door.Progress;

        public bool IsAnimating => Door.IsAnimatingTowards(_open);

        protected override Vector3 EffectiveScale => new Vector3(
            BODY_WIDTH_MM * AppConstants.MM_TO_UNITS,
            TANK_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            BODY_DEPTH_MM * AppConstants.MM_TO_UNITS);

        public const float TANK_CENTER_Y_MM = DishwasherBody.TANK_CENTER_Y_MM;

        public Vector3 SoleCenterWorld => transform.position + transform.rotation *
            new Vector3(0f, -BODY_HEIGHT_MM * 0.5f, 0f) * AppConstants.MM_TO_UNITS;

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, TANK_CENTER_Y_MM, 0f) * AppConstants.MM_TO_UNITS;

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
            Boxes.Ensure(ChildCount);

            var body = DishwasherBody.BodyPartsMM();
            for (int i = 0; i < BodyPartCount; i++)
                Boxes.Place(i, body[i].centerMM, body[i].sizeMM, Quaternion.identity);

            ApplyDoorPose();
            ApplyMaterials();
        }

        internal void ApplyDoorPose()
        {
            if (Boxes.Count < ChildCount) return;
            var doorRotation = Door.ApplyPose();
            ApplyFacadePose(doorRotation);
        }

        private void ApplyFacadePose(Quaternion doorRotation)
        {
            var facade = FindAttachedFacade();
            if (facade == null) return;

            var (worldPos, worldRot) = DropDoor.RiderPose(transform, doorRotation,
                Door.HingeLocalUnits, facade.ClosedPosition, facade.ClosedRotation);
            facade.transform.SetPositionAndRotation(worldPos, worldRot);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            SyncAttachedFacade();
            if (Door.IsAnimatingTowards(open))
            {
                enabled = true;
                FrameRateManager.KeepAwake(DropDoor.OPEN_SECONDS + DropDoor.KEEP_AWAKE_MARGIN_SECONDS);
            }
        }

        public void ToggleOpen() => SetOpen(!_open);

        public void ForceClose()
        {
            var facade = FindAttachedFacade();
            if (facade != null) facade.ForceClose();

            bool wasOpen = _open;
            _open = false;
            if (!Door.ForceClose() && !wasOpen) return;
            ApplyDoorPose();
        }

        private void SyncAttachedFacade()
        {
            var facade = FindAttachedFacade();
            if (facade != null) facade.SetOpen(_open);
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

        private float MaxSafeDoorProgress()
        {
            var exclude = new List<KitchenElement>();
            var facade = FindAttachedFacade();
            if (facade != null) exclude.Add(facade);
            return OpeningCollision.FindMaxProgress(this, GetOpenBoxes, exclude);
        }

        public void GetOpenBoxes(float progress, List<OrientedBox> into)
        {
            Door.WorldBoxes(transform, progress, into);

            var facade = FindAttachedFacade();
            if (facade == null) return;

            into.Add(DropDoor.RiderBox(transform, DropDoor.LocalRotation(progress),
                Door.HingeLocalUnits, facade.ClosedPosition, facade.ClosedRotation,
                facade.transform.localScale * 0.5f));
        }

        public override MeshRenderer? DecorRenderer => Boxes.RendererOf(IdxDoor);

        private void ApplyMaterials()
        {
            var front = Skin();
            for (int i = 0; i < ChildCount; i++)
                Boxes.SetMaterial(i, i switch
                {
                    IdxPanel => ApplianceMaterials.DishwasherPanel,
                    IdxDoor or IdxBase => front,
                    _ => ApplianceMaterials.DishwasherTank,
                });
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId))
                return ApplianceMaterials.DishwasherDoor;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : ApplianceMaterials.DishwasherDoor;
        }

        public void SetMaterial(Material material)
        {
            var front = SanitaryDecor.ChosenOrFactory(MaterialId, material,
                ApplianceMaterials.DishwasherDoor);
            Boxes.SetMaterial(IdxDoor, front);
            Boxes.SetMaterial(IdxBase, front);
        }

        public void DestroyChildren() => Boxes.Destroy();

        protected override void OnElementDestroyed()
        {
            DestroyChildren();
        }
    }
}
