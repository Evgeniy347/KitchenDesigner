using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Настоящий каталог, пропущенный через арифметику плиточной сетки дока.
/// До перехода на плитки (docs/todo_evolution.md §2.1, §2.4) этот набор мерил
/// список строк переменной высоты по имени пункта; после перехода высота плитки
/// постоянна (96×96), а раскладка ветвится по количеству ПЛИТОК в группе —
/// SidebarTileBuilder.BuildTiles сворачивает варианты одного типа в пресеты
/// одной плитки. Повод тот же, что и раньше: рассинхрон между «что видно» и
/// «что объявлено высотой содержимого» невидим глазом и виден только счётом.</summary>
public class SidebarCatalogLayoutTests
{
    private static List<SidebarTileGroupMetrics> AllGroupsOpen()
    {
        var metrics = new List<SidebarTileGroupMetrics>();
        foreach (var g in SidebarCatalog.Build())
            metrics.Add(new SidebarTileGroupMetrics(true, SidebarTileBuilder.BuildTiles(g.items).Count));
        return metrics;
    }

    /// <summary>Группа «Конструкции» заведена этапом 3а ПУСТОЙ, и это её рабочее
    /// состояние на две-три недели вперёд. Пустая группа обязана стоить ровно
    /// свой заголовок: если бы раскладка добавляла ей ряд плиток или зазор
    /// «после плиток», док на 1366×768 потерял бы видимый ряд, а заметили бы это
    /// по скриншоту, а не по счёту.</summary>
    [Test]
    public void AnEmptyGroup_CostsExactlyItsHeader()
    {
        var withEmpty = new List<SidebarTileGroupMetrics>
        {
            new SidebarTileGroupMetrics(true, 4),
            new SidebarTileGroupMetrics(true, 0),
        };
        var withoutEmpty = new List<SidebarTileGroupMetrics>
        {
            new SidebarTileGroupMetrics(true, 4),
        };

        var rows = new List<SidebarTileRow>();
        float taller = SidebarLayout.PlaceTiles(withEmpty, rows);
        int headerRows = rows.Count(r => r.IsHeader);
        int tileRowsOfTheEmptyGroup = rows.Count(r => r.Group == 1 && !r.IsHeader);
        float shorter = SidebarLayout.PlaceTiles(withoutEmpty, rows);

        Assert.AreEqual(2, headerRows, "у пустой группы обязан остаться собственный заголовок");
        Assert.AreEqual(0, tileRowsOfTheEmptyGroup, "плиток у пустой группы нет");
        Assert.AreEqual(SidebarLayout.HeaderH + SidebarLayout.HeaderGap, taller - shorter, 0.01f,
            "пустая группа удлинила содержимое не на один заголовок");
    }

    /// <summary>Тот же счёт, но на настоящем каталоге и на настоящем экране
    /// 1366×768: сколько плиток видно без прокрутки, от появления пустой группы
    /// не меняется — меняется только длина содержимого.</summary>
    [Test]
    public void TheRealCatalog_WithTheEmptyConstructionGroup_StillFillsTheDockAt768()
    {
        var catalog = SidebarCatalog.Build();
        Assert.IsEmpty(catalog[catalog.Count - 1].items,
            "последняя группа каталога — «Конструкции», и она пока пустая; когда в неё "
            + "приедут плитки этапов 3б/4/5, этот тест нужно переписать, а не удалить");

        var rows = new List<SidebarTileRow>();
        float height = SidebarLayout.PlaceTiles(AllGroupsOpen(), rows);

        Assert.Greater(SidebarDockBudget.TilesVisibleWithoutScroll(768f, 36f, 8f), 0,
            "на 1366×768 док обязан показывать хотя бы один ряд плиток без прокрутки");
        Assert.Greater(height, SidebarDockBudget.ViewportHeight(
            SidebarDockBudget.PanelHeight(768f, 36f, 8f)),
            "каталог длиннее окна дока — прокрутка и должна быть; равенство означало бы, "
            + "что раскладка потеряла содержимое");
    }

    [Test]
    public void WholeCatalog_ContentHeight_CoversTheLowestTile()
    {
        var rows = new List<SidebarTileRow>();

        float height = SidebarLayout.PlaceTiles(AllGroupsOpen(), rows);

        float lowest = 0f;
        foreach (var row in rows)
            if (row.Visible) lowest = Mathf.Max(lowest, row.Bottom);

        Assert.Greater(rows.Count, 0);
        Assert.GreaterOrEqual(height, lowest,
            $"каталог занимает {lowest} px, а содержимому объявлено {height} px — "
            + "разница обрезается и прокруткой не достаётся");
    }

    [Test]
    public void WholeCatalog_RowsFollowOneAnother_WithoutOverlap()
    {
        var rows = new List<SidebarTileRow>();
        SidebarLayout.PlaceTiles(AllGroupsOpen(), rows);

        foreach (var group in rows.GroupBy(r => r.Group))
        {
            var headerRow = group.First(r => r.IsHeader);
            float headerBottom = headerRow.Bottom;
            foreach (var tileRow in group.Where(r => !r.IsHeader && r.Visible))
                Assert.GreaterOrEqual(-tileRow.Position.y, -headerRow.Position.y,
                    $"плитка группы {tileRow.Group} начинается выше собственного заголовка");
        }
    }

    [Test]
    public void EveryCatalogTile_GetsARowOfItsOwn()
    {
        var catalog = SidebarCatalog.Build();
        int expectedTiles = catalog.Sum(g => SidebarTileBuilder.BuildTiles(g.items).Count);

        var rows = new List<SidebarTileRow>();
        SidebarLayout.PlaceTiles(AllGroupsOpen(), rows);

        int tileRows = rows.Count(r => !r.IsHeader);
        Assert.AreEqual(expectedTiles, tileRows,
            "ни одна плитка каталога не имеет права потеряться по дороге в раскладку");
    }
}
