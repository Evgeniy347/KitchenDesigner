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

        public const string MODEL = "Bosch SMV25EX02E";

        public const int BODY_WIDTH_MM = 598;
        public const int BODY_DEPTH_MM = 550;

        public const int HEIGHT_MIN_MM = 815;
        public const int HEIGHT_MAX_MM = 875;

        public const int BODY_HEIGHT_MM = HEIGHT_MIN_MM;

        public const int NICHE_WIDTH_MM = 600;
        public const int NICHE_MIN_DEPTH_MM = 550;

        public const int FACADE_WIDTH_MM = NICHE_WIDTH_MM;
        public const int FACADE_MIN_HEIGHT_MM = 655;
        public const int FACADE_MAX_HEIGHT_MM = 725;
        public const int FACADE_NOMINAL_HEIGHT_MM = 720;

        public const int PLINTH_MIN_MM = 90;
        public const int PLINTH_MAX_MM = 220;
        public const int PLINTH_NICHE_MM = 89;
        public const int PLINTH_SETBACK_MM = 53;

        public const int FEET_PROTRUSION_MM = 100;
        public const int FEET_ADJUST_MM = HEIGHT_MAX_MM - HEIGHT_MIN_MM;

        public const int BASE_HEIGHT_MM = BODY_HEIGHT_MM - FACADE_MAX_HEIGHT_MM;
        public const int BASE_SETBACK_MM = FEET_PROTRUSION_MM;
        public const int BASE_DEPTH_MM = BODY_DEPTH_MM - BASE_SETBACK_MM;

        public const int TANK_HEIGHT_MM = BODY_HEIGHT_MM - BASE_HEIGHT_MM;

        public const float FACADE_MOUNT_GAP_MM = 5f;

        public const int CONTROL_PANEL_HEIGHT_MM = 14;
        public const float OVERLAY_THICKNESS_MM = 2f;

        public const int BODY_WALL_MM = 20;
        public const int DOOR_THICKNESS_MM = 20;

        public const float DOOR_OPEN_ANGLE_DEG = DropDoor.OPEN_ANGLE_DEG;

        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(BODY_WIDTH_MM, BODY_HEIGHT_MM, BODY_DEPTH_MM);

        public static int PlinthForFacade(int facadeHeightMM) => BODY_HEIGHT_MM - facadeHeightMM;

        public static bool IsFacadeHeightValid(int facadeHeightMM) =>
            facadeHeightMM >= FACADE_MIN_HEIGHT_MM && facadeHeightMM <= FACADE_MAX_HEIGHT_MM;

        private const int IdxBodyBottom = 0;
        private const int IdxBodyTop = 1;
        private const int IdxBodyLeft = 2;
        private const int IdxBodyRight = 3;
        private const int IdxBodyBack = 4;
        private const int IdxBase = 5;
        private const int IdxDoor = 6;
        private const int IdxPanel = 7;
        private const int BodyPartCount = 6;
        private const int DoorPartCount = 2;
        private const int ChildCount = BodyPartCount + DoorPartCount;

        [SerializeField] private string _attachedFacadeName = "";
        [SerializeField] private bool _open;

        private ApplianceBoxes? _boxes;
        private DropDoor? _door;

        private ApplianceBoxes Boxes => _boxes ??= new ApplianceBoxes(transform, ChildName);

        private DropDoor Door =>
            _door ??= new DropDoor(Boxes, BodyPartCount, DoorPartsMM, () => HingeLocalMM);

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

        public const float TANK_CENTER_Y_MM = BASE_HEIGHT_MM * 0.5f;

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

        private static string ChildName(int idx) => idx switch
        {
            IdxBodyBottom => "BodyBottom",
            IdxBodyTop => "BodyTop",
            IdxBodyLeft => "BodyLeft",
            IdxBodyRight => "BodyRight",
            IdxBodyBack => "BodyBack",
            IdxBase => "Base",
            IdxDoor => "Door",
            _ => "ControlPanel",
        };

        private const float TankBottomMM = -BODY_HEIGHT_MM * 0.5f + BASE_HEIGHT_MM;

        private static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM()
        {
            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;
            float t = BODY_WALL_MM;

            float cy = TANK_CENTER_Y_MM;
            float bottom = TankBottomMM;
            float top = halfH;
            float back = -halfD;
            float depth = BODY_DEPTH_MM - DOOR_THICKNESS_MM;
            float cz = back + depth * 0.5f;

            float sideX = (BODY_WIDTH_MM - t) * 0.5f;
            float innerH = TANK_HEIGHT_MM - 2 * t;
            float innerW = BODY_WIDTH_MM - 2 * t;

            return new[]
            {
                (new Vector3(0f, bottom + t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, depth)),
                (new Vector3(0f, top - t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, depth)),
                (new Vector3(-sideX, cy, cz), new Vector3(t, innerH, depth)),
                (new Vector3(sideX, cy, cz), new Vector3(t, innerH, depth)),
                (new Vector3(0f, cy, back + t * 0.5f), new Vector3(innerW, innerH, t)),
                (new Vector3(0f, -halfH + BASE_HEIGHT_MM * 0.5f, back + BASE_DEPTH_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, BASE_HEIGHT_MM, BASE_DEPTH_MM)),
            };
        }

        private static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM()
        {
            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;

            return new[]
            {
                (new Vector3(0f, TANK_CENTER_Y_MM, halfD - DOOR_THICKNESS_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, TANK_HEIGHT_MM, DOOR_THICKNESS_MM)),
                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f,
                     halfD - OVERLAY_THICKNESS_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM)),
            };
        }

        public static Vector3 HingeLocalMM =>
            new Vector3(0f, TankBottomMM, BODY_DEPTH_MM * 0.5f - DOOR_THICKNESS_MM);

        public static Quaternion DoorLocalRotation(float progress) => DropDoor.LocalRotation(progress);

        private void RebuildGeometry()
        {
            Boxes.Ensure(ChildCount);

            var body = BodyPartsMM();
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
                FrameRateManager.KeepAwake(DropDoor.OPEN_SECONDS + DropDoor.KEEP_AWAKE_MARGIN_SECONDS);
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

        internal void Update() => StepDoor(Time.deltaTime);

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
