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

        public static void Add(Level level)
        {
            if (level != null) _items.Add(level);
        }

        public static void Insert(int index, Level level)
        {
            if (level == null) return;
            if (index < 0) index = 0;
            if (index > _items.Count) index = _items.Count;
            _items.Insert(index, level);
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null && _items[i].id == id) return i;
            return -1;
        }

        public static void Remove(string id)
        {
            int i = IndexOf(id);
            if (i >= 0) _items.RemoveAt(i);
        }

        public static void Reset() => _items.Clear();
    }
}
