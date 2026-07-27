using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class ProjectFloorplans
    {
        private static readonly List<FloorplanScopeData> _items = new();
        public static IReadOnlyList<FloorplanScopeData> Items => _items;
        public static void Set(IEnumerable<FloorplanScopeData>? items)
        {
            _items.Clear();
            if (items == null) return;
            foreach (var item in items) if (item != null) _items.Add(item);
        }
        public static FloorplanScopeData? Find(string id)
        {
            foreach (var item in _items)
                if (string.Equals(item.id, id, System.StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }
        public static void Reset() => _items.Clear();
    }
}
