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
}
