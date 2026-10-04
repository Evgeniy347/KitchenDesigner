using KitchenDesigner.Core;
using NUnit.Framework;

public class PerfHudLinesTests
{
    [Test]
    public void ForFrame_WarnsOnlyAboveTheSixtyFpsBudget()
    {
        Assert.AreEqual(PerfHudLineKind.Normal, PerfHudLines.ForFrame(PerfHudLines.FrameBudgetMs),
            "кадр ровно в бюджет — не тревога: порог строгий");
        Assert.AreEqual(PerfHudLineKind.Warning, PerfHudLines.ForFrame(PerfHudLines.FrameBudgetMs + 0.1f));
        Assert.AreEqual(PerfHudLineKind.Normal, PerfHudLines.ForFrame(8f));
    }

    [Test]
    public void ForMarker_WarnsWhenOneMarkerEatsAThirdOfTheFrameBudget()
    {
        Assert.AreEqual(PerfHudLineKind.Normal, PerfHudLines.ForMarker(PerfHudLines.MarkerWarnMs - 0.01f));
        Assert.AreEqual(PerfHudLineKind.Warning, PerfHudLines.ForMarker(PerfHudLines.MarkerWarnMs));
        Assert.AreEqual(PerfHudLines.FrameBudgetMs / 3f, PerfHudLines.MarkerWarnMs, 1e-4f);
    }

    [Test]
    public void Join_PutsEveryLineOnItsOwnRow_LikeAppendLine()
    {
        var lines = new[]
        {
            new PerfHudLine("a", PerfHudLineKind.Normal),
            new PerfHudLine("b", PerfHudLineKind.Warning),
        };

        Assert.AreEqual("a" + System.Environment.NewLine + "b" + System.Environment.NewLine,
            PerfHudLines.Join(lines),
            "HudText остался строкой для тестов и дампа — склейка та же, что давал StringBuilder.AppendLine");
    }
}
