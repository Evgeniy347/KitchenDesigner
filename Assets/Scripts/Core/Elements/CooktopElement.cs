using UnityEngine;

namespace KitchenDesigner.Core
{
    public class CooktopElement : KitchenElement, IPartCutout, IFixedSizeElement
    {
        public override string DisplayTypeName => HasFixedSize ? Model : "Варочная";

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

        public const int SNAP_CATCH_MM = 100;
        public const int SNAP_RELEASE_MM = 60;
        public const int SNAP_PLANE_MM = 20;

        public const float ALIGNED_ROTATION_EPSILON_DEG = 0.05f;
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
        [SerializeField] private PartMount _mount = new PartMount();
        [SerializeField] private int _cutoutWidthMM = DEFAULT_CUTOUT_WIDTH_MM;
        [SerializeField] private int _cutoutDepthMM = DEFAULT_CUTOUT_DEPTH_MM;
        [SerializeField] private float _yawDeg;

        private bool _mountConfigured;
        private CooktopMesh? _mesh;

        private KitchenElement? _memoPart;
        private int _memoOffsetXMM = int.MinValue;
        private int _memoOffsetYMM = int.MinValue;
        private int _memoCutoutWidthMM = int.MinValue;
        private int _memoCutoutDepthMM = int.MinValue;
        private float _memoYawDeg = float.MinValue;

        private Quaternion _appliedRotation = Quaternion.identity;
        private bool _hasAppliedRotation;
        private int _lastPoseVersion;

        private PartMount Mount
        {
            get
            {
                if (_mount == null) _mount = new PartMount();
                if (!_mountConfigured)
                {
                    _mountConfigured = true;
                    _mount.Configure(this, transform, IsSuitableHost,
                        SNAP_CATCH_MM, SNAP_RELEASE_MM, ForgetAlignmentMemo);
                }
                return _mount;
            }
        }

        private CooktopMesh Mesh => _mesh ??= new CooktopMesh(transform);

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

        [NotUndoable("служебная привязка к детали, вычисляется SnapToPart")]
        public string AttachedPartName
        {
            get => Mount.AttachedPartName;
            set => Mount.AttachedPartName = value;
        }

        [NotUndoable("смещение от центра детали — производная позиции, откатывается MoveCommand")]
        public int OffsetXMM { get => Mount.OffsetXMM; set => Mount.OffsetXMM = value; }

        [NotUndoable("см. OffsetXMM")]
        public int OffsetYMM { get => Mount.OffsetYMM; set => Mount.OffsetYMM = value; }

        public bool IsAttached => Mount.IsAttached;

        [NotUndoable("производная transform.rotation: копится в TrackRotation, откатывается вместе с позой (MoveCommand/ResizeCommand)")]
        public float YawDeg
        {
            get => _yawDeg;
            set => _yawDeg = Mathf.Repeat(value, 360f);
        }

        public (int widthMM, int depthMM) CutoutExtentsMM =>
            (Mathf.RoundToInt(_yawDeg / 90f) & 1) == 0
                ? (CutoutWidthMM, CutoutDepthMM)
                : (CutoutDepthMM, CutoutWidthMM);

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
                new Vector3(0f, RIM_HEIGHT_MM * 0.5f * AppConstants.MM_TO_UNITS, 0f);

        private void Start()
        {
            SnapToPart();
        }

        internal void Update()
        {
            if (PoseVersion != _lastPoseVersion)
            {
                _lastPoseVersion = PoseVersion;
                SnapToPart();
            }

            if (Mount.PartMoved) SnapToPart();
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

        public void SnapToPart()
        {
            var part = Mount.CurrentOrNamedPart();
            Mount.TrackDrift(part);
            TrackRotation(part);

            part = Mount.ReleaseIfLost(part);
            if (part == null) part = FindCatchingPart();
            if (part == null) return;

            Mount.Adopt(part);
            AlignToPart(part);
        }

        public void AttachToPart(KitchenElement part)
        {
            if (part == null || !IsSuitableHost(part)) return;
            Mount.AttachTo(part);
            _yawDeg = YawRelativeTo(part);
            AlignToPart(part);
        }

        internal void UnregisterFromPart() => Mount.Detach();

        private void ForgetAlignmentMemo()
        {
            _memoPart = null;
            _memoOffsetXMM = int.MinValue;
            _memoOffsetYMM = int.MinValue;
            _memoYawDeg = float.MinValue;
        }

        private void TrackRotation(KitchenElement? part)
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

        public int HoleAxisIn(KitchenElement part) => PartPlane.Of(part).UpAxis;

        public bool IsSuitableHost(KitchenElement part)
        {
            if (part == null || !part.SupportsGrooves) return false;
            var plane = PartPlane.Of(part);
            if (!plane.IsHorizontal) return false;
            return plane.SizeAlongA >= MinPartWidthMM && plane.SizeAlongB >= MinPartDepthMM;
        }

        private KitchenElement? FindCatchingPart()
        {
            KitchenElement? best = null;
            float bestHeight = float.MaxValue;
            int bestX = 0, bestY = 0;
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || !IsSuitableHost(el)) continue;

                var plane = PartPlane.Of(el);
                var (offX, offY, heightMM) = plane.PoseOf(transform.position);
                if (!Mount.WithinCatchBand(heightMM)) continue;
                if (!plane.CoversOffset(offX, offY)) continue;

                float h = Mathf.Abs(heightMM);
                if (h >= bestHeight) continue;
                bestHeight = h;
                best = el;
                bestX = offX;
                bestY = offY;
            }
            if (best == null) return null;

            _yawDeg = YawRelativeTo(best);
            var (clampedX, clampedY) = ClampOffsets(PartPlane.Of(best), bestX, bestY);
            Mount.CaptureCatch(clampedX, clampedY);
            return best;
        }

        private (int offXMM, int offYMM) ClampOffsets(PartPlane plane, int offXMM, int offYMM)
        {
            var (cutW, cutD) = CutoutExtentsMM;
            int maxX = (plane.SizeAlongA - cutW) / 2 - MIN_EDGE_MM;
            int maxY = (plane.SizeAlongB - cutD) / 2 - MIN_EDGE_MM;
            return (Mathf.Clamp(offXMM, -maxX, maxX), Mathf.Clamp(offYMM, -maxY, maxY));
        }

        private CutoutBody BodyOn(PartPlane plane, int offXMM, int offYMM)
        {
            var (cutW, cutD) = CutoutExtentsMM;
            return new CutoutBody(plane, offXMM, offYMM, cutW, cutD, BodyHeightMM);
        }

        public bool BodyBlocked(KitchenElement part, int offX, int offY) =>
            FirstBlocker(part, offX, offY) != null;

        public string? FirstBlocker(KitchenElement part, int offX, int offY) =>
            BodyOn(PartPlane.Of(part), offX, offY).FirstBlocker(this);

        public string DescribeCatch(KitchenElement part)
        {
            if (!IsSuitableHost(part)) return "деталь не годится под варочную";
            var plane = PartPlane.Of(part);
            var (offX, offY, height) = plane.PoseOf(transform.position);
            bool over = plane.CoversOffset(offX, offY);
            var (cx, cy) = ClampOffsets(plane, offX, offY);
            return $"height={height:F1}мм ({Mount.DescribeCatchBand()}) " +
                   $"over={over} off=({offX},{offY})→({cx},{cy}) " +
                   $"blocker={FirstBlocker(part, cx, cy) ?? "-"}";
        }

        private void AlignToPart(KitchenElement part)
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

            bool memoChanged = _memoPart != part
                || _memoOffsetXMM != offX || _memoOffsetYMM != offY
                || _memoCutoutWidthMM != CutoutWidthMM || _memoCutoutDepthMM != CutoutDepthMM
                || !Mathf.Approximately(_memoYawDeg, _yawDeg);

            Mount.MarkAligned(part);
            _appliedRotation = transform.rotation;
            _hasAppliedRotation = true;

            if (!memoChanged) return;

            _memoPart = part;
            _memoOffsetXMM = offX;
            _memoOffsetYMM = offY;
            _memoCutoutWidthMM = CutoutWidthMM;
            _memoCutoutDepthMM = CutoutDepthMM;
            _memoYawDeg = _yawDeg;
            part.RebuildGrooveMesh();
        }

        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part)
        {
            if (part == null) return default;
            var plane = PartPlane.Of(part);
            if (plane.SizeAlongA <= 0 || plane.SizeAlongB <= 0) return default;

            var (cutW, cutD) = CutoutExtentsMM;
            float halfW = cutW * 0.5f;
            float halfD = cutD * 0.5f;
            return new GrooveMesh.Rect2
            {
                xMin = (Mount.OffsetXMM - halfW) / plane.SizeAlongA,
                xMax = (Mount.OffsetXMM + halfW) / plane.SizeAlongA,
                yMin = (Mount.OffsetYMM - halfD) / plane.SizeAlongB,
                yMax = (Mount.OffsetYMM + halfD) / plane.SizeAlongB,
            };
        }

        public Vector3 BodyCenter =>
            transform.position - transform.rotation *
                new Vector3(0f, BodyHeightMM * 0.5f * AppConstants.MM_TO_UNITS, 0f);

        public Vector3 BodySize => new Vector3(
            CutoutWidthMM * AppConstants.MM_TO_UNITS,
            BodyHeightMM * AppConstants.MM_TO_UNITS,
            CutoutDepthMM * AppConstants.MM_TO_UNITS);

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            var dims = Data.DimensionsMM;
            box.size = new Vector3(dims.x * toU, dims.y * toU, dims.z * toU);
            box.center = new Vector3(0f, (RIM_HEIGHT_MM - dims.y) * 0.5f * toU, 0f);
        }

        private void RebuildGeometry()
        {
            Mesh.Rebuild(Data.DimensionsMM, BodyHeightMM, CutoutWidthMM, CutoutDepthMM, HasFixedSize);
            ApplyMaterials();
            MaterialManager.RefreshTiling(this);
        }

        public void ApplyMaterials()
        {
            Material? decor = null;
            if (MaterialManager.HasCustomDecor(this))
                decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            Mesh.ApplySurfaceMaterial(decor ?? ApplianceMaterials.CooktopGlass);
        }

        public void DestroyChildren() => Mesh.Destroy();

        public override void PrepareForDestruction()
        {
            UnregisterFromPart();
            DestroyChildren();
        }

        protected override void OnElementDestroyed()
        {
            UnregisterFromPart();
            DestroyChildren();
        }
    }
}
