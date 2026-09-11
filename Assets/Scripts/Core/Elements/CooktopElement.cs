using UnityEngine;

namespace KitchenDesigner.Core
{
    public class CooktopElement : PartCutoutElement, IFixedSizeElement, IPaintsItself
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("лицо варочной — её верх: конфорки и обод видны сверху с любой стороны");

        public override string DisplayTypeName => HasFixedSize ? Model : "Варочная";

        public override bool ParticipatesInGapChecks => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public const int RIM_HEIGHT_MM = 5;

        public const int DEFAULT_WIDTH_MM = 590;
        public const int DEFAULT_DEPTH_MM = 520;
        public const int DEFAULT_HEIGHT_MM = 65;
        public const int DEFAULT_CUTOUT_WIDTH_MM = 560;
        public const int DEFAULT_CUTOUT_DEPTH_MM = 490;

        public const int MIN_RIM_OVERLAP_MM = 5;
        public const int MIN_SIDE_MM = 100;
        public const int MAX_SIDE_MM = 2000;
        public const int MIN_CUTOUT_MM = 50;
        public const int MIN_BODY_HEIGHT_MM = 10;
        public const int MAX_HEIGHT_MM = 600;

        public const int MIN_EDGE_MM = 20;

        public const int SNAP_PLANE_MM = 20;

        public const float MIN_TRACKED_TWIST_DEG = 0.01f;

        public const string MODEL_BOSCH_PUE611BB5E = "Bosch PUE611BB5E";
        public const int BOSCH_WIDTH_MM = 592;
        public const int BOSCH_DEPTH_MM = 522;
        public const int BOSCH_HEIGHT_MM = 51;
        public const int BOSCH_CUTOUT_WIDTH_MM = 560;
        public const int BOSCH_CUTOUT_DEPTH_MM = 490;

        public static Vector3Int ModelDimensionsMM(string? model) => model switch
        {
            MODEL_BOSCH_PUE611BB5E => new Vector3Int(BOSCH_WIDTH_MM, BOSCH_HEIGHT_MM, BOSCH_DEPTH_MM),
            _ => Vector3Int.zero,
        };

        public static Vector2Int ModelCutoutMM(string? model) => model switch
        {
            MODEL_BOSCH_PUE611BB5E => new Vector2Int(BOSCH_CUTOUT_WIDTH_MM, BOSCH_CUTOUT_DEPTH_MM),
            _ => Vector2Int.zero,
        };

        public static bool IsKnownModel(string? model) => ModelDimensionsMM(model).x > 0;

        [SerializeField] private string _model = "";
        [SerializeField] private int _cutoutWidthMM = DEFAULT_CUTOUT_WIDTH_MM;
        [SerializeField] private int _cutoutDepthMM = DEFAULT_CUTOUT_DEPTH_MM;
        [SerializeField] private float _yawDeg;

        private CooktopMesh? _mesh;

        private int _memoCutoutWidthMM = int.MinValue;
        private int _memoCutoutDepthMM = int.MinValue;
        private float _memoYawDeg = float.MinValue;

        private Quaternion _appliedRotation = Quaternion.identity;
        private bool _hasAppliedRotation;

        private CooktopMesh Mesh => _mesh ??= new CooktopMesh(transform);

        protected override (int widthMM, int depthMM) CutoutExtentsMM =>
            (Mathf.RoundToInt(_yawDeg / 90f) & 1) == 0
                ? (CutoutWidthMM, CutoutDepthMM)
                : (CutoutDepthMM, CutoutWidthMM);

        protected override int RimHeightMM => RIM_HEIGHT_MM;

        protected override int MinEdgeMM => MIN_EDGE_MM;

        protected override string HostRejectionMessage => "деталь не годится под варочную";

        [NotUndoable("модель прибора задаётся при создании; отменять нечего — габариты от неё производные")]
        public string Model
        {
            get => _model;
            set
            {
                _model = value ?? "";
                ApplyDimensions();
            }
        }

        public bool HasFixedSize => IsKnownModel(_model);

        [NotUndoable("производная transform.rotation: копится в TrackRotation, откатывается вместе с позой (MoveCommand/ResizeCommand)")]
        public float YawDeg
        {
            get => _yawDeg;
            set => _yawDeg = Mathf.Repeat(value, 360f);
        }

        [NotUndoable("проекция DimensionsMM.x — откатывается вместе с габаритом")]
        public int WidthMM
        {
            get => DimensionsMM.x;
            set => DimensionsMM = new Vector3Int(value, DimensionsMM.y, DimensionsMM.z);
        }

        [NotUndoable("проекция DimensionsMM.z — откатывается вместе с габаритом")]
        public int DepthMM
        {
            get => DimensionsMM.z;
            set => DimensionsMM = new Vector3Int(DimensionsMM.x, DimensionsMM.y, value);
        }

        [NotUndoable("проекция DimensionsMM.y — откатывается вместе с габаритом")]
        public int HeightMM
        {
            get => DimensionsMM.y;
            set => DimensionsMM = new Vector3Int(DimensionsMM.x, value, DimensionsMM.z);
        }

        public int BodyHeightMM => Mathf.Max(MIN_BODY_HEIGHT_MM, DimensionsMM.y - RIM_HEIGHT_MM);

        [NotUndoable("своя команда SetCooktopCutoutCommand: геттер клампится по плите, снимок был бы лоссовым")]
        public int CutoutWidthMM
        {
            get => HasFixedSize
                ? ModelCutoutMM(_model).x
                : ClampCutout(_cutoutWidthMM, DimensionsMM.x);
            set
            {
                if (HasFixedSize) return;
                _cutoutWidthMM = Mathf.Clamp(value, MIN_CUTOUT_MM, MAX_SIDE_MM);
                ApplyDimensions();
            }
        }

        [NotUndoable("см. CutoutWidthMM — SetCooktopCutoutCommand")]
        public int CutoutDepthMM
        {
            get => HasFixedSize
                ? ModelCutoutMM(_model).y
                : ClampCutout(_cutoutDepthMM, DimensionsMM.z);
            set
            {
                if (HasFixedSize) return;
                _cutoutDepthMM = Mathf.Clamp(value, MIN_CUTOUT_MM, MAX_SIDE_MM);
                ApplyDimensions();
            }
        }

        public static int ClampSide(int mm) => Mathf.Clamp(mm, MIN_SIDE_MM, MAX_SIDE_MM);

        public static int ClampHeight(int mm) =>
            Mathf.Clamp(mm, RIM_HEIGHT_MM + MIN_BODY_HEIGHT_MM, MAX_HEIGHT_MM);

        public static int ClampCutout(int mm, int outerMM) =>
            Mathf.Clamp(mm, MIN_CUTOUT_MM, Mathf.Max(MIN_CUTOUT_MM, outerMM - 2 * MIN_RIM_OVERLAP_MM));

        public int MinPartWidthMM => CutoutExtentsMM.widthMM + 2 * MIN_EDGE_MM;
        public int MinPartDepthMM => CutoutExtentsMM.depthMM + 2 * MIN_EDGE_MM;

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            RIM_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, AppConstants.HalfHeightUnits(RIM_HEIGHT_MM), 0f);

        private void Start()
        {
            SnapToPart();
        }

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;

            var fixedDims = ModelDimensionsMM(_model);
            if (fixedDims.x > 0) Data.DimensionsMM = fixedDims;

            var dims = Data.DimensionsMM;
            int w = ClampSide(dims.x <= 0 ? DEFAULT_WIDTH_MM : dims.x);
            int d = ClampSide(dims.z <= 0 ? DEFAULT_DEPTH_MM : dims.z);
            int h = ClampHeight(dims.y <= 0 ? DEFAULT_HEIGHT_MM : dims.y);
            Data.DimensionsMM = new Vector3Int(w, h, d);

            UpdateCollider();
            if (SuppressVisualRebuild) return;
            RebuildGeometry();

            if (Mount.Part != null &&
                (_memoCutoutWidthMM != CutoutWidthMM || _memoCutoutDepthMM != CutoutDepthMM))
            {
                _memoCutoutWidthMM = CutoutWidthMM;
                _memoCutoutDepthMM = CutoutDepthMM;
                Mount.Part.RebuildGrooveMesh();
            }
        }

        public override void SnapToPart() => SnapToPartCore();

        protected override void OnAttached(KitchenElement part) => _yawDeg = YawRelativeTo(part);

        protected override void ForgetAlignmentMemo()
        {
            base.ForgetAlignmentMemo();
            _memoYawDeg = float.MinValue;
        }

        protected override void TrackYaw(KitchenElement? part)
        {
            if (!_hasAppliedRotation)
            {
                _appliedRotation = transform.rotation;
                _hasAppliedRotation = true;
                return;
            }
            Quaternion delta = transform.rotation * Quaternion.Inverse(_appliedRotation);
            _appliedRotation = transform.rotation;
            if (part == null || Quaternion.Angle(delta, Quaternion.identity) < MIN_TRACKED_TWIST_DEG) return;

            _yawDeg = Mathf.Repeat(_yawDeg + PartPlane.Of(part).TwistAroundUpDeg(delta), 360f);
        }

        private float YawRelativeTo(KitchenElement part)
        {
            var plane = PartPlane.Of(part);
            Quaternion delta = transform.rotation * Quaternion.Inverse(plane.RestingRotation);
            return Mathf.Repeat(plane.TwistAroundUpDeg(delta), 360f);
        }

        public bool IsSuitableHost(KitchenElement part) => IsHostSuitable(part, MinPartWidthMM, MinPartDepthMM);

        protected override bool AcceptsHost(KitchenElement part) => IsSuitableHost(part);

        protected override void OnCatchFound(KitchenElement part) => _yawDeg = YawRelativeTo(part);

        private CutoutBody BodyOn(PartPlane plane, int offXMM, int offYMM)
        {
            var (cutW, cutD) = CutoutExtentsMM;
            return new CutoutBody(plane, offXMM, offYMM, cutW, cutD, BodyHeightMM);
        }

        public bool BodyBlocked(KitchenElement part, int offX, int offY) =>
            FirstBlocker(part, offX, offY) != null;

        protected override string? FirstBlocker(KitchenElement part, int offX, int offY) =>
            BodyOn(PartPlane.Of(part), offX, offY).FirstBlocker(this);

        protected override void AlignToPart(KitchenElement part)
        {
            var plane = PartPlane.Of(part);
            Quaternion targetRot =
                Quaternion.AngleAxis(_yawDeg, plane.UpWorld) * plane.RestingRotation;

            var (snappedX, snappedY) =
                BodyOn(plane, Mount.OffsetXMM, Mount.OffsetYMM).SnappedToNeighbours(this, SNAP_PLANE_MM);
            var (offX, offY) = ClampOffsets(plane, snappedX, snappedY);
            Mount.OffsetXMM = offX;
            Mount.OffsetYMM = offY;

            Vector3 targetPos = plane.SurfacePoint(offX, offY);
            if ((targetPos - transform.position).sqrMagnitude > Tolerance.EpsilonSqr ||
                Quaternion.Angle(targetRot, transform.rotation) > ALIGNED_ROTATION_EPSILON_DEG)
                transform.SetPositionAndRotation(targetPos, targetRot);

            bool memoChanged = MemoPart != part
                || MemoOffsetXMM != offX || MemoOffsetYMM != offY
                || _memoCutoutWidthMM != CutoutWidthMM || _memoCutoutDepthMM != CutoutDepthMM
                || !Mathf.Approximately(_memoYawDeg, _yawDeg);

            Mount.MarkAligned(part);
            _appliedRotation = transform.rotation;
            _hasAppliedRotation = true;

            if (!memoChanged) return;

            MemoPart = part;
            MemoOffsetXMM = offX;
            MemoOffsetYMM = offY;
            _memoCutoutWidthMM = CutoutWidthMM;
            _memoCutoutDepthMM = CutoutDepthMM;
            _memoYawDeg = _yawDeg;
            part.RebuildGrooveMesh();
        }

        public Vector3 BodyCenter =>
            transform.position - transform.rotation *
                new Vector3(0f, AppConstants.HalfHeightUnits(BodyHeightMM), 0f);

        public Vector3 BodySize => new Vector3(
            CutoutWidthMM * AppConstants.MM_TO_UNITS,
            BodyHeightMM * AppConstants.MM_TO_UNITS,
            CutoutDepthMM * AppConstants.MM_TO_UNITS);

        private void RebuildGeometry()
        {
            Mesh.Rebuild(Data.DimensionsMM, BodyHeightMM, CutoutWidthMM, CutoutDepthMM, HasFixedSize);
            ApplyMaterials();
            MaterialManager.RefreshTiling(this);
        }

        public void ApplyMaterials() => Mesh.ApplySurfaceMaterial(Skin());

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return ApplianceMaterials.CooktopGlass;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : ApplianceMaterials.CooktopGlass;
        }

        public void SetMaterial(Material material) => Mesh.ApplySurfaceMaterial(
            SanitaryDecor.ChosenOrFactory(MaterialId, material,
                ApplianceMaterials.CooktopGlass));

        protected override void DestroyChildren() => Mesh.Destroy();
    }
}
