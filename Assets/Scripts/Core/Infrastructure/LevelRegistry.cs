using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class LevelRegistry
    {
        private static readonly List<Level> _items = new();
        private static string _currentId = "";

        public static IReadOnlyList<Level> Items => _items;

        public static Level Current => LevelResolution.ResolveElementLevel(_currentId, Snapshot());

        public static string CurrentId
        {
            get => Current.id;
            set => _currentId = value ?? "";
        }

        public static int CurrentFloorElevationMm => Current.floorElevationMm;

        public static Level LevelOf(KitchenElement? element) =>
            element == null ? Current : LevelResolution.ResolveElementLevel(element.LevelId, Snapshot());

        public static Level[] Snapshot()
        {
            if (_items.Count == 0)
                return new[] { new Level(LevelResolution.DefaultLevelId, LevelResolution.DefaultLevelName, 0, 0) };
            var arr = new Level[_items.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = _items[i];
            return arr;
        }

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

        public static void Reset()
        {
            _items.Clear();
            _currentId = "";
        }
    }
}
