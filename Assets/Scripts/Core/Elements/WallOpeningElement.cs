using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public abstract class WallOpeningElement : KitchenElement, IOpenable, IWallMounted, IPaintsItself, ICutsItsHost
    {
        public override bool CanFollowAnAttachParent => false;

        public bool IsClosedPose => IsDoorClosed;

        public string OpenActionLabel => IsOpen ? OpenLabels.Close : OpenLabels.Open;

        public void CycleOpenState() => ToggleOpen();

        private const float OpenSeconds = AppConstants.OPENING_ANIM_DURATION_SECONDS;
        private const float WallSwitchHysteresisU = 0.05f;

        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private string _attachedWallName = "";

        protected readonly List<GameObject> Children = new List<GameObject>();
        protected Transform? StaticGroup;
        protected Transform? SashGroup;

        private float _openT;
        protected Vector3 SashClosedLocal;
        protected Vector3 SashHalfExtents;
        private Vector3 _lastCutoutPos = new Vector3(float.NaN, 0f, 0f);
        private int _lastPoseVersion;
        private Wall? _attachedWall;
        private Wall? _seatWall;

        [Undoable]
        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; ApplyDoorPose(); }
        }

        public bool IsOpen => _isOpen;
        public float DoorProgress => _openT;

        [NotUndoable("служебная привязка к стене, вычисляется SnapToWall")]
        public string AttachedWallName { get => _attachedWallName; set => _attachedWallName = value ?? ""; }

        public Vector3 ClosedPosition => transform.position;
        public Quaternion ClosedRotation => transform.rotation;
        public bool IsDoorClosed => !_isOpen && _openT <= 0f;

        private void Start()
        {
            SnapToWall();
        }

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            UpdateCollider();
            EnsureChildren();
            RebuildGeometry();

            var wall = FindAttachedWall();
            if (wall != null) wall.RebuildMesh();
        }

        public void RefreshGeometry()
        {
            RebuildGeometry();
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (!Mathf.Approximately(_openT, open ? 1f : 0f))
            {
                enabled = true;
                FrameRateManager.KeepAwake(OpenSeconds + AppConstants.OPENING_KEEP_AWAKE_MARGIN_SECONDS);
            }
        }

        public void ToggleOpen() => SetOpen(!_isOpen);

        public void ForceClose()
        {
            if (_openT <= 0f && !_isOpen) return;
            _isOpen = false;
            _openT = 0f;
            ApplyDoorPose();
        }

        protected void ApplyDoorPose()
        {
            if (SashGroup == null) return;
            FacadeDoor.Pose(SashClosedLocal, Quaternion.identity, SashHalfExtents,
                _mode, _openT, out var pos, out var rot, HingeKinematics.EdgePivot);
            SashGroup.localPosition = pos;
            SashGroup.localRotation = rot;
        }

        internal void Update()
        {
            StepDoor(Time.deltaTime);
            if (PoseVersion != _lastPoseVersion)
            {
                _lastPoseVersion = PoseVersion;
                SnapToWall();
            }

            if (Mathf.Approximately(_openT, _isOpen ? 1f : 0f)) enabled = false;
        }

        protected override void OnOwnPoseVersionBumped() => enabled = true;

        public void StepDoor(float dt)
        {
            float target = _isOpen ? 1f : 0f;
            if (Mathf.Approximately(_openT, target)) return;
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _openT = Mathf.MoveTowards(_openT, target, step);

            if (_isOpen && _openT > 0f)
            {
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBoxes);
                if (safe < _openT) _openT = Mathf.Max(_openT - step, safe);
            }

            ApplyDoorPose();
        }

        public void GetOpenBoxes(float progress, List<OrientedBox> into)
        {
            if (SashGroup == null || SashHalfExtents.sqrMagnitude < 1e-12f)
            {
                var frame = transform.rotation;
                var origin = transform.position;
                into.Add(LocalFrame.ToWorld(LocalFrame.BoundsOf(GetVertices(), origin, frame),
                    origin, frame));
                return;
            }

            FacadeDoor.Pose(SashClosedLocal, Quaternion.identity, SashHalfExtents,
                _mode, progress, out var localPos, out var localRot, HingeKinematics.EdgePivot);

            into.Add(new OrientedBox(transform.TransformPoint(localPos),
                transform.rotation * localRot, SashHalfExtents));
        }

        public void SnapToWall()
        {
            using var _ = SnapToWallMarker.Auto();
            var wall = RegisterWithNearestWall();
            if (wall != null) AlignToWall(wall);
        }

        public void AttachToWall(Wall wall)
        {
            if (wall == null) return;
            UnregisterFromWall();
            _attachedWallName = wall.gameObject.name;
            _attachedWall = wall;
            RegisterOnWall(wall);
            _lastCutoutPos = transform.position;
            AlignToWall(wall);
        }

        private Wall? RegisterWithNearestWall()
        {
            var best = FindNearestWall();
            if (best == null) return null;
            if (best.gameObject.name != _attachedWallName || !IsRegisteredOn(best))
            {
                UnregisterFromWall();
                _attachedWallName = best.gameObject.name;
                _attachedWall = best;
                RegisterOnWall(best);
                _lastCutoutPos = transform.position;
            }
            return best;
        }

        private Wall? FindNearestWall()
        {
            float bestDist = float.MaxValue;
            float attachedDist = float.MaxValue;
            Wall? bestWall = null;
            Wall? attached = null;
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this) continue;
                var wall = el.GetComponent<Wall>();
                if (wall == null) continue;
                float dist = DistanceToWall(wall);
                if (wall.gameObject.name == _attachedWallName) { attached = wall; attachedDist = dist; }
                if (dist < bestDist) { bestDist = dist; bestWall = wall; }
            }
            if (attached != null && bestWall != attached &&
                attachedDist - bestDist < WallSwitchHysteresisU)
                return attached;
            return bestWall;
        }

        private float DistanceToWall(Wall wall)
        {
            var t = wall.transform;
            Vector3 center = wall.FullPosition;
            Vector3 half = t.localScale;
            half.y = wall.FullScaleY;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) * 0.5f;

            Vector3 local = Quaternion.Inverse(t.rotation) * (transform.position - center);
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            Vector3 closest = center + t.rotation * local;
            return (closest - transform.position).magnitude;
        }

        private void AlignToWall(Wall wall)
        {
            var wallEl = wall.GetComponent<KitchenElement>();
            if (wallEl == null) return;
            var wt = wall.transform;
            var wallDims = wallEl.DimensionsMM;

            bool thickAlongX = wallDims.x <= wallDims.z;
            int thicknessMM = Mathf.Min(wallDims.x, wallDims.z);

            Vector3 dir = thickAlongX ? wt.right : wt.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            if (Vector3.Dot(transform.forward, dir) < 0f) dir = -dir;
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);

            Vector3 local = wt.InverseTransformPoint(transform.position);
            if (thickAlongX) local.x = 0f; else local.z = 0f;
            Vector3 targetPos = wt.TransformPoint(local);

            if ((targetPos - transform.position).sqrMagnitude > Tolerance.EpsilonSqr ||
                Quaternion.Angle(targetRot, transform.rotation) > 0.05f)
                transform.SetPositionAndRotation(targetPos, targetRot);

            if (wallDims.y <= 0) return;
            int targetY = Mathf.Min(DimensionsMM.y, wallDims.y);
            int targetZ = thicknessMM;
            float toU = AppConstants.MM_TO_UNITS;
            float wallHalfH = wallDims.y * toU * 0.5f;
            float wallCenterY = wall.FullPosition.y;
            float alignedY = AlignedY(targetY, toU, wallHalfH, wallCenterY);
            if (Mathf.Abs(alignedY - transform.position.y) > Tolerance.EpsilonUnits)
            {
                transform.position = new Vector3(transform.position.x, alignedY, transform.position.z);
                _lastCutoutPos = new Vector3(float.NaN, 0f, 0f);
            }

            if (DimensionsMM.y != targetY || DimensionsMM.z != targetZ)
            {
                DimensionsMM = new Vector3Int(DimensionsMM.x, targetY, targetZ);
                _lastCutoutPos = transform.position;
            }

            if (float.IsNaN(_lastCutoutPos.x) ||
                (transform.position - _lastCutoutPos).sqrMagnitude > Tolerance.EpsilonSqr)
            {
                _lastCutoutPos = transform.position;
                wall.RebuildMesh();
            }
        }

        private Wall? FindAttachedWall()
        {
            if (string.IsNullOrEmpty(_attachedWallName)) return null;
            foreach (var el in PartRegistry.All)
            {
                if (el == null) continue;
                var wall = el.GetComponent<Wall>();
                if (wall != null && wall.gameObject.name == _attachedWallName)
                    return wall;
            }
            return null;
        }

        public void ReleaseHostCutout()
        {
            var seat = FindAttachedWall();
            if (seat == null && _attachedWall != null) seat = _attachedWall;
            _seatWall = seat;
            UnregisterFromWall();
        }

        public void RestoreHostCutout()
        {
            var seat = _seatWall;
            if (seat != null && seat.gameObject.activeInHierarchy)
            {
                _seatWall = null;
                AttachToWall(seat);
                return;
            }
            SnapToWall();
        }

        internal static void ResettlePendingOpeningsOf(KitchenElement host)
        {
            var wall = host != null ? host.GetComponent<Wall>() : null;
            if (wall == null) return;
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var opening = all[i] as WallOpeningElement;
                if (opening == null) continue;
                if (!ReferenceEquals(opening._seatWall, wall)) continue;
                opening.RestoreHostCutout();
            }
        }

        internal void UnregisterFromWall()
        {
            var wall = FindAttachedWall();
            if (wall == null && _attachedWall != null) wall = _attachedWall;
            _attachedWallName = "";
            _attachedWall = null;
            if (wall != null) UnregisterOnWall(wall);
        }

        protected int ComputeHiddenSides() =>
            OpeningNeighbourSides.HiddenSidesOf(this, FindAttachedWall());

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = EffectiveScale;
        }

        protected void EnsureChildGroups()
        {
            if (StaticGroup == null)
            {
                var staticGo = new GameObject("_Static");
                staticGo.transform.SetParent(transform, false);
                StaticGroup = staticGo.transform;
            }
            if (SashGroup == null)
            {
                var sashGo = new GameObject("_Sash");
                sashGo.transform.SetParent(transform, false);
                SashGroup = sashGo.transform;
            }
        }

        protected void EnsureChildCount(int needed, System.Func<int, string> childName, System.Func<int, bool> isSashChild)
        {
            while (Children.Count < needed)
            {
                int idx = Children.Count;
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = childName(idx);
                child.transform.SetParent(isSashChild(idx) ? SashGroup : StaticGroup, false);
                var col = child.GetComponent<BoxCollider>();
                if (col != null) Object.DestroyImmediate(col);
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Children.Add(child);
            }
        }

        protected void ApplyMaterialFrame()
        {
            var def = MaterialCatalog.Get(MaterialId);
            if (def == null) return;
            var mat = MaterialManager.GetSharedMaterial(def);
            if (mat != null) PaintFrame(mat);
        }

        public void SetMaterial(Material material)
        {
            if (material == null) return;
            PaintFrame(material);
        }

        public override void PrepareForDestruction()
        {
            UnregisterFromWall();
            DestroyChildren();
        }

        protected override void OnElementDestroyed()
        {
            UnregisterFromWall();
            DestroyChildren();
            DestroyGroup(ref StaticGroup);
            DestroyGroup(ref SashGroup);
        }

        private static void DestroyGroup(ref Transform? group)
        {
            if (group == null) return;
            if (Application.isPlaying) Object.Destroy(group.gameObject);
            else Object.DestroyImmediate(group.gameObject);
            group = null;
        }

        protected abstract PerfMarker SnapToWallMarker { get; }
        protected abstract void RegisterOnWall(Wall wall);
        protected abstract void UnregisterOnWall(Wall wall);
        protected abstract bool IsRegisteredOn(Wall wall);
        protected abstract float AlignedY(int targetY, float toU, float wallHalfH, float wallCenterY);
        protected abstract void EnsureChildren();
        protected abstract void RebuildGeometry();
        protected abstract void DestroyChildren();
        protected abstract void PaintFrame(Material material);
    }
}
