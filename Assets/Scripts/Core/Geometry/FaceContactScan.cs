using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum FaceAlignment
    {
        ParallelEitherWay,
        FacingEachOther,
    }

    public readonly struct FaceContactHit
    {
        public readonly int IndexA;
        public readonly int IndexB;
        public readonly float PlaneGap;
        public readonly float OverlapArea;
        public readonly float OverlapRatio;

        public FaceContactHit(int indexA, int indexB, float planeGap, float overlapArea, float overlapRatio)
        {
            IndexA = indexA;
            IndexB = indexB;
            PlaneGap = planeGap;
            OverlapArea = overlapArea;
            OverlapRatio = overlapRatio;
        }
    }

    public readonly struct FaceContactScan
    {
        public const float NoLowerGapBound = -1f;

        private readonly Face[] _a;
        private readonly Face[] _b;
        private readonly FaceAlignment _alignment;
        private readonly float _gapAbove;
        private readonly float _gapUpTo;
        private readonly float _minOverlapRatio;

        public FaceContactScan(Face[] a, Face[] b, FaceAlignment alignment,
            float gapAbove, float gapUpTo, float minOverlapRatio)
        {
            _a = a;
            _b = b;
            _alignment = alignment;
            _gapAbove = gapAbove;
            _gapUpTo = gapUpTo;
            _minOverlapRatio = minOverlapRatio;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private readonly FaceContactScan _scan;
            private int _ia;
            private int _ib;
            private FaceContactHit _current;

            public Enumerator(FaceContactScan scan)
            {
                _scan = scan;
                _ia = 0;
                _ib = -1;
                _current = default;
            }

            public FaceContactHit Current => _current;

            public bool MoveNext()
            {
                while (true)
                {
                    _ib++;
                    if (_ib >= Face.BoxFaceCount)
                    {
                        _ib = 0;
                        _ia++;
                        if (_ia >= Face.BoxFaceCount) return false;
                    }

                    if (TryHit(_ia, _ib, out _current)) return true;
                }
            }

            private bool TryHit(int ia, int ib, out FaceContactHit hit)
            {
                hit = default;
                Face fa = _scan._a[ia];
                Face fb = _scan._b[ib];

                float dot = Vector3.Dot(fa.normal, fb.normal);
                bool aligned = _scan._alignment == FaceAlignment.FacingEachOther
                    ? dot <= -Tolerance.ParallelDot
                    : Tolerance.IsParallel(dot);
                if (!aligned) return false;

                float planeGap = Mathf.Abs(Vector3.Dot(fb.center - fa.center, fa.normal));
                if (planeGap <= _scan._gapAbove || planeGap > _scan._gapUpTo) return false;

                if (!FaceContacts.FacesOverlap(fa, fb, out float area, out float ratio)) return false;
                if (ratio < _scan._minOverlapRatio) return false;

                hit = new FaceContactHit(ia, ib, planeGap, area, ratio);
                return true;
            }
        }
    }
}
