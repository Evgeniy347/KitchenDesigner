using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class FrozenValidation
    {
        private static readonly System.Comparison<(int lo, int hi)> NestedLoopOrder =
            (p, q) => p.lo != q.lo ? p.lo.CompareTo(q.lo) : p.hi.CompareTo(q.hi);

        private readonly CoreValidationResult _static = new CoreValidationResult();
        private readonly OverlapMarks _staticMarks = new OverlapMarks();
        private readonly OverlapMarks _liveMarks = new OverlapMarks();
        private readonly List<(int lo, int hi)> _livePairs = new List<(int lo, int hi)>();
        private readonly List<int> _movers = new List<int>();
        private StaticContactIndex _index = null!;
        private bool[] _isMover = System.Array.Empty<bool>();

        public int Count { get; private set; }

        public IReadOnlyList<int> Movers => _movers;

        public int PairsInLastPass { get; private set; }

        public bool IsMover(int index) => index >= 0 && index < _isMover.Length && _isMover[index];

        public static FrozenValidation? Freeze(IReadOnlyList<ValidationElement> all,
            IReadOnlyList<int> movers)
        {
            if (all == null || all.Count == 0) return null;
            if (!ValidationCore.HasAnchor(all)) return null;

            var frozen = new FrozenValidation { Count = all.Count };
            frozen._isMover = new bool[all.Count];
            for (int i = 0; i < movers.Count; i++)
            {
                int m = movers[i];
                if (m < 0 || m >= all.Count || frozen._isMover[m]) continue;
                frozen._isMover[m] = true;
                frozen._movers.Add(m);
            }

            float contactDist = ValidationCore.ContactDistUnits;
            ValidationBroadPhase.Clear();
            var candidates = ValidationBroadPhase.CandidatePairsInNestedLoopOrder(all, contactDist);

            var staticPairs = new List<(int lo, int hi)>(candidates.Count);
            for (int c = 0; c < candidates.Count; c++)
            {
                var pair = candidates[c];
                if (frozen._isMover[pair.lo] || frozen._isMover[pair.hi]) continue;
                staticPairs.Add(pair);
            }

            ValidationCore.ProcessPairs(all, staticPairs, frozen._static, frozen._staticMarks);
            frozen._index = StaticContactIndex.Over(all, frozen._isMover, contactDist);
            return frozen;
        }

        public void Revalidate(IReadOnlyList<ValidationElement> all, CoreValidationResult result)
        {
            result.Clear();
            result.Contacts.AddRange(_static.Contacts);
            if (_static.Diagnostics != null)
                for (int i = 0; i < _static.Diagnostics.Count; i++)
                {
                    var d = _static.Diagnostics[i];
                    result.AddDiagnostic(d.Element, d.Other, d.Kind);
                }
            _liveMarks.CopyFrom(_staticMarks);

            float contactDist = ValidationCore.ContactDistUnits;
            _livePairs.Clear();
            for (int i = 0; i < _movers.Count; i++)
            {
                int mover = _movers[i];
                var reaching = _index.Reaching(all, mover, contactDist);
                for (int k = 0; k < reaching.Count; k++) AddPair(mover, reaching[k]);

                for (int j = i + 1; j < _movers.Count; j++)
                    if (ValidationBroadPhase.SolidsReach(all[mover], all[_movers[j]], contactDist))
                        AddPair(mover, _movers[j]);
            }
            _livePairs.Sort(NestedLoopOrder);

            ValidationCore.ProcessPairs(all, _livePairs, result, _liveMarks);
            ValidationCore.Finish(all, result, _liveMarks);
            PairsInLastPass = _livePairs.Count;
        }

        private void AddPair(int a, int b) =>
            _livePairs.Add(a < b ? (a, b) : (b, a));
    }
}
