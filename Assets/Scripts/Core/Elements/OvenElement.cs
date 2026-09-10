using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class OvenElement : KitchenElement, IFixedSizeElement, IOpenable, IPaintsItself,
        IQuantifies
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

        public const string MODEL = "Bosch HBA514BB3";

        public const int FACADE_WIDTH_MM = 594;
        public const int FACADE_HEIGHT_MM = 595;
        public const float FACADE_THICKNESS_MM = 19.5f;

        public const int BODY_WIDTH_MM = 560;
        public const int BODY_DEPTH_MM = 548;
        public const int BODY_HEIGHT_MM = 570;

        public const int FACADE_TOP_OVERHANG_MM = 25;

        public const int FACADE_BOTTOM_OVERHANG_MM =
            FACADE_HEIGHT_MM - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM;

        public const int BODY_WALL_MM = 20;

        public const int CONTROL_PANEL_HEIGHT_MM = 96;
        public const int GLASS_HEIGHT_MM = FACADE_HEIGHT_MM - CONTROL_PANEL_HEIGHT_MM;
        public const int DOOR_FRAME_MM = 15;
        public const float OVERLAY_THICKNESS_MM = 2f;

        public const int HANDLE_TOP_MM = CONTROL_PANEL_HEIGHT_MM;
        public const int HANDLE_HEIGHT_MM = 28;
        public const int HANDLE_SIDE_INSET_MM = 12;
        public const int HANDLE_PROTRUSION_MM = 50;

        public const float TOTAL_DEPTH_MM = BODY_DEPTH_MM + FACADE_THICKNESS_MM;
        public const int DEPTH_MM = 568;

        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, DEPTH_MM);

        public const float DOOR_OPEN_ANGLE_DEG = DropDoor.OPEN_ANGLE_DEG;

        private const int IdxBodyBottom = 0;
        private const int IdxBodyTop = 1;
        private const int IdxBodyLeft = 2;
        private const int IdxBodyRight = 3;
        private const int IdxBodyBack = 4;
        private const int IdxFacade = 5;
        private const int IdxGlass = 6;
        private const int IdxPanel = 7;
        private const int IdxHandle = 8;
        private const int BodyPartCount = 5;
        private const int DoorPartCount = 4;
        private const int ChildCount = BodyPartCount + DoorPartCount;

        [SerializeField] private bool _open;

        private ApplianceBoxes? _boxes;
        private DropDoor? _door;

        private ApplianceBoxes Boxes => _boxes ??= new ApplianceBoxes(transform, ChildName);

        private DropDoor Door =>
            _door ??= new DropDoor(Boxes, BodyPartCount, DoorPartsMM, () => HingeLocalMM);

        public bool HasFixedSize => true;

        public bool IsOpen => _open;

        public float DoorProgress => Door.Progress;

        public bool IsAnimating => Door.IsAnimatingTowards(_open);

        protected override Vector3 EffectiveScale => new Vector3(
            BODY_WIDTH_MM * AppConstants.MM_TO_UNITS,
            BODY_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            BODY_DEPTH_MM * AppConstants.MM_TO_UNITS);

        public const float BODY_CENTER_Y_MM =
            FACADE_HEIGHT_MM * 0.5f - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM * 0.5f;

        public const float BODY_CENTER_Z_MM =
            TOTAL_DEPTH_MM * 0.5f - FACADE_THICKNESS_MM - BODY_DEPTH_MM * 0.5f;

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, BODY_CENTER_Y_MM, BODY_CENTER_Z_MM) * AppConstants.MM_TO_UNITS;

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
            IdxFacade => "Facade",
            IdxGlass => "Glass",
            IdxPanel => "ControlPanel",
            _ => "Handle",
        };

        private static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM()
        {
            float halfH = FACADE_HEIGHT_MM * 0.5f;
            float cy = BODY_CENTER_Y_MM;
            float cz = BODY_CENTER_Z_MM;
            float t = BODY_WALL_MM;

            float top = halfH - FACADE_TOP_OVERHANG_MM;
            float bottom = top - BODY_HEIGHT_MM;
            float back = cz - BODY_DEPTH_MM * 0.5f;
            float sideX = (BODY_WIDTH_MM - t) * 0.5f;
            float innerH = BODY_HEIGHT_MM - 2 * t;
            float innerW = BODY_WIDTH_MM - 2 * t;

            return new[]
            {
                (new Vector3(0f, bottom + t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, BODY_DEPTH_MM)),
                (new Vector3(0f, top - t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, BODY_DEPTH_MM)),
                (new Vector3(-sideX, cy, cz), new Vector3(t, innerH, BODY_DEPTH_MM)),
                (new Vector3(sideX, cy, cz), new Vector3(t, innerH, BODY_DEPTH_MM)),
                (new Vector3(0f, cy, back + t * 0.5f), new Vector3(innerW, innerH, t)),
            };
        }

        private static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM()
        {
            float halfH = FACADE_HEIGHT_MM * 0.5f;
            float halfD = TOTAL_DEPTH_MM * 0.5f;

            float overlayZ = halfD - OVERLAY_THICKNESS_MM * 0.5f;
            float doorTopY = halfH - CONTROL_PANEL_HEIGHT_MM;

            return new[]
            {
                (new Vector3(0f, 0f, halfD - FACADE_THICKNESS_MM * 0.5f),
                 new Vector3(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, FACADE_THICKNESS_MM)),
                (new Vector3(0f, (doorTopY - halfH) * 0.5f, overlayZ),
                 new Vector3(FACADE_WIDTH_MM - 2 * DOOR_FRAME_MM,
                     GLASS_HEIGHT_MM - 2 * DOOR_FRAME_MM, OVERLAY_THICKNESS_MM)),
                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f, overlayZ),
                 new Vector3(FACADE_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM)),
                (new Vector3(0f, halfH - HANDLE_TOP_MM - HANDLE_HEIGHT_MM * 0.5f,
                     halfD + HANDLE_PROTRUSION_MM * 0.5f),
                 new Vector3(FACADE_WIDTH_MM - 2 * HANDLE_SIDE_INSET_MM,
                     HANDLE_HEIGHT_MM, HANDLE_PROTRUSION_MM)),
            };
        }

        public static Vector3 HingeLocalMM => new Vector3(
            0f, -FACADE_HEIGHT_MM * 0.5f, TOTAL_DEPTH_MM * 0.5f - FACADE_THICKNESS_MM);

        public static Quaternion DoorLocalRotation(float progress) => DropDoor.LocalRotation(progress);

        private void RebuildGeometry()
        {
            Boxes.Ensure(ChildCount);

            var body = BodyPartsMM();
            for (int i = 0; i < BodyPartCount; i++)
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

        public override MeshRenderer? DecorRenderer => Boxes.RendererOf(IdxFacade);

        private void ApplyMaterials()
        {
            var facade = Skin();
            for (int i = 0; i < ChildCount; i++)
                Boxes.SetMaterial(i, i switch
                {
                    IdxFacade => facade,
                    IdxGlass => ApplianceMaterials.OvenGlass,
                    IdxPanel => ApplianceMaterials.OvenPanel,
                    IdxHandle => ApplianceMaterials.OvenHandle,
                    _ => ApplianceMaterials.OvenBody,
                });
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return ApplianceMaterials.OvenFacade;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : ApplianceMaterials.OvenFacade;
        }

        public void SetMaterial(Material material) => Boxes.SetMaterial(IdxFacade,
            SanitaryDecor.ChosenOrFactory(MaterialId, material,
                ApplianceMaterials.OvenFacade));

        public void DestroyChildren() => Boxes.Destroy();

        protected override void OnElementDestroyed()
        {
            DestroyChildren();
        }
    }
}
