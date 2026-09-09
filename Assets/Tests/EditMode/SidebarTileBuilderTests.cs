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

    /// <summary>До этой правки заголовок плитки с несколькими пресетами
    /// вычислялся эвристикой (общие первые слова имён пресетов), а для
    /// фитингов, у которых общих слов нет вовсе («Колено», «Муфта», «Тройник»…),
    /// был отдельный ручной словарь `TitleOverrides`. Оба механизма удалены:
    /// заголовок теперь идёт явной строкой из <see cref="SidebarCatalogRow.TypeRow"/>
    /// и виден на <see cref="SidebarCatalog.Item"/> как <c>tileTitle</c> — тест
    /// проверяет именно эту дорогу, а не то, что имена пресетов случайно
    /// совпадают.</summary>
    [Test]
    public void FittingTile_TitleIsExplicit_NotAGuessFromUnrelatedPresetNames()
    {
        var sanitary = SidebarCatalog.Build().First(g => g.title == "Сантехника");
        var tiles = SidebarTileBuilder.BuildTiles(sanitary.items);

        var fittingTile = tiles.First(t => t.presets[0].kind == SidebarItemKind.PipeFitting);

        Assert.AreEqual(6, fittingTile.presets.Count);
        Assert.AreEqual("Фитинг", fittingTile.title,
            "шесть фитингов (колено, муфта, тройник, заглушка, подача, обратка) не делят "
            + "ни одного общего слова в имени — заголовок обязан идти явной строкой из "
            + "таблицы каталога, а не подбираться по именам пресетов");
    }

    /// <summary>Однопресетная плитка тоже берёт заголовок из таблицы, а не из
    /// собственного имени пресета — само по себе имя пресета плитку не называет,
    /// даже когда оно с ним совпадает.</summary>
    [Test]
    public void SinglePresetTile_TitleComesFromTheTable()
    {
        var board = SidebarCatalog.Build().First(g => g.title == "Детали");
        var tiles = SidebarTileBuilder.BuildTiles(board.items);

        var shelf = tiles.First(t => t.presets[0].kind == SidebarItemKind.Board);
        Assert.AreEqual(1, shelf.presets.Count);
        Assert.AreEqual("Полка", shelf.title);
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
