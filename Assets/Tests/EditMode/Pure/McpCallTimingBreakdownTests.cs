using System.Diagnostics;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Разбивка одного MCP-вызова на этапы: принят, поставлен в очередь, начал
/// выполняться, выполнен, ответ отправлен. Числа переводятся из тиков `Stopwatch` в
/// миллисекунды здесь — без Unity, без сети, без потоков, — поэтому проверяемо в dotnet.
/// Настоящие тики со Stopwatch/HttpListener собирает `McpCallTiming` (Unity-слой,
/// проверяется на собранном плеере, не здесь).</summary>
public class McpCallTimingBreakdownTests
{
    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);

    [Test]
    public void EachStage_MeasuresTheGapToTheNextTimestamp()
    {
        var b = new McpCallTimingBreakdown(
            acceptedTicks: Ticks(0.000),
            queuedTicks: Ticks(0.001),
            startedTicks: Ticks(0.301),
            executedTicks: Ticks(0.329),
            respondedTicks: Ticks(0.330));

        Assert.AreEqual(1.0, b.AcceptedToQueuedMs, 0.05,
            "приём запроса и разбор тела — до постановки в очередь на главный поток");
        Assert.AreEqual(300.0, b.QueuedToStartedMs, 0.5,
            "это и есть подозреваемое ожидание кадра — счётчик кадров/лимитер, а не сама обработка");
        Assert.AreEqual(28.0, b.StartedToExecutedMs, 0.5,
            "сама работа обработчика на главном потоке");
        Assert.AreEqual(1.0, b.ExecutedToRespondedMs, 0.05,
            "запись ответа в HTTP-поток — должна быть дёшева");
    }

    [Test]
    public void TotalMs_IsAcceptedToResponded_NotTheSumOfStages()
    {
        var b = new McpCallTimingBreakdown(
            acceptedTicks: Ticks(10.0),
            queuedTicks: Ticks(10.1),
            startedTicks: Ticks(10.4),
            executedTicks: Ticks(10.42),
            respondedTicks: Ticks(10.43));

        Assert.AreEqual(430.0, b.TotalMs, 0.5,
            "итог — это (отправлен - принят) напрямую, а не сумма промежуточных разниц: "
            + "иначе округление на каждом этапе накопится в собственную погрешность");
    }

    [Test]
    public void ZeroElapsed_ReportsZero_NotNegativeOrNaN()
    {
        long t = Ticks(5.0);
        var b = new McpCallTimingBreakdown(t, t, t, t, t);

        Assert.AreEqual(0.0, b.AcceptedToQueuedMs);
        Assert.AreEqual(0.0, b.QueuedToStartedMs);
        Assert.AreEqual(0.0, b.StartedToExecutedMs);
        Assert.AreEqual(0.0, b.ExecutedToRespondedMs);
        Assert.AreEqual(0.0, b.TotalMs);
    }

    [Test]
    public void Format_NamesEveryStage_AndCarriesTheWorkCounter()
    {
        var b = new McpCallTimingBreakdown(
            Ticks(0), Ticks(0.001), Ticks(0.301), Ticks(0.329), Ticks(0.330));
        var text = b.Format("create_elements", validationRecomputes: 1);

        Assert.That(text, Does.Contain("method=create_elements"));
        Assert.That(text, Does.Contain("accepted->queued="));
        Assert.That(text, Does.Contain("queued->started="));
        Assert.That(text, Does.Contain("started->executed="));
        Assert.That(text, Does.Contain("executed->responded="));
        Assert.That(text, Does.Contain("validateRecomputes=1"),
            "сторож на регресс — счётчик пересчётов сцены за этот вызов, а не только миллисекунды");
    }

    [Test]
    public void ACallThatNeverReachedTheScene_ReportsNoStages_InsteadOfNegativeOnes()
    {
        var b = new McpCallTimingBreakdown(
            acceptedTicks: Ticks(5.000),
            queuedTicks: 0,
            startedTicks: 0,
            executedTicks: 0,
            respondedTicks: Ticks(5.046));

        Assert.IsFalse(b.ReachedTheScene);
        Assert.AreEqual(46.0, b.TotalMs, 0.5, "итог у такого вызова честный: принят -> отвечен");
        Assert.AreEqual(0.0, b.AcceptedToQueuedMs);
        Assert.AreEqual(0.0, b.ExecutedToRespondedMs);

        var text = b.Format("initialize", validationRecomputes: 0);

        Assert.That(text, Does.Not.Contain("-"),
            "initialize и tools/list не идут на главный поток, и их отметки остаются нулём: "
            + "в логе плеера это печаталось как accepted->queued=-5013,20ms — минус длиной "
            + "в пять секунд, из-за которого две первые строки разбивки читались как мусор");
        Assert.That(text, Does.Contain("answeredWithoutTheScene"));
    }

    [Test]
    public void ACallThatDidReachTheScene_StillPrintsEveryStage()
    {
        var b = new McpCallTimingBreakdown(
            Ticks(0), Ticks(0.001), Ticks(0.301), Ticks(0.329), Ticks(0.330));

        Assert.IsTrue(b.ReachedTheScene);
        Assert.That(b.Format("create_elements", 1), Does.Contain("started->executed="),
            "обычный вызов инструмента печатает разбивку как раньше");
    }

    [Test]
    public void Format_CarriesTheSceneScans_AndTheStagesOfTheHandler()
    {
        var b = new McpCallTimingBreakdown(
            Ticks(0), Ticks(0.001), Ticks(0.301), Ticks(0.329), Ticks(0.330));

        var text = b.Format("create_elements", validationRecomputes: 1,
            stages: "spawn 40,20ms, settle 12,10ms", sceneScans: 3);

        Assert.That(text, Does.Contain("sceneScans=3"),
            "обход сцены — работа, и её число обязано стоять в той же строке, что и миллисекунды");
        Assert.That(text, Does.Contain("stages=[spawn 40,20ms, settle 12,10ms]"),
            "72,87 мс в started->executed были безымянными — теперь этапы называют себя");
    }

    [Test]
    public void Format_WithoutStages_DoesNotPrintAnEmptyBracket()
    {
        var b = new McpCallTimingBreakdown(
            Ticks(0), Ticks(0.001), Ticks(0.301), Ticks(0.329), Ticks(0.330));

        Assert.That(b.Format("get_elements", 0), Does.Not.Contain("stages="),
            "обработчик без разметки этапов не должен печатать пустые скобки");
    }
}
