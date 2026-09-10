using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement, IOpenable, IParksAtAGestureLimit
    {
        public override bool IsFlatBoardElement => true;

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => true;

        public override Vector3 AttachRestPosition => ClosedPosition;

        public override Quaternion AttachRestRotation => ClosedRotation;


        public override string DisplayTypeName => FacadeBody.DISPLAY_TYPE_NAME;

        public override CutoutNeighbourRole CutoutRole => FacadeBody.CUTOUT_ROLE;

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

        public const int DEFAULT_GAP_MM = FacadeBody.DEFAULT_GAP_MM;

        public override bool SupportsGaps => FacadeBody.SUPPORTS_GAPS;

        public FacadeBody Body =>
            new FacadeBody(Data.DimensionsMM, Data.Gaps,
                ValidationPositionAt(transform.position), ValidationRotation);

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

        private const float OpenSeconds = AppConstants.OPENING_ANIM_DURATION_SECONDS;

        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        [SerializeField] private bool _isPassenger;
        private bool _openTarget;
        private float _doorProgress;
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;
        private float _cachedSafeProgress = 1f;
        private int _obstacleCheckRevision = -1;
        private bool _parkedAtLimit;
        private readonly System.Collections.Generic.List<KitchenElement> _ridersOfThisGesture =
            new System.Collections.Generic.List<KitchenElement>();
        private int _ridersRevision = -1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _activeStepDoors;

        public static int TakeActiveStepDoorCalls()
        {
            int n = _activeStepDoors;
            _activeStepDoors = 0;
            return n;
        }
#endif

        [NotUndoable("режим пассажира — ставится хостом при пристёгивании")]
        public bool IsPassenger
        {
            get => _isPassenger;
            set
            {
                _isPassenger = value;
                if (!value) enabled = true;
            }
        }

        [Undoable]
        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; _obstacleCheckRevision = -1; enabled = true; if (_doorProgress > 0f) ApplyDoor(); }
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
            _obstacleCheckRevision = -1;
            _ridersRevision = -1;
            if (!Mathf.Approximately(_doorProgress, open ? 1f : 0f))
            {
                enabled = true;
                FrameRateManager.KeepAwake(OpenSeconds + AppConstants.OPENING_KEEP_AWAKE_MARGIN_SECONDS);
            }
        }

        public void ForceClose()
        {
            if (_isPassenger) return;
            if (_doorProgress <= 0f && !_openTarget) return;
            _openTarget = false;
            _doorProgress = 0f;
            ForgetTheRidersOfThisGesture();
            _parkedAtLimit = false;
            enabled = false;
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
            _obstacleCheckRevision = -1;
        }

        internal void CaptureClosedPose() => CaptureClosed();

        private void Update() => StepDoor(Time.deltaTime);

        public void StepDoor(float dt)
        {
            if (_isPassenger)
            {
                _parkedAtLimit = false;
                enabled = false;
                return;
            }

            using var _ = PerfMarkers.FacadeStepDoor.Auto();

            float target = _openTarget ? 1f : 0f;
            if (Mathf.Approximately(_doorProgress, target))
            {
                if (_doorProgress <= 0f) CaptureClosed();
                ForgetTheRidersOfThisGesture();
                _parkedAtLimit = false;
                enabled = false;
                return;
            }

            if (StillBlockedOnThisRevision)
            {
                _parkedAtLimit = true;
                enabled = false;
                return;
            }

            _parkedAtLimit = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _activeStepDoors++;
#endif
            float progressBeforeThisFrame = _doorProgress;

            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _doorProgress = Mathf.MoveTowards(_doorProgress, target, step);

            if (_openTarget && _doorProgress > 0f)
            {
                float safe = SafeProgress();
                if (safe < _doorProgress) _doorProgress = Mathf.Max(_doorProgress - step, safe);
            }

            if (!Mathf.Approximately(_doorProgress, progressBeforeThisFrame))
            {
                ApplyDoor();
                SceneChangeTracker.NoteSelfAnimated(this);
                var riders = RidersOfThisGesture();
                for (int i = 0; i < riders.Count; i++)
                    SceneChangeTracker.NoteSelfAnimated(riders[i]);
            }
        }

        private System.Collections.Generic.List<KitchenElement> RidersOfThisGesture()
        {
            if (_ridersRevision != SceneRevision.Version)
            {
                _ridersOfThisGesture.Clear();
                AttachLinks.Descendants(this, _ridersOfThisGesture);
                _ridersRevision = SceneRevision.Version;
            }
            return _ridersOfThisGesture;
        }

        private void ForgetTheRidersOfThisGesture()
        {
            _ridersOfThisGesture.Clear();
            _ridersRevision = -1;
        }

        private float SafeProgress()
        {
            if (_obstacleCheckRevision == SceneRevision.Version) return _cachedSafeProgress;

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
            exclude.AddRange(RidersOfThisGesture());

            _cachedSafeProgress = OpeningCollision.FindMaxProgress(this, GetOpenBoxes, exclude);
            _obstacleCheckRevision = SceneRevision.Version;
            return _cachedSafeProgress;
        }

        public bool IsParkedAtALimit => _parkedAtLimit;

        private bool StillBlockedOnThisRevision =>
            _openTarget && _obstacleCheckRevision == SceneRevision.Version
            && Mathf.Approximately(_doorProgress, _cachedSafeProgress);

        private void ApplyDoor()
        {
            if (_isPassenger) return;
            using var _ = PerfMarkers.FacadeApplyDoor.Auto();
            var half = transform.localScale * 0.5f;
            FacadeDoor.Pose(_closedPos, _closedRot, half, _mode, _doorProgress, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        internal void ShiftClosedPose(Vector3 worldDelta)
        {
            _closedPos += worldDelta;
            _obstacleCheckRevision = -1;
            enabled = true;
            ApplyDoor();
        }

        protected override void OnResetToPristineState()
        {
            _mode = DoorMode.HingeFrontLeft;
            _isPassenger = false;
            _openTarget = false;
            _doorProgress = 0f;
            _closedPos = Vector3.zero;
            _closedRot = Quaternion.identity;
            _cachedSafeProgress = 1f;
            _obstacleCheckRevision = -1;
            _parkedAtLimit = false;
            ForgetTheRidersOfThisGesture();
            enabled = true;
        }
    }
}
