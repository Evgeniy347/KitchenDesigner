using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SinkElement : KitchenElement, IPartCutout, IPaintsItself
    {
        public override string DisplayTypeName => "Мойка";

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public const int MODULE_WIDTH_MM = 600;
        public const int OUTER_WIDTH_MM = 500;
        public const int OUTER_DEPTH_MM = 500;
        public const int RIM_HEIGHT_MM = 8;
        public const int RIM_WIDTH_MM = 30;
        public const int CUTOUT_CLEARANCE_MM = 10;
        public const int BOWL_DEPTH_MM = 180;
        public const int BOWL_WALL_MM = 10;
        public const int MIN_EDGE_MM = 30;

        public const int SNAP_CATCH_MM = 100;
        public const int SNAP_RELEASE_MM = 60;

        public const float ALIGNED_ROTATION_EPSILON_DEG = 0.05f;

        public static int CutoutWidthMM => OUTER_WIDTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int CutoutDepthMM => OUTER_DEPTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int TotalHeightMM => RIM_HEIGHT_MM + BOWL_DEPTH_MM;

        public static int MinPartWidthMM => CutoutWidthMM + 2 * MIN_EDGE_MM;
        public static int MinPartDepthMM => CutoutDepthMM + 2 * MIN_EDGE_MM;

        [SerializeField] private PartMount _mount = new PartMount();

        private bool _mountConfigured;
        private SinkMesh? _mesh;
        private int _faucetSign = 1;

        private KitchenElement? _memoPart;
        private int _memoOffsetXMM = int.MinValue;
        private int _memoOffsetYMM = int.MinValue;
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

        private SinkMesh Mesh => _mesh ??= new SinkMesh(transform);

        private Material? Skin() => SanitaryDecor.IsFactoryLook(MaterialId)
            ? null
            : MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));

        public void SetMaterial(Material material) => Mesh.ApplyMaterials(
            SanitaryDecor.IsFactoryLook(MaterialId) ? null : material);

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

        protected override Vector3 EffectiveScale => new Vector3(
            OUTER_WIDTH_MM * AppConstants.MM_TO_UNITS,
            RIM_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            OUTER_DEPTH_MM * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, RIM_HEIGHT_MM * 0.5f * AppConstants.MM_TO_UNITS, 0f);

        private void Start()
        {
            SnapToPart();
        }

        private void Update()
        {
            if (PoseVersion == _lastPoseVersion) return;
            _lastPoseVersion = PoseVersion;
            SnapToPart();
        }

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            Data.DimensionsMM = new Vector3Int(OUTER_WIDTH_MM, TotalHeightMM, OUTER_DEPTH_MM);
            UpdateCollider();
            Mesh.Rebuild(_faucetSign, Skin());
        }

        public void SnapToPart()
        {
            using var _ = PerfMarkers.SinkSnapToPart.Auto();
            var part = Mount.CurrentOrNamedPart();
            Mount.TrackDrift(part);

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
            AlignToPart(part);
        }

        internal void UnregisterFromPart() => Mount.Detach();

        private void ForgetAlignmentMemo()
        {
            _memoPart = null;
            _memoOffsetXMM = int.MinValue;
            _memoOffsetYMM = int.MinValue;
        }

        public static int HoleAxisFor(KitchenElement part) => PartPlane.Of(part).UpAxis;

        public int HoleAxisIn(KitchenElement part) => HoleAxisFor(part);

        public static bool IsSuitableHost(KitchenElement part)
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

                var (clampedX, clampedY) = ClampOffsets(plane, offX, offY);
                if (CutoutBlocked(el, clampedX, clampedY)) continue;

                float h = Mathf.Abs(heightMM);
                if (h >= bestHeight) continue;
                bestHeight = h;
                best = el;
                bestX = clampedX;
                bestY = clampedY;
            }
            if (best == null) return null;

            Mount.CaptureCatch(bestX, bestY);
            return best;
        }

        private static (int offXMM, int offYMM) ClampOffsets(PartPlane plane, int offXMM, int offYMM)
        {
            int maxX = (plane.SizeAlongA - CutoutWidthMM) / 2 - MIN_EDGE_MM;
            int maxY = (plane.SizeAlongB - CutoutDepthMM) / 2 - MIN_EDGE_MM;
            return (Mathf.Clamp(offXMM, -maxX, maxX), Mathf.Clamp(offYMM, -maxY, maxY));
        }

        private static CutoutBody BowlOn(PartPlane plane, int offXMM, int offYMM) =>
            new CutoutBody(plane, offXMM, offYMM, CutoutWidthMM, CutoutDepthMM, BOWL_DEPTH_MM);

        public bool CutoutBlocked(KitchenElement part, int offX, int offY) =>
            FirstBlocker(part, offX, offY) != null;

        public string? FirstBlocker(KitchenElement part, int offX, int offY) =>
            BowlOn(PartPlane.Of(part), offX, offY).FirstBlocker(this);

        public string DescribeCatch(KitchenElement part)
        {
            if (!IsSuitableHost(part)) return "деталь не годится под мойку";
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
            Quaternion targetRot = plane.RestingRotation;

            var (offX, offY) = ClampOffsets(plane, Mount.OffsetXMM, Mount.OffsetYMM);
            (offX, offY) = SlideAroundBlocker(part, offX, offY);
            Mount.OffsetXMM = offX;
            Mount.OffsetYMM = offY;

            Vector3 targetPos = plane.SurfacePoint(offX, offY);
            if ((targetPos - transform.position).sqrMagnitude > Tolerance.EpsilonSqr ||
                Quaternion.Angle(targetRot, transform.rotation) > ALIGNED_ROTATION_EPSILON_DEG)
                transform.SetPositionAndRotation(targetPos, targetRot);

            bool memoChanged = _memoPart != part || _memoOffsetXMM != offX || _memoOffsetYMM != offY;

            Mount.MarkAligned(part);
            UpdateFaucetSide(plane, offY);

            if (!memoChanged) return;

            _memoPart = part;
            _memoOffsetXMM = offX;
            _memoOffsetYMM = offY;
            part.RebuildGrooveMesh();
        }

        private (int offXMM, int offYMM) SlideAroundBlocker(KitchenElement part, int offX, int offY)
        {
            bool hasPrevious = _memoPart == part && _memoOffsetXMM != int.MinValue;
            if (!hasPrevious || !CutoutBlocked(part, offX, offY)) return (offX, offY);

            if (!CutoutBlocked(part, offX, _memoOffsetYMM)) return (offX, _memoOffsetYMM);
            if (!CutoutBlocked(part, _memoOffsetXMM, offY)) return (_memoOffsetXMM, offY);
            return (_memoOffsetXMM, _memoOffsetYMM);
        }

        private void UpdateFaucetSide(PartPlane plane, int offY)
        {
            float spacePlus = plane.SizeAlongB * 0.5f - offY - OUTER_DEPTH_MM * 0.5f;
            float spaceMinus = plane.SizeAlongB * 0.5f + offY - OUTER_DEPTH_MM * 0.5f;
            int plusInSinkZ =
                Vector3.Dot(plane.ForwardLocal, PartPlane.AxisVector(plane.AxisB)) >= 0f ? 1 : -1;
            int faucetSign = spacePlus >= spaceMinus ? plusInSinkZ : -plusInSinkZ;
            if (faucetSign == _faucetSign) return;
            _faucetSign = faucetSign;
            Mesh.Rebuild(_faucetSign, Skin());
        }

        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part)
        {
            if (part == null) return default;
            var plane = PartPlane.Of(part);
            if (plane.SizeAlongA <= 0 || plane.SizeAlongB <= 0) return default;

            float halfW = CutoutWidthMM * 0.5f;
            float halfD = CutoutDepthMM * 0.5f;
            return new GrooveMesh.Rect2
            {
                xMin = (Mount.OffsetXMM - halfW) / plane.SizeAlongA,
                xMax = (Mount.OffsetXMM + halfW) / plane.SizeAlongA,
                yMin = (Mount.OffsetYMM - halfD) / plane.SizeAlongB,
                yMax = (Mount.OffsetYMM + halfD) / plane.SizeAlongB,
            };
        }

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            box.size = new Vector3(OUTER_WIDTH_MM * toU, TotalHeightMM * toU, OUTER_DEPTH_MM * toU);
            box.center = new Vector3(0f, (RIM_HEIGHT_MM - TotalHeightMM) * 0.5f * toU, 0f);
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
