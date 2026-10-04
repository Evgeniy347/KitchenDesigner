using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class SidebarDockBudgetTests
{
    private const float TopOffsetUnderToolbar = 48f;
    private const float BottomMargin = 42f;
    private const float ScreenWidth = 1366f;
    private const float ScreenHeight = 768f;

    private static float CanvasHeightOfTheBaseLaptop() =>
        UiScale.CanvasHeight(ScreenHeight, UiScale.Automatic(ScreenWidth, ScreenHeight));

    [Test]
    public void At1366x768_AtLeastTenTilesFitInAnOpenGroupWithoutScrolling()
    {
        float canvasHeight = CanvasHeightOfTheBaseLaptop();
        float panelHeight = canvasHeight - TopOffsetUnderToolbar - BottomMargin;
        float viewportHeight = panelHeight - SidebarLayout.TopStripH;
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
            SidebarDockBudget.TilesVisibleWithoutScroll(canvasHeight, TopOffsetUnderToolbar, BottomMargin),
            "SidebarDockBudget обязан считать столько же строк, сколько ручной перебор рядов");
        Assert.GreaterOrEqual(tilesWithoutScroll, 10,
            "на базовом экране 1366×768 (канва 1681×945: пол масштаба D1) раскрытая группа "
            + "обязана показывать не меньше 10 плиток без прокрутки (docs/todo_evolution.md §2.4); "
            + "плитки макета 122×120 считаются в единицах канвы, а не в физических пикселях");
    }

    [Test]
    public void DockWidth_IsTheMockupWidth_AndTwoTilesFillItEdgeToEdge()
    {
        Assert.AreEqual(268f, SidebarLayout.DockW,
            "раскрытый док — 268 px по макету shell.png: шире — залезает на сцену");
        Assert.AreEqual(SidebarLayout.DockW,
            2f * SidebarLayout.Pad + SidebarLayout.GridColumns * SidebarLayout.TileW
            + (SidebarLayout.GridColumns - 1) * SidebarLayout.TileGap, 0.01f,
            "две плитки с зазором и полями обязаны занимать док целиком: «справа 30 px пустоты» — дефект №6 аудита");
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
