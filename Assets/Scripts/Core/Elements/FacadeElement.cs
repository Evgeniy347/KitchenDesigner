using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement, IOpenable
    {
        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => true;

        public override Vector3 AttachRestPosition => ClosedPosition;

        public override Quaternion AttachRestRotation => ClosedRotation;


        public override string DisplayTypeName => "Фасад";

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.AlignsCutout;

        public override ElementDisposal Disposal =>
            GetType() == typeof(FacadeElement) ? ElementDisposal.FacadePool : ElementDisposal.Destroy;

        public bool IsClosedPose => IsDoorClosed;

        public IOpenable OpenTarget()
        {
            if (string.IsNullOrEmpty(PartName)) return this;
            foreach (var el in PartRegistry.All)
                if (el is IFacadeHost host && el is IOpenable openable
                    && host.AttachedFacadeName == PartName)
                    return openable;
            return this;
        }

        public string OpenActionLabel
        {
            get
            {
                var host = OpenTarget();
                return ReferenceEquals(host, this)
                    ? (IsOpen ? OpenLabels.Close : OpenLabels.Open)
                    : host.OpenActionLabel;
            }
        }

        public void CycleOpenState()
        {
            var host = OpenTarget();
            if (ReferenceEquals(host, this)) ToggleOpen();
            else host.CycleOpenState();
        }

        public const int DEFAULT_GAP_MM = 2;

        public override bool SupportsGaps => true;

        protected override Vector3 ValidationPosition => _isPassenger ? transform.position : ClosedPosition;

        protected override Quaternion ValidationRotation => _isPassenger ? transform.rotation : ClosedRotation;

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition)
            => _isPassenger ? transformPosition : (IsDoorClosed ? transformPosition : _closedPos);

        private void CornerUnits(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
            => GappedBox.CornerUnits(transform.localScale, Data.Gaps,
                out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

        public void GetOpenBoxes(float progress, System.Collections.Generic.List<OrientedBox> into)
        {
            if (_isPassenger)
            {
                into.Add(new OrientedBox(transform.position, transform.rotation,
                    transform.localScale * 0.5f));
                return;
            }

            var cp = IsDoorClosed ? transform.position : _closedPos;
            var cr = IsDoorClosed ? transform.rotation : _closedRot;
            var halfExtents = transform.localScale * 0.5f;

            FacadeDoor.Pose(cp, cr, halfExtents, _mode, progress, out var pos, out var rot);

            CornerUnits(out var minX, out var maxX, out var minY, out var maxY, out var minZ, out var maxZ);
            var center = new Vector3(minX + maxX, minY + maxY, minZ + maxZ) * 0.5f;
            var half = new Vector3(maxX - minX, maxY - minY, maxZ - minZ) * 0.5f;

            into.Add(new OrientedBox(pos + rot * center, rot, half));
        }

        private const float OpenSeconds = 0.4f;

        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        [SerializeField] private bool _isPassenger;
        private bool _openTarget;
        private float _doorProgress;
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        [NotUndoable("режим пассажира — ставится хостом при пристёгивании")]
        public bool IsPassenger
        {
            get => _isPassenger;
            set => _isPassenger = value;
        }

        [Undoable]
        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; if (_doorProgress > 0f) ApplyDoor(); }
        }

        public void CycleMode() => Mode = FacadeDoor.Next(_mode);

        public bool IsOpen => _openTarget;
        public float DoorProgress => _doorProgress;
        public bool IsDoorClosed => !_openTarget && _doorProgress <= 0f;

        public override bool PoseFollowsTransform => _isPassenger || IsDoorClosed;

        public Vector3 ClosedPosition => _isPassenger ? _closedPos : (IsDoorClosed ? transform.position : _closedPos);
        public Quaternion ClosedRotation => _isPassenger ? _closedRot : (IsDoorClosed ? transform.rotation : _closedRot);

        public void ToggleOpen() => SetOpen(!_openTarget);

        public void SetOpen(bool open)
        {
            if (_isPassenger)
            {
                if (open && IsDoorClosed) CaptureClosed();
                _openTarget = open;
                return;
            }
            if (open && _doorProgress <= 0f) CaptureClosed();
            _openTarget = open;
            if (!Mathf.Approximately(_doorProgress, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        public void ForceClose()
        {
            if (_isPassenger) return;
            if (_doorProgress <= 0f && !_openTarget) return;
            _openTarget = false;
            _doorProgress = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);

            foreach (var el in PartRegistry.All)
                if (el is DrawerElement d && d.AttachedFacadeName == PartName)
                {
                    d.ForceClose();
                    break;
                }
        }

        private void CaptureClosed()
        {
            _closedPos = transform.position;
            _closedRot = transform.rotation;
        }

        internal void CaptureClosedPose() => CaptureClosed();

        private void Update() => StepDoor(Time.deltaTime);

        public void StepDoor(float dt)
        {
            if (_isPassenger) return;
            using var _ = PerfMarkers.FacadeStepDoor.Auto();
            float target = _openTarget ? 1f : 0f;
            if (Mathf.Approximately(_doorProgress, target))
            {
                if (_doorProgress <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _doorProgress = Mathf.MoveTowards(_doorProgress, target, step);

            if (_openTarget && _doorProgress > 0f)
            {
                var exclude = new System.Collections.Generic.List<KitchenElement>();
                foreach (var el in PartRegistry.All)
                {
                    if (el is DrawerElement d && d.AttachedFacadeName == PartName)
                    {
                        exclude.Add(d);
                        var pair = d.FindPaired();
                        if (pair != null) exclude.Add(pair);
                        break;
                    }
                }
                exclude.AddRange(AttachLinks.Descendants(this));
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBoxes, exclude);
                if (safe < _doorProgress) _doorProgress = Mathf.Max(_doorProgress - step, safe);
            }

            ApplyDoor();
        }

        private void ApplyDoor()
        {
            if (_isPassenger) return;
            var half = transform.localScale * 0.5f;
            FacadeDoor.Pose(_closedPos, _closedRot, half, _mode, _doorProgress, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        internal void ShiftClosedPose(Vector3 worldDelta)
        {
            _closedPos += worldDelta;
            ApplyDoor();
        }
    }
}
