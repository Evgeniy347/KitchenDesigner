using System.Collections.Generic;

namespace KitchenDesigner.Core.Measure
{
    public static class DistanceGuideStore
    {
        private static readonly List<GuideLine> _lines = new List<GuideLine>();

        public static IReadOnlyList<GuideLine> Lines => _lines;

        public static bool Showing => _lines.Count > 0;

        public static void Set(IReadOnlyList<GuideLine> lines)
        {
            _lines.Clear();
            if (lines == null) return;
            for (int i = 0; i < lines.Count; i++) _lines.Add(lines[i]);
        }

        public static void Clear() => _lines.Clear();
    }
}
