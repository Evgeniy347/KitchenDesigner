using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class PerfHudTests
{
    private const string SomeHudText = "dt 16.7ms";
    private const float SomeHudHeight = 120f;

    [Test]
    public void DefaultToolbarBottomY_MatchesTheRealToolbarHeight()
    {
        Assert.AreEqual(ToolbarUI.BarHeight, PerfHud.DefaultToolbarBottomY,
            "запасное значение в Diagnostics не имеет права разъехаться с настоящей высотой "
            + "тулбара — иначе оверлей до первого кадра UIManager.Awake рисуется по чужому числу. "
            + "Слою Diagnostics ссылаться на UI напрямую нельзя (LayerDependencyDirectionTests), "
            + "поэтому число продублировано, а этот тест держит дубликат в узде");
    }

    [Test]
    public void ShouldPaint_OnRepaint_WhenTheMonitorIsOnAndHasText()
    {
        Assert.IsTrue(PerfHud.ShouldPaint(EventType.Repaint, true, SomeHudText));
    }

    [Test]
    public void ShouldPaint_IsFalseOnEveryEventExceptRepaint()
    {
        Assert.IsFalse(PerfHud.ShouldPaint(EventType.Layout, true, SomeHudText),
            "IMGUI зовёт OnGUI несколько раз за кадр (Layout, события ввода, Repaint); "
            + "рисовать на каждом — значит умножить стоимость оверлея в том самом кадре, "
            + "который он измеряет");
        Assert.IsFalse(PerfHud.ShouldPaint(EventType.MouseMove, true, SomeHudText));
        Assert.IsFalse(PerfHud.ShouldPaint(EventType.KeyDown, true, SomeHudText));
    }

    [Test]
    public void ShouldPaint_IsFalseWhileTheMonitorIsOff()
    {
        Assert.IsFalse(PerfHud.ShouldPaint(EventType.Repaint, false, SomeHudText),
            "замер выключен по умолчанию, и оверлея быть не должно");
    }

    [Test]
    public void ShouldPaint_IsFalseUntilTheMonitorHasBuiltItsFirstText()
    {
        Assert.IsFalse(PerfHud.ShouldPaint(EventType.Repaint, true, ""),
            "пустой оверлей — это чёрный прямоугольник поверх сцены");
    }

    [Test]
    public void TheHud_AsksTheOsForAMonospaceFont_BecauseItsColumnsAreAlignedWithSpaces()
    {
        var font = Font.CreateDynamicFontFromOSFont(PerfHud.MonospaceFontName, 13);

        Assert.IsNotNull(font,
            "строки HUD выравниваются PadRight/PadLeft (см. PerfMonitor.MarkerLine); "
            + "на пропорциональном шрифте такие колонки плывут, и HUD становится "
            + "нечитаемым ровно тогда, когда в него смотрят");
        if (font != null) Object.DestroyImmediate(font);
    }

    [TestCase(1920f)]
    [TestCase(1366f)]
    public void ComputeRect_AnchorsToTheRightEdge_BelowTheToolbar(float screenWidth)
    {
        var rect = PerfHud.ComputeRect(screenWidth, ToolbarUI.BarHeight, SomeHudHeight);

        Assert.GreaterOrEqual(rect.y, ToolbarUI.BarHeight,
            "оверлей обязан начинаться НИЖЕ тулбара, а не перекрывать его сверху");
        Assert.Greater(rect.x, screenWidth / 2f,
            "оверлей обязан висеть у правого края экрана, а не слева, где докнута левая панель");
        Assert.LessOrEqual(rect.x + rect.width, screenWidth,
            "оверлей не имеет права вылезать за правый край экрана");
        Assert.GreaterOrEqual(rect.x, 0f);
    }

    [TestCase(1920f)]
    [TestCase(1366f)]
    public void ComputeRect_NeverOverlapsTheToolbarRect(float screenWidth)
    {
        var toolbarRect = new Rect(0, 0, screenWidth, ToolbarUI.BarHeight);
        var rect = PerfHud.ComputeRect(screenWidth, ToolbarUI.BarHeight, SomeHudHeight);

        Assert.IsFalse(rect.Overlaps(toolbarRect),
            "F9-оверлей обязан оставаться целиком под тулбаром, а не наезжать на него верхним краем");
    }

    [Test]
    public void ComputeRect_StaysOnScreen_AtANarrowWindow()
    {
        const float narrowScreenWidth = 480f;
        var rect = PerfHud.ComputeRect(narrowScreenWidth, ToolbarUI.BarHeight, SomeHudHeight);

        Assert.GreaterOrEqual(rect.x, 0f,
            "даже когда оверлей шире экрана за вычетом отступов, левый край обязан остаться на экране");
        Assert.LessOrEqual(rect.x + rect.width, narrowScreenWidth,
            "и правый край тоже — оверлей не имеет права вылезать за пределы узкого окна");
    }

    [Test]
    public void PerfHud_IsCompiledUnconditionally_SoTheOverlayExistsInAReleaseBuildToo()
    {
        var path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Core", "Diagnostics", "PerfHud.cs");
        Assert.IsTrue(System.IO.File.Exists(path), "скан не видит файла — проверять было бы нечего");

        var source = System.IO.File.ReadAllText(path);
        StringAssert.DoesNotContain("#if", source,
            "PerfMonitor рисует HUD через этот компонент; спрятанный под #if "
            + "UNITY_EDITOR/DEVELOPMENT_BUILD оверлей выпал бы из обычной сборки "
            + "даже если сам PerfMonitor остался — окно снова не появилось бы");
    }

    [TestCase(1920f, 1500f, 1492f)]
    [TestCase(1920f, 1920f, 1912f)]
    public void ComputeRect_StopsLeftOfAnOpenRightDock(float screenWidth, float dockLeft, float expectedRight)
    {
        var rect = PerfHud.ComputeRect(screenWidth, ToolbarUI.BarHeight, SomeHudHeight, dockLeft);

        Assert.AreEqual(expectedRight, rect.xMax, 0.01f,
            "правый край HUD отступает от левой кромки открытого дока «Сцены» на поле, а не лежит поверх него");
    }

    [Test]
    public void ComputeRect_ShrinksButStaysReadable_WhenTheDockLeavesLittleRoom()
    {
        var rect = PerfHud.ComputeRect(1920f, ToolbarUI.BarHeight, SomeHudHeight, 300f);

        Assert.GreaterOrEqual(rect.x, 0f);
        Assert.LessOrEqual(rect.xMax, 300f, "HUD целиком левее дока");
    }

    [Test]
    public void Install_PutsThePaletteTokensInTheHud_AndTheHudKeepsTheMonoFontSize()
    {
        var previous = PerfHud.Style;
        try
        {
            PerfHudBinding.Install();

            var style = PerfHud.Style!;
            Assert.AreEqual(UIStyle.HudPanel, style.Panel, "фон HUD — токен HudPanel (Panel, альфа 0,88)");
            Assert.AreEqual(0.88f, UIStyle.HudPanel.a, 1e-4f);
            Assert.AreEqual(UIStyle.Panel.r, UIStyle.HudPanel.r, 1e-4f, "тот же цвет, что у окон: контраст Text/Panel уже под сторожем");
            Assert.AreEqual(UIStyle.Text, style.ColorOf(PerfHudLineKind.Normal));
            Assert.AreEqual(UIStyle.TextSecondary, style.ColorOf(PerfHudLineKind.Secondary), "подпись/подсказка — вторичный текст");
            Assert.AreEqual(UIStyle.TextWarning, style.ColorOf(PerfHudLineKind.Warning), "строка выше порога — TextWarning");
            Assert.AreEqual(UIStyle.FontMono, style.FontSize);
            Assert.AreEqual(UIStyle.Space3, style.PadX, "паддинг 12 по горизонтали (D2)");
            Assert.AreEqual(UIStyle.Space2, style.PadY, "и 8 по вертикали");
        }
        finally
        {
            PerfHud.Style = previous;
            PerfHud.RightLimit = null;
        }
    }

    [TestCase(1500f, 1900f, 1920f, 1500f)]
    [TestCase(40f, 340f, 1920f, float.MaxValue)]
    public void DockLimit_AppliesOnlyToADockOnTheRightHalf(float left, float right, float screenWidth, float expected)
    {
        Assert.AreEqual(expected, PerfHudBinding.DockLimit(left, right, screenWidth),
            "окно «Сцены», утащенное на левую половину, F9 не закрывает — отступать не от чего");
    }

    [Test]
    public void ClosedDock_LeavesTheHudAtTheScreenEdge()
    {
        Assume.That(HierarchyPanelUI.Instance == null || !HierarchyPanelUI.Instance.IsVisible,
            "предусловие: ни одна «Сцена» из соседних тестов не открыта");
        Assert.AreEqual(float.MaxValue, PerfHudBinding.RightDockLeftEdge(),
            "без открытой «Сцены» ограничения нет: HUD прижат к правому краю, как раньше");
    }
}
