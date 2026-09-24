using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class LevelRegistry
    {
        private static readonly List<Level> _items = new();

        public static IReadOnlyList<Level> Items => _items;

        public static void Set(IEnumerable<Level>? levels)
        {
            _items.Clear();
            if (levels == null) return;
            foreach (var level in levels) if (level != null) _items.Add(level);
        }

        public static void Reset() => _items.Clear();
    }
}
