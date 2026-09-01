#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PerfHudTests
{
    private const string SomeHudText = "dt 16.7ms";

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
}

#endif
