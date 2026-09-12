using System.Diagnostics;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Прибор, заведённый под P1 §3 «Вызов MCP стоит 300–500 мс». Разбивка
/// в логе плеера называла всего четыре этапа — принят, поставлен в очередь, выполнен,
/// отвечен, — и целиком прятала работу обработчика в одном числе
/// <c>started->executed</c>. На дымовом прогоне 12.09 это число было 72,87 мс из 118,87,
/// и «из чего оно» не отвечал никто. Здесь этап называет себя сам, как это уже сделано
/// в <c>SceneScanLog</c> для обходов сцены.
///
/// Проверяется ИМЕННО прибор, на искусственных тиках: настоящий <c>Stopwatch</c> в тесте
/// дал бы измерение времени, а по времени тесты в этом репозитории не годятся. Поэтому
/// <c>Add</c> принимает тики напрямую, и число миллисекунд выводится из частоты
/// <c>Stopwatch</c>, а не из сна.</summary>
public class McpCallStagesTests
{
    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);

    [SetUp]
    public void SetUp() => McpCallStages.Forget();

    [TearDown]
    public void TearDown() => McpCallStages.Forget();

    [Test]
    public void AStage_CarriesItsOwnMilliseconds()
    {
        McpCallStages.Add("spawn", Ticks(0.040));

        Assert.IsTrue(McpCallStages.TryGet("spawn", out double ms, out int times));
        Assert.AreEqual(40.0, ms, 0.5, "40 мс работы этапа spawn");
        Assert.AreEqual(1, times, "этап отработал один раз");
    }

    [Test]
    public void RepeatsOfOneStage_AddUpAndAreCounted()
    {
        McpCallStages.Add("describe", Ticks(0.010));
        McpCallStages.Add("describe", Ticks(0.030));

        Assert.IsTrue(McpCallStages.TryGet("describe", out double ms, out int times));
        Assert.AreEqual(40.0, ms, 0.5, "две ходки складываются, а не затирают друг друга");
        Assert.AreEqual(2, times,
            "число ходок — это РАБОТА: этап, отработавший дважды за вызов, виден как ×2");
    }

    [Test]
    public void Stages_DoNotBleedIntoEachOther()
    {
        McpCallStages.Add("spawn", Ticks(0.040));
        McpCallStages.Add("settle", Ticks(0.012));

        Assert.IsTrue(McpCallStages.TryGet("spawn", out double spawnMs, out _));
        Assert.IsTrue(McpCallStages.TryGet("settle", out double settleMs, out _));
        Assert.AreEqual(40.0, spawnMs, 0.5);
        Assert.AreEqual(12.0, settleMs, 0.5);
    }

    [Test]
    public void AnUnknownStage_IsNotInvented()
    {
        McpCallStages.Add("spawn", Ticks(0.040));

        Assert.IsFalse(McpCallStages.TryGet("settle", out double ms, out int times),
            "этап, которого не было, обязан отсутствовать, а не отвечать нулём как будто он был");
        Assert.AreEqual(0.0, ms);
        Assert.AreEqual(0, times);
    }

    [Test]
    public void Take_NamesEveryStage_AndMarksTheRepeatedOnes()
    {
        McpCallStages.Add("accept", Ticks(0.002));
        McpCallStages.Add("spawn", Ticks(0.040));
        McpCallStages.Add("spawn", Ticks(0.001));

        string text = McpCallStages.Take();

        Assert.That(text, Does.Contain("accept"));
        Assert.That(text, Does.Contain("spawn"));
        Assert.That(text, Does.Contain("×2"), "повтор схлопывается в ×N, а не в две строки");
    }

    [Test]
    public void Take_ClearsTheCall_SoTheNextOneDoesNotInheritIt()
    {
        McpCallStages.Add("spawn", Ticks(0.040));
        McpCallStages.Take();

        Assert.IsFalse(McpCallStages.TryGet("spawn", out _, out _),
            "следующий вызов MCP не должен унаследовать этапы предыдущего");
        Assert.AreEqual(string.Empty, McpCallStages.Take(),
            "пустой вызов печатает пустую строку, а не остатки прошлого");
    }

    [Test]
    public void MoreStagesThanRemembered_AreCountedAsAnOverflow_NotSilentlyDropped()
    {
        for (int i = 0; i <= McpCallStages.MostStagesRemembered; i++)
            McpCallStages.Add("stage" + i, Ticks(0.001));

        string text = McpCallStages.Take();

        Assert.That(text, Does.Contain("сверх предела"),
            "переполнение обязано назвать себя, иначе разбивка тихо перестанет сходиться с total");
    }

    [Test]
    public void End_MeasuresFromTheTimestampBegin_HandedOut()
    {
        long began = McpCallStages.Begin();
        McpCallStages.End("accept", began);

        Assert.IsTrue(McpCallStages.TryGet("accept", out double ms, out int times),
            "Begin/End — та пара, которой пользуется обработчик; она обязана завести этап");
        Assert.AreEqual(1, times);
        Assert.GreaterOrEqual(ms, 0.0, "отрицательной длительности быть не может");
    }
}
