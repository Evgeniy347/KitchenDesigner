using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarTileGrouping
    {
        public static List<List<int>> GroupPreservingOrder(IReadOnlyList<int> keys)
        {
            var tiles = new List<List<int>>();
            var tileOfKey = new Dictionary<int, int>();

            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                if (!tileOfKey.TryGetValue(key, out int tileIndex))
                {
                    tileIndex = tiles.Count;
                    tiles.Add(new List<int>());
                    tileOfKey[key] = tileIndex;
                }
                tiles[tileIndex].Add(i);
            }

            return tiles;
        }
    }
}
