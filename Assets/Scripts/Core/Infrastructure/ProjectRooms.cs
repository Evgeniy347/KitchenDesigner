using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class ProjectRooms
    {
        private static readonly List<RoomData> _items = new();
        public static IReadOnlyList<RoomData> Items => _items;

        public static void Set(IEnumerable<RoomData>? rooms)
        {
            _items.Clear();
            if (rooms == null) return;
            foreach (var room in rooms) if (room != null) _items.Add(room);
        }

        public static void Reset() => _items.Clear();
    }
}
