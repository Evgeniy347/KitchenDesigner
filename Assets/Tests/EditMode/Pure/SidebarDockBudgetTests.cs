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

    [Test]
    public void CollapsesAfterSpawn_Unset_FollowsScreenHeight_Like768()
    {
        Assert.IsTrue(SidebarDockBudget.CollapsesAfterSpawn(SidebarDockChoice.Unset, 768f),
            "без выбора пользователя низкий экран сворачивает док в рейку после установки — "
            + "прежнее поведение SidebarDockBudget.AutoCollapsesAt не должно измениться");
    }

    [Test]
    public void CollapsesAfterSpawn_Unset_FollowsScreenHeight_Like1080()
    {
        Assert.IsFalse(SidebarDockBudget.CollapsesAfterSpawn(SidebarDockChoice.Unset, 1080f),
            "без выбора пользователя высокий экран оставляет док раскрытым после установки");
    }

    [Test]
    public void CollapsesAfterSpawn_Docked_NeverCollapses_OnAShortScreen()
    {
        Assert.IsFalse(SidebarDockBudget.CollapsesAfterSpawn(SidebarDockChoice.Docked, 768f),
            "пользователь выбрал раскрытый док — низкий экран, который раньше сворачивал бы "
            + "панель, больше не решает");
    }

    [Test]
    public void CollapsesAfterSpawn_Docked_NeverCollapses_OnATallScreen()
    {
        Assert.IsFalse(SidebarDockBudget.CollapsesAfterSpawn(SidebarDockChoice.Docked, 1080f),
            "раскрытый док остаётся раскрытым и на большом экране — это ожидаемо, но обязано "
            + "оставаться верным ПОСЛЕ выбора, а не только по умолчанию");
    }

    [Test]
    public void CollapsesAfterSpawn_Rail_AlwaysCollapses_OnATallScreen()
    {
        Assert.IsTrue(SidebarDockBudget.CollapsesAfterSpawn(SidebarDockChoice.Rail, 1080f),
            "пользователь выбрал рейку иконок — большой экран, который раньше оставлял бы "
            + "панель раскрытой, больше не решает");
    }

    [Test]
    public void CollapsesAfterSpawn_Rail_AlwaysCollapses_OnAShortScreen()
    {
        Assert.IsTrue(SidebarDockBudget.CollapsesAfterSpawn(SidebarDockChoice.Rail, 768f),
            "рейка сворачивается и на маленьком экране — тот же результат, что и по умолчанию, "
            + "но теперь он идёт от явного выбора, а не от высоты");
    }
}
