using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [Serializable]
    public class PartMount
    {
        [SerializeField] private string _attachedPartName = "";
        [SerializeField] private int _offsetXMM;
        [SerializeField] private int _offsetYMM;

        private IPartCutout? _owner;
        private Transform? _ownerTransform;
        private Func<KitchenElement, bool>? _isSuitableHost;
        private Action? _onReleased;
        private int _catchMM;
        private int _releaseMM;

        private KitchenElement? _part;
        private Vector3 _partPosition;
        private float _freeHeightMM;
        private Vector3 _appliedPosition;
        private bool _hasAppliedPosition;

        public void Configure(IPartCutout owner, Transform ownerTransform,
            Func<KitchenElement, bool> isSuitableHost, int catchMM, int releaseMM, Action onReleased)
        {
            _onReleased = onReleased;
            _owner = owner;
            _ownerTransform = ownerTransform;
            _isSuitableHost = isSuitableHost;
            _catchMM = catchMM;
            _releaseMM = releaseMM;
        }

        public string AttachedPartName
        {
            get => _attachedPartName;
            set => _attachedPartName = value ?? "";
        }

        public int OffsetXMM { get => _offsetXMM; set => _offsetXMM = value; }

        public int OffsetYMM { get => _offsetYMM; set => _offsetYMM = value; }

        public float FreeHeightMM => _freeHeightMM;

        public KitchenElement? Part => _part;

        public bool IsAttached => _part != null;

        public bool PartMoved =>
            _part != null &&
            (_part.transform.position - _partPosition).sqrMagnitude > Tolerance.EpsilonSqr;

        public KitchenElement? CurrentOrNamedPart() => _part != null ? _part : FindNamedPart();

        public KitchenElement? FindNamedPart()
        {
            if (string.IsNullOrEmpty(_attachedPartName)) return null;
            foreach (var el in PartRegistry.All)
                if (el != null && !ReferenceEquals(el, _owner) && el.PartName == _attachedPartName)
                    return el;
            return null;
        }

        public void TrackDrift(KitchenElement? part)
        {
            var t = _ownerTransform!;
            if (!_hasAppliedPosition)
            {
                _appliedPosition = t.position;
                _hasAppliedPosition = true;
                return;
            }
            Vector3 drift = t.position - _appliedPosition;
            _appliedPosition = t.position;
            if (part == null || drift.sqrMagnitude < Tolerance.EpsilonSqr) return;

            var (alongA, alongB, up) = PartPlane.Of(part).DecomposeMM(drift);
            _offsetXMM += Mathf.RoundToInt(alongA);
            _offsetYMM += Mathf.RoundToInt(alongB);
            _freeHeightMM += up;
        }

        public bool StillHolds(KitchenElement part) =>
            _isSuitableHost!(part) && _freeHeightMM >= -_releaseMM && _freeHeightMM <= _catchMM;

        public bool WithinCatchBand(float heightMM) => heightMM <= _catchMM && heightMM >= -_releaseMM;

        public string DescribeCatchBand() => $"полоса {-_releaseMM}..{_catchMM}";

        public KitchenElement? ReleaseIfLost(KitchenElement? part)
        {
            if (part == null || StillHolds(part)) return part;

            var plane = PartPlane.Of(part);
            var t = _ownerTransform!;
            float actualHeightMM = plane.PoseOf(t.position).heightMM;
            t.position += plane.UpWorld * ((_freeHeightMM - actualHeightMM) * AppConstants.MM_TO_UNITS);
            _appliedPosition = t.position;

            Detach();
            _freeHeightMM = 0f;
            _onReleased?.Invoke();
            return null;
        }

        public void CaptureCatch(int offXMM, int offYMM)
        {
            _offsetXMM = offXMM;
            _offsetYMM = offYMM;
            _freeHeightMM = 0f;
        }

        public void Adopt(KitchenElement part)
        {
            if (part.PartName == _attachedPartName && part.HasCutout(_owner!)) return;
            Detach();
            _attachedPartName = part.PartName;
            part.RegisterCutout(_owner!);
        }

        public void AttachTo(KitchenElement part)
        {
            Detach();
            _attachedPartName = part.PartName;
            part.RegisterCutout(_owner!);
            _freeHeightMM = 0f;
        }

        public void Detach()
        {
            var part = FindNamedPart();
            _attachedPartName = "";
            _part = null;
            if (part != null) part.UnregisterCutout(_owner!);
        }

        public void MarkAligned(KitchenElement part)
        {
            _part = part;
            _partPosition = part.transform.position;
            _appliedPosition = _ownerTransform!.position;
        }
    }
}
