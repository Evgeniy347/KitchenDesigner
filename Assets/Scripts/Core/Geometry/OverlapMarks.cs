using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class OverlapMarks
    {
        private readonly List<int> _order = new List<int>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly HashSet<int> _anchorsInIllegalOverlap = new HashSet<int>();

        public IReadOnlyList<int> InOrder => _order;

        public bool AnchorIsInIllegalOverlap(int index) => _anchorsInIllegalOverlap.Contains(index);

        public void Clear()
        {
            _order.Clear();
            _seen.Clear();
            _anchorsInIllegalOverlap.Clear();
        }

        public void CopyFrom(OverlapMarks other)
        {
            Clear();
            for (int i = 0; i < other._order.Count; i++)
            {
                _order.Add(other._order[i]);
                _seen.Add(other._order[i]);
            }
            foreach (int index in other._anchorsInIllegalOverlap)
                _anchorsInIllegalOverlap.Add(index);
        }

        public void Mark(int index)
        {
            if (_seen.Add(index)) _order.Add(index);
        }

        public void MarkAnchorInIllegalOverlap(int index) => _anchorsInIllegalOverlap.Add(index);
    }
}
