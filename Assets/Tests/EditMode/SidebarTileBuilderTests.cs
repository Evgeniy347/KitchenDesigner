using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>Раскладка каталога на плитки-типы с пресетами (docs/todo_evolution.md §2.1,
/// §2.4): читает только SidebarCatalog и держит переписанное обещание «ничто из
/// достижимого сегодня не пропадёт».</summary>
public class SidebarTileBuilderTests
{
    [Test]
    public void EveryCatalogItem_IsReachableAsATileOrAPreset_ExactlyOnce()
    {
        var catalog = SidebarCatalog.Build();
        var reachable = new HashSet<string>();

        foreach (var g in catalog)
            foreach (var tile in SidebarTileBuilder.BuildTiles(g.items))
                foreach (var preset in tile.presets)
                    Assert.IsTrue(reachable.Add(g.title + "/" + preset.name),
                        $"пункт «{preset.name}» из группы «{g.title}» задваивается между плитками");

        int expected = catalog.Sum(g => g.items.Count);
        Assert.AreEqual(expected, reachable.Count,
            "каждый пункт каталога обязан остаться доступным как плитка или как пресет "
            + "внутри плитки — ни один не имеет права потеряться при переходе на плитки");
    }

    [Test]
    public void TilesOnTheFirstLevel_AreFewerThanFlatCatalogItems()
    {
        var catalog = SidebarCatalog.Build();
        int totalItems = catalog.Sum(g => g.items.Count);
        int totalTiles = catalog.Sum(g => SidebarTileBuilder.BuildTiles(g.items).Count);

        Assert.Less(totalTiles, totalItems,
            "плитки первого уровня обязаны показывать ТИПЫ, а не варианты — иначе они "
            + "унаследуют те же кнопки, ради ухода от которых всё затевалось");
    }

    [Test]
    public void ItemsSharingTheSameKind_BecomePresetsOfOneTile()
    {
        var sanitary = SidebarCatalog.Build().First(g => g.title == "Сантехника");
        var tiles = SidebarTileBuilder.BuildTiles(sanitary.items);

        var fittingTile = tiles.First(t => t.presets[0].kind == SidebarItemKind.PipeFitting);
        Assert.AreEqual(6, fittingTile.presets.Count,
            "шесть фитингов одной трубы обязаны стать пресетами ОДНОЙ плитки");
    }

    [Test]
    public void MultiPresetTile_PresetsAllShareTheSameCategory()
    {
        foreach (var g in SidebarCatalog.Build())
            foreach (var tile in SidebarTileBuilder.BuildTiles(g.items))
            {
                var first = tile.presets[0].Category;
                foreach (var preset in tile.presets)
                    Assert.AreEqual(first, preset.Category,
                        $"плитка «{tile.title}» смешивает пресеты с разной доступностью по "
                        + "режиму — грайаут одной плитки не может решить, что показать");
            }
    }
}
