using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class SidebarDockBudgetTests
{
    private const float TopOffsetUnderToolbar = 52f;
    private const float BottomMargin = 42f;

    [Test]
    public void At1366x768_AtLeastTenTilesFitInAnOpenGroupWithoutScrolling()
    {
        float panelHeight = 768f - TopOffsetUnderToolbar - BottomMargin;
        float viewportHeight = panelHeight - SidebarLayout.TopStripH - SidebarLayout.SearchBandH;
        float tilesArea = viewportHeight - SidebarLayout.HeaderH - SidebarLayout.HeaderGap;

        int rows = 0;
        float used = 0f;
        while (used + SidebarLayout.TileH <= tilesArea)
        {
            rows++;
            used += SidebarLayout.TileH;
            if (used + SidebarLayout.TileGap + SidebarLayout.TileH <= tilesArea)
                used += SidebarLayout.TileGap;
            else
                break;
        }
        int tilesWithoutScroll = rows * SidebarLayout.GridColumns;

        Assert.AreEqual(tilesWithoutScroll,
            SidebarDockBudget.TilesVisibleWithoutScroll(768f, TopOffsetUnderToolbar, BottomMargin),
            "SidebarDockBudget обязан считать столько же строк, сколько ручной перебор рядов");
        Assert.GreaterOrEqual(tilesWithoutScroll, 10,
            "на базовом экране 1366×768 раскрытая группа обязана показывать не меньше 10 "
            + "плиток без прокрутки (docs/todo_evolution.md §2.4)");
    }

    [Test]
    public void DockWidth_DoesNotExceedTwoHundredSixtyPixels()
    {
        Assert.AreEqual(260f, SidebarLayout.DockW,
            "раскрытый док обязан оставаться шириной 260 px — не залезать на сцену больше");
    }

    [Test]
    public void AutoCollapsesAt_Is800PixelsOrLess()
    {
        Assert.IsTrue(SidebarDockBudget.AutoCollapsesAt(768f));
        Assert.IsTrue(SidebarDockBudget.AutoCollapsesAt(800f));
        Assert.IsFalse(SidebarDockBudget.AutoCollapsesAt(801f));
        Assert.IsFalse(SidebarDockBudget.AutoCollapsesAt(1080f));
    }
}
