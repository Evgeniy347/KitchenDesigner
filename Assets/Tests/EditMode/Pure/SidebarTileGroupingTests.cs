using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class SidebarTileGroupingTests
{
    [Test]
    public void EveryItem_EndsUpInExactlyOneTile()
    {
        var keys = new List<int> { 1, 2, 3, 2, 1, 4, 2 };

        var tiles = SidebarTileGrouping.GroupPreservingOrder(keys);

        var seen = new HashSet<int>();
        foreach (var tile in tiles)
            foreach (var index in tile)
                Assert.IsTrue(seen.Add(index), $"индекс {index} попал больше чем в одну плитку");

        Assert.AreEqual(keys.Count, seen.Count, "ни один пункт каталога не имеет права потеряться");
    }

    [Test]
    public void ItemsSharingAKey_CollapseIntoOneTileAsPresets()
    {
        var keys = new List<int> { 10, 20, 10, 10, 30 };

        var tiles = SidebarTileGrouping.GroupPreservingOrder(keys);

        Assert.AreEqual(3, tiles.Count, "три разных типа — три плитки");
        Assert.AreEqual(new[] { 0, 2, 3 }, tiles[0].ToArray(),
            "все пункты с одинаковым типом становятся пресетами одной и той же плитки, "
            + "в порядке появления в каталоге");
    }

    [Test]
    public void TileOrder_FollowsFirstAppearanceInTheCatalog()
    {
        var keys = new List<int> { 5, 1, 5, 1, 9 };

        var tiles = SidebarTileGrouping.GroupPreservingOrder(keys);

        Assert.AreEqual(3, tiles.Count);
        Assert.AreEqual(0, tiles[0][0], "первая плитка — тип, впервые встреченный первым");
        Assert.AreEqual(1, tiles[1][0]);
        Assert.AreEqual(4, tiles[2][0]);
    }

    [Test]
    public void EmptyCatalog_ProducesNoTiles()
    {
        Assert.IsEmpty(SidebarTileGrouping.GroupPreservingOrder(new List<int>()));
    }
}
