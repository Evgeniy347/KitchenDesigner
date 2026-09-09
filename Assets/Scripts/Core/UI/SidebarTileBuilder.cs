using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarTileBuilder
    {
        public struct Tile
        {
            public string title;
            public List<SidebarCatalog.Item> presets;
        }

        public static List<Tile> BuildTiles(List<SidebarCatalog.Item> items)
        {
            var keys = items.Select(i => (int)i.kind).ToList();
            var groups = SidebarTileGrouping.GroupPreservingOrder(keys);

            var tiles = new List<Tile>(groups.Count);
            foreach (var indices in groups)
            {
                var presets = indices.Select(i => items[i]).ToList();
                tiles.Add(new Tile { title = presets[0].tileTitle, presets = presets });
            }
            return tiles;
        }
    }
}
