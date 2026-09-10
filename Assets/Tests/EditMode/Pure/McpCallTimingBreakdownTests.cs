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
}
