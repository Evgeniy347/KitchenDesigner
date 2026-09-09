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
