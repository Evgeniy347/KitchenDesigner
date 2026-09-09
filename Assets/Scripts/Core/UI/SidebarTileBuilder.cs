using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarTileBuilder
    {
        private static readonly Dictionary<SidebarItemKind, string> TitleOverrides =
            new Dictionary<SidebarItemKind, string>
            {
                { SidebarItemKind.PipeFitting, "Фитинг" },
            };

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
                tiles.Add(new Tile { title = TitleFor(presets), presets = presets });
            }
            return tiles;
        }

        private static string TitleFor(List<SidebarCatalog.Item> presets)
        {
            if (TitleOverrides.TryGetValue(presets[0].kind, out var overrideTitle)) return overrideTitle;
            return presets.Count == 1 ? presets[0].DisplayName : CommonLeadingWords(presets);
        }

        private static string CommonLeadingWords(List<SidebarCatalog.Item> presets)
        {
            var firstWords = presets[0].DisplayName.Split(' ');
            int common = firstWords.Length;
            foreach (var preset in presets)
            {
                var words = preset.DisplayName.Split(' ');
                int i = 0;
                while (i < common && i < words.Length && words[i] == firstWords[i]) i++;
                common = i;
            }
            return common > 0 ? string.Join(" ", firstWords.Take(common)) : presets[0].DisplayName;
        }
    }
}
