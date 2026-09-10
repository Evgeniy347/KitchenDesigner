using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class DrawerElement : KitchenElement, IFacadeHost, IOpenable, IQuantifies
    {
        public override bool CanFollowAnAttachParent => false;

        public override Vector3 AttachRestPosition => ClosedPosition;

        public override Quaternion AttachRestRotation => ClosedRotation;

        public override string DisplayTypeName => DrawerConstants.GetDefaultName(System);

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.AlignsCutout;

        public override KitchenElement InspectedElement =>
            IsUpperDrawer ? (KitchenElement?)FindPaired() ?? this : this;

        public bool IsClosedPose => !IsOpen && !IsAnimating;

        public string OpenActionLabel => FindPaired() != null
            ? DrawerConstants.GetCycleButtonLabel(DoubleState)
            : (IsOpen ? OpenLabels.CloseDrawer : OpenLabels.OpenDrawer);

        public void CycleOpenState()
        {
            if (FindPaired() != null) CycleDoubleState();
            else ToggleOpen();
        }
        private const float OpenSeconds = DrawerConstants.DRAWER_ANIM_DURATION;
        private const float DrawerSlideMeters = DrawerConstants.DRAWER_SLIDE_METERS;

        private MeshFilter? _filter;

        [SerializeField] private DrawerSystem _system = DrawerSystem.Gtv;
        [SerializeField] private DrawerType _type = DrawerType.A;
        [SerializeField] private int _nominalLength = 350;
        [SerializeField] private DrawerColor _color = DrawerColor.Anthracite;
        [SerializeField] private int _internalWidth = 400;
        [SerializeField] private bool _isDouble = false;
        [SerializeField] private bool _isUpperDrawer = false;
        [SerializeField] private string _pairedDrawerName = "";
        [SerializeField] private string _attachedFacadeName = "";
        [SerializeField] private DoubleDrawerState _doubleState = DoubleDrawerState.Closed;

        private bool _open;
        private float _t;
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        [Undoable]
        public DrawerSystem System
        {
            get => _system;
            set
            {
                if (_system == value) return;
                _system = value;
                RebuildMesh();
                if (!_isUpperDrawer)
                {
                    var pair = FindPairedDrawer();
                    if (pair != null && pair._system != value) pair.System = value;
                }
            }
        }

        [Undoable]
        public DrawerType Type
        {
            get => _type;
            set { _type = value; ApplyDimensions(); }
        }

        [Undoable]
        public int NominalLength
        {
            get => _nominalLength;
            set
            {
                if (!DrawerConstants.IsValidLength(value) || value == _nominalLength) return;
                _nominalLength = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public DrawerColor Color
        {
            get => _color;
            set
            {
                _color = value;
                MaterialManager.ApplyById(this, DrawerConstants.GetColorMaterialId(value));
                if (!_isUpperDrawer)
                {
                    var pair = FindPairedDrawer();
                    if (pair != null && pair._color != value) pair.Color = value;
                }
            }
        }

        [Undoable]
        public int InternalWidth
        {
            get => _internalWidth;
            set
            {
                int clamped = Mathf.Max(100, value);
                if (clamped == _internalWidth) return;
                _internalWidth = clamped;
                var dims = Data.DimensionsMM;
                dims.x = clamped;
                Data.DimensionsMM = dims;
                ApplyDimensions();
            }
        }

        public int BoxWidth =>
            _system == DrawerSystem.Movento
                ? _internalWidth - DrawerConstants.MOVENTO_WIDTH_INSET
                : _internalWidth;

        [NotUndoable("структура пары: ставится при создании/удалении второй коробки, откатывается Create/DeleteCommand")]
        public bool IsDouble
        {
            get => _isDouble;
            set => _isDouble = value;
        }

        [NotUndoable("см. IsDouble — роль в паре, а не правка свойств")]
        public bool IsUpperDrawer
        {
            get => _isUpperDrawer;
            set => _isUpperDrawer = value;
        }

        [NotUndoable("обратная ссылка пары, ведёт DrawerLinks")]
        public string PairedDrawerName
        {
            get => _pairedDrawerName;
            set => _pairedDrawerName = value;
        }

        [NotUndoable("обратная ссылка на фасад, ведёт DrawerLinks")]
        public string AttachedFacadeName
        {
            get => _attachedFacadeName;
            set => _attachedFacadeName = value;
        }

        public float FacadeMountGapMm => 0f;

        public void OnAttachedFacadeChanged(FacadeElement? oldFacade, FacadeElement? newFacade)
        {
            if (oldFacade != null && oldFacade.IsPassenger) oldFacade.IsPassenger = false;
        }

        [NotUndoable("показ анимации выдвижения, а не правка документа")]
        public DoubleDrawerState DoubleState
        {
            get => _doubleState;
            set => ApplyDoubleState(value, syncPair: true);
        }

        private void ApplyDoubleState(DoubleDrawerState value, bool syncPair)
        {
            _doubleState = value;
            bool willOpen = _isUpperDrawer
                ? (value == DoubleDrawerState.BothOpen)
                : (value != DoubleDrawerState.Closed);
            if (willOpen && _t <= 0f) CaptureClosed();
            _open = willOpen;
            SyncAttachedFacade();

            if (syncPair)
            {
                var paired = FindPairedDrawer();
                if (paired != null && paired._doubleState != value)
                    paired.ApplyDoubleState(value, syncPair: false);
            }
        }

        private DrawerElement? FindPairedDrawer()
        {
            using var _ = PerfMarkers.DrawerFindPaired.Auto();
            if (string.IsNullOrEmpty(_pairedDrawerName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is DrawerElement d && d != this && d.PartName == _pairedDrawerName) return d;
            return null;
        }

        public DrawerElement? FindPaired() => FindPairedDrawer();

        public FacadeElement? FindAttachedFacade()
        {
            if (string.IsNullOrEmpty(_attachedFacadeName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is FacadeElement f && f.PartName == _attachedFacadeName) return f;
            return null;
        }

        private void SyncAttachedFacade()
        {
            var f = FindAttachedFacade();
            if (f != null && f.IsOpen != _open) f.SetOpen(_open);
        }

        public Vector3 ClosedPosition => (!_open && _t <= 0f) ? transform.position : _closedPos;

        public Quaternion ClosedRotation => (!_open && _t <= 0f) ? transform.rotation : _closedRot;

        protected override Vector3 ValidationPosition => ClosedPosition;
        protected override Quaternion ValidationRotation => ClosedRotation;

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            PoseFollowsTransform ? transformPosition : ClosedPosition;

        public override bool PoseFollowsTransform => !_open && _t <= 0f;

        public float AnimProgress => _t;

        public bool IsOpen => _open;

        public bool IsAnimating => !Mathf.Approximately(_t, _open ? 1f : 0f);

        public override void ApplyDimensions()
        {
            _internalWidth = Mathf.Max(100, Data.DimensionsMM.x);

            int openingHeight = DrawerConstants.GetMinOpeningHeight(_type);
            Data.DimensionsMM = new Vector3Int(
                _internalWidth,
                openingHeight,
                _nominalLength
            );
            transform.localScale = new Vector3(
                _internalWidth * AppConstants.MM_TO_UNITS,
                openingHeight * AppConstants.MM_TO_UNITS,
                _nominalLength * AppConstants.MM_TO_UNITS
            );
            RebuildMesh();

            if (!_isUpperDrawer)
            {
                var pair = FindPairedDrawer();
                if (pair != null && pair._internalWidth != _internalWidth)
                    pair.InternalWidth = _internalWidth;
            }
        }

        public void RebuildMesh()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_filter == null) return;

            var mesh = _system == DrawerSystem.Movento
                ? MoventoDrawerMesh.Build(_internalWidth, _type, _nominalLength)
                : DrawerMesh.Build(_internalWidth, _type, _nominalLength);
            AdoptOwnedMesh(mesh);
            _filter.sharedMesh = mesh;
        }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            string decor = MaterialCatalog.Get(MaterialId).displayName;

            if (_system == DrawerSystem.Movento)
            {
                foreach (var part in MoventoDrawerMesh.ComputeParts(_internalWidth, _type, _nominalLength))
                    yield return new SpecItem(SpecSections.Furniture, part.suffix,
                        part.materialKind ?? decor, SpecUnit.AreaM2,
                        BoardFaceArea.FaceAreaM2(part.dimsMM), part.dimsMM, hasDims: true);
                yield break;
            }

            yield return new SpecItem(SpecSections.Furniture,
                $"Комплект {DrawerConstants.GetSystemLabel(_system)}", "", SpecUnit.Pieces, 1f,
                DimensionsMM, hasDims: true);

            var bottom = GtvDrawerBoardParts.BottomDimsMM(_internalWidth, _nominalLength);
            yield return new SpecItem(SpecSections.Furniture, MoventoDrawerMesh.SUFFIX_BOTTOM, decor,
                SpecUnit.AreaM2, BoardFaceArea.FaceAreaM2(bottom), bottom, hasDims: true);

            var back = GtvDrawerBoardParts.BackDimsMM(_internalWidth, _type);
            yield return new SpecItem(SpecSections.Furniture, MoventoDrawerMesh.SUFFIX_BACK, decor,
                SpecUnit.AreaM2, BoardFaceArea.FaceAreaM2(back), back, hasDims: true);
        }

        private void Update() => StepAnimation(Time.deltaTime);

        private void LateUpdate()
        {
            if (_isUpperDrawer) SyncToLower();
        }

        public void SyncToLower()
        {
            using var _ = PerfMarkers.DrawerSyncToLower.Auto();
            var lower = FindPairedDrawer();
            if (lower == null || lower._isUpperDrawer) return;

            float step = AppConstants.HalfHeightUnits(DrawerConstants.GetMinOpeningHeight(lower.Type)
                        + DrawerConstants.GetMinOpeningHeight(_type));
            _closedPos = lower.ClosedPosition + lower.ClosedRotation * Vector3.up * step;
            _closedRot = lower.ClosedRotation;
            ApplyAnimPose();
        }

        public void StepAnimation(float dt)
        {
            using var _ = PerfMarkers.DrawerStepAnimation.Auto();
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target))
            {
                if (_t <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);

            if (_open && _t > 0f)
            {
                var exclude = new System.Collections.Generic.List<KitchenElement> { this };
                var f = FindAttachedFacade();
                if (f != null) exclude.Add(f);
                var pair = FindPairedDrawer();
                if (pair != null)
                {
                    exclude.Add(pair);
                    var pairFacade = pair.FindAttachedFacade();
                    if (pairFacade != null) exclude.Add(pairFacade);
                }
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBoxes, exclude);
                if (safe < _t)
                {
                    _t = Mathf.Max(_t - step, safe);
                    if (!_isUpperDrawer && pair != null && pair._isUpperDrawer && pair._t > _t)
                        pair._t = _t;
                }
            }

            ApplyAnimPose();
        }

        public void GetOpenBoxes(float progress, List<OrientedBox> into)
        {
            float eased = 0.5f * (1f - Mathf.Cos(Mathf.PI * progress));
            Vector3 pos = _closedPos + _closedRot * Vector3.forward * (DrawerSlideMeters * eased);

            into.Add(new OrientedBox(pos, _closedRot, transform.localScale * 0.5f));

            var f = FindAttachedFacade();
            if (f != null) f.GetOpenBoxes(progress, into);
        }

        public void SetOpen(bool open)
        {
            if (open && _t <= 0f) CaptureClosed();
            _open = open;
            SyncAttachedFacade();
            if (!Mathf.Approximately(_t, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + AppConstants.OPENING_KEEP_AWAKE_MARGIN_SECONDS);
        }

        public void ToggleOpen() => SetOpen(!_open);

        public void ForceClose()
        {
            var f = FindAttachedFacade();
            if (f != null) f.ForceClose();
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);
        }

        public void CycleDoubleState() => DoubleState = DrawerConstants.NextCycleState(_doubleState);

        private void CaptureClosed()
        {
            _closedPos = transform.position;
            _closedRot = transform.rotation;
        }

        private void ApplyAnimPose()
        {
            float eased = 0.5f * (1f - Mathf.Cos(Mathf.PI * _t));
            Vector3 offset = _closedRot * Vector3.forward * (DrawerSlideMeters * eased);
            transform.SetPositionAndRotation(_closedPos + offset, _closedRot);
        }
    }
}
