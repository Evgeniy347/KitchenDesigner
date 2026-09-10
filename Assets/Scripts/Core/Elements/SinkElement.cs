using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SinkElement : PartCutoutElement, IPaintsItself
    {
        public override string DisplayTypeName => "Мойка";

        public override bool ParticipatesInGapChecks => false;

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

        public static int CutoutWidthMM => OUTER_WIDTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int CutoutDepthMM => OUTER_DEPTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int TotalHeightMM => RIM_HEIGHT_MM + BOWL_DEPTH_MM;

        public static int MinPartWidthMM => CutoutWidthMM + 2 * MIN_EDGE_MM;
        public static int MinPartDepthMM => CutoutDepthMM + 2 * MIN_EDGE_MM;

        private SinkMesh? _mesh;
        private int _faucetSign = 1;

        private int _lastPoseVersion;

        protected override (int widthMM, int depthMM) CutoutExtentsMM => (CutoutWidthMM, CutoutDepthMM);

        protected override int RimHeightMM => RIM_HEIGHT_MM;

        protected override int MinEdgeMM => MIN_EDGE_MM;

        protected override string HostRejectionMessage => "деталь не годится под мойку";

        private SinkMesh Mesh => _mesh ??= new SinkMesh(transform);

        private Material? Skin() => SanitaryDecor.IsFactoryLook(MaterialId)
            ? null
            : MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));

        public void SetMaterial(Material material) => Mesh.ApplyMaterials(
            SanitaryDecor.IsFactoryLook(MaterialId) ? null : material);

        protected override Vector3 EffectiveScale => new Vector3(
            OUTER_WIDTH_MM * AppConstants.MM_TO_UNITS,
            RIM_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            OUTER_DEPTH_MM * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, AppConstants.HalfHeightUnits(RIM_HEIGHT_MM), 0f);

        private void Start()
        {
            SnapToPart();
        }

        internal void Update()
        {
            if (PoseVersion == _lastPoseVersion) { enabled = false; return; }
            _lastPoseVersion = PoseVersion;
            SnapToPart();
        }

        protected override void OnOwnPoseVersionBumped() => enabled = true;

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
            SnapToPartCore();
        }

        public static int HoleAxisFor(KitchenElement part) => PartPlane.Of(part).UpAxis;

        public static bool IsSuitableHost(KitchenElement part) =>
            IsHostSuitable(part, MinPartWidthMM, MinPartDepthMM);

        protected override bool AcceptsHost(KitchenElement part) => IsSuitableHost(part);

        protected override bool TryCandidate(
            KitchenElement candidate, PartPlane plane, ref int offXMM, ref int offYMM)
        {
            var (clampedX, clampedY) = ClampOffsets(plane, offXMM, offYMM);
            if (CutoutBlocked(candidate, clampedX, clampedY)) return false;
            offXMM = clampedX;
            offYMM = clampedY;
            return true;
        }

        private static CutoutBody BowlOn(PartPlane plane, int offXMM, int offYMM) =>
            new CutoutBody(plane, offXMM, offYMM, CutoutWidthMM, CutoutDepthMM, BOWL_DEPTH_MM);

        public bool CutoutBlocked(KitchenElement part, int offX, int offY) =>
            FirstBlocker(part, offX, offY) != null;

        protected override string? FirstBlocker(KitchenElement part, int offX, int offY) =>
            BowlOn(PartPlane.Of(part), offX, offY).FirstBlocker(this);

        protected override void AlignToPart(KitchenElement part)
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

            bool memoChanged = MemoPart != part || MemoOffsetXMM != offX || MemoOffsetYMM != offY;

            Mount.MarkAligned(part);
            UpdateFaucetSide(plane, offY);

            if (!memoChanged) return;

            MemoPart = part;
            MemoOffsetXMM = offX;
            MemoOffsetYMM = offY;
            part.RebuildGrooveMesh();
        }

        private (int offXMM, int offYMM) SlideAroundBlocker(KitchenElement part, int offX, int offY)
        {
            bool hasPrevious = MemoPart == part && MemoOffsetXMM != int.MinValue;
            if (!hasPrevious || !CutoutBlocked(part, offX, offY)) return (offX, offY);

            if (!CutoutBlocked(part, offX, MemoOffsetYMM)) return (offX, MemoOffsetYMM);
            if (!CutoutBlocked(part, MemoOffsetXMM, offY)) return (MemoOffsetXMM, offY);
            return (MemoOffsetXMM, MemoOffsetYMM);
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

        protected override void DestroyChildren() => Mesh.Destroy();
    }
}
