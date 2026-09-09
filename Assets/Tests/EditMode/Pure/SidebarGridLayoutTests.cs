using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class SidebarGridLayoutTests
{
    private static readonly List<SidebarTileRow> Rows = new List<SidebarTileRow>();

    private static SidebarTileGroupMetrics Group(bool open, int tileCount)
        => new SidebarTileGroupMetrics(open, tileCount);

    private static float LowestRowBottom(List<SidebarTileRow> rows)
    {
        float lowest = 0f;
        foreach (var row in rows)
            if (row.Visible && row.Bottom > lowest)
                lowest = row.Bottom;
        return lowest;
    }

    [Test]
    public void PlaceTiles_EmptyCatalog_IsJustTheTopPadding()
    {
        float h = SidebarLayout.PlaceTiles(new List<SidebarTileGroupMetrics>(), Rows);

        Assert.AreEqual(SidebarLayout.Pad, h);
        Assert.IsEmpty(Rows);
    }

    [Test]
    public void PlaceTiles_PutsTilesInTwoColumns()
    {
        SidebarLayout.PlaceTiles(new List<SidebarTileGroupMetrics> { Group(true, 3) }, Rows);

        Assert.AreEqual(4, Rows.Count);
        var t0 = Rows[1];
        var t1 = Rows[2];
        var t2 = Rows[3];

        Assert.AreEqual(SidebarLayout.Pad, t0.Position.x, "первая плитка стоит в левой колонке");
        Assert.AreEqual(SidebarLayout.Pad + SidebarLayout.TileW + SidebarLayout.TileGap, t1.Position.x,
            "вторая плитка стоит в правой колонке, отступив на ширину плитки и зазор");
        Assert.AreEqual(t0.Position.y, t1.Position.y,
            "первая и вторая плитка стоят в одном ряду");
        Assert.AreEqual(SidebarLayout.Pad, t2.Position.x,
            "третья плитка переносится в левую колонку следующего ряда");
        Assert.AreEqual(t0.Position.y - (SidebarLayout.TileH + SidebarLayout.TileGap), t2.Position.y,
            "следующий ряд отступает на высоту плитки плюс зазор — не на константу");
    }

    [Test]
    public void PlaceTiles_ClosedGroup_HidesItsTilesAndCollapsesToTheHeaderAlone()
    {
        var groups = new List<SidebarTileGroupMetrics> { Group(false, 4), Group(true, 1) };

        SidebarLayout.PlaceTiles(groups, Rows);

        Assert.IsFalse(Rows[1].Visible);
        Assert.IsFalse(Rows[2].Visible);
        Assert.IsFalse(Rows[3].Visible);
        Assert.IsFalse(Rows[4].Visible);
        Assert.AreEqual(-(SidebarLayout.Pad + SidebarLayout.HeaderH + SidebarLayout.HeaderGap),
            Rows[5].Position.y, "свёрнутая группа не оставляет за собой пустоты");
    }

    [Test]
    public void PlaceTiles_ContentHeight_ReachesBelowTheLowestTile()
    {
        var groups = new List<SidebarTileGroupMetrics>();
        for (int g = 0; g < 7; g++) groups.Add(Group(true, 5));

        float h = SidebarLayout.PlaceTiles(groups, Rows);

        Assert.GreaterOrEqual(h, LowestRowBottom(Rows),
            "нижний край последней плитки обязан быть внутри содержимого: "
            + "то, что вылезло за него, прокруткой не достать");
    }

    [Test]
    public void GridContentHeight_OddTileCount_StillReservesAFullLastRow()
    {
        float threeTiles = SidebarLayout.GridContentHeight(3);
        float twoTiles = SidebarLayout.GridContentHeight(2);

        Assert.Greater(threeTiles, twoTiles,
            "третья плитка переносит вторую строку — высота обязана вырасти");
        Assert.AreEqual(2 * SidebarLayout.TileH + SidebarLayout.TileGap, threeTiles);
    }

    [Test]
    public void VisibleTileRows_CountsOnlyFullyFittingRows()
    {
        float exactlyTwoRows = 2 * SidebarLayout.TileH + SidebarLayout.TileGap;

        Assert.AreEqual(2, SidebarLayout.VisibleTileRows(exactlyTwoRows));
        Assert.AreEqual(1, SidebarLayout.VisibleTileRows(exactlyTwoRows - 1f),
            "ряду не хватает одного пикселя — считать его целиком видимым нельзя");
    }
}
