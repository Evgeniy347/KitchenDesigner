using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Measure
{
    public static class MeasureStore
    {
        private static readonly List<MeasureSegment> _segments = new List<MeasureSegment>();

        public static IReadOnlyList<MeasureSegment> Segments => _segments;

        public static MeasureSegment? Selected { get; private set; }

        public static event Action? Changed;

        public static void Add(MeasureSegment segment)
        {
            if (segment == null) return;
            _segments.Add(segment);
            Changed?.Invoke();
        }

        public static void Remove(MeasureSegment segment)
        {
            if (segment == null) return;
            if (!_segments.Remove(segment)) return;
            if (Selected == segment) Selected = null;
            Changed?.Invoke();
        }

        public static void Select(MeasureSegment? segment)
        {
            if (Selected == segment) return;
            Selected = segment;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (_segments.Count == 0 && Selected == null) return;
            _segments.Clear();
            Selected = null;
            Changed?.Invoke();
        }
    }
}
