using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Второй подозреваемый по P1 §3: один обход сцены
/// (<c>PartRegistry.GetAll</c>) стоит 0,7–2,2 МБ мусора и десятки миллисекунд на сцене
/// в 400 деталей. Прогон 12.09 показал <c>sceneScans=4</c> у <c>create_elements</c> и
/// <c>sceneScans=9</c> у <c>load_project</c> на проекте в ОДНУ деталь — и кто именно
/// обходит, счёт без имён не отвечал.
///
/// <c>SceneScanLog</c> на этот вопрос не годится: он отвечает ПОКАДРОВО и отдаёт кадр
/// разрушительно, через <c>Take</c>, который каждый кадр забирает <c>PerfMonitor</c>.
/// Вызов MCP живёт не по кадрам, и забрать у чужого прибора его кадр значит сломать
/// чужой прибор. Поэтому счётчик здесь ОТДЕЛЬНЫЙ и монотонный: он только растёт,
/// наблюдатель берёт срез между двумя отметками, и никто ни у кого ничего не отнимает.
///
/// [NonParallelizable]: SceneScanLog._scans/_shares/_recent — process-global, не
/// [ThreadStatic]; под ParallelScope.Fixtures (geometry/pure-tests) сосед на другом потоке
/// вклинил бы свой Note() между «before» и проверкой точной дельты
/// (SceneScanCounterIsolationTests).</summary>
[NonParallelizable]
public class SceneScanCounterTests
{
    private const string Somewhere = "McpCommandHandler.HandleCreateElements";
    private const string Elsewhere = "ElementHighlighter.RefreshHighlights";

    [Test]
    public void EveryScan_MovesTheCounterByOne()
    {
        long before = SceneScanCounter.Scans;

        SceneScanCounter.Note(Somewhere);
        SceneScanCounter.Note(Somewhere);
        SceneScanCounter.Note(Somewhere);

        Assert.AreEqual(3, SceneScanCounter.Scans - before,
            "три обхода сцены — три единицы; это РАБОТА, а не миллисекунды");
    }

    [Test]
    public void TheCounterOnlyGrows_SoTwoObserversDoNotFightOverIt()
    {
        long first = SceneScanCounter.Scans;
        SceneScanCounter.Note(Somewhere);
        long second = SceneScanCounter.Scans;
        SceneScanCounter.Note(Somewhere);

        Assert.Greater(second, first);
        Assert.Greater(SceneScanCounter.Scans, second,
            "счётчик не сбрасывается чтением: окно наблюдателя — это разница, а не остаток");
    }

    [Test]
    public void TakingTheFrameLog_DoesNotTouchTheCounter()
    {
        SceneScanCounter.Note(Somewhere);
        SceneScanLog.Note("GetAll", @"C:\kd\Assets\Scripts\Core\MCP\McpCommandHandler.cs");
        long before = SceneScanCounter.Scans;

        SceneScanLog.Take();

        Assert.AreEqual(before, SceneScanCounter.Scans,
            "PerfMonitor забирает кадр у SceneScanLog каждый кадр — счёт вызова MCP "
            + "обязан это пережить, иначе два прибора будут отнимать работу друг у друга");
    }

    [Test]
    public void Since_NamesTheCallers_OfThatWindowOnly()
    {
        SceneScanCounter.Note("ThisOneCameBeforeTheWindow");
        long mark = SceneScanCounter.Scans;

        SceneScanCounter.Note(Somewhere);
        SceneScanCounter.Note(Elsewhere);

        string text = SceneScanCounter.Since(mark);

        Assert.That(text, Does.Contain(Somewhere));
        Assert.That(text, Does.Contain(Elsewhere));
        Assert.That(text, Does.Not.Contain("ThisOneCameBeforeTheWindow"),
            "срез принадлежит одному вызову MCP: обходы, случившиеся до отметки, не его");
    }

    [Test]
    public void Since_CollapsesRepeatsOfOneCaller()
    {
        long mark = SceneScanCounter.Scans;

        SceneScanCounter.Note(Elsewhere);
        SceneScanCounter.Note(Elsewhere);
        SceneScanCounter.Note(Somewhere);

        string text = SceneScanCounter.Since(mark);

        Assert.That(text, Does.Contain(Elsewhere + "×2"),
            "повтор одного места — это ×2, а не две одинаковые строки: "
            + "именно повтор и есть лишняя работа, которую ищут");
        Assert.That(text, Does.Not.Contain(Somewhere + "×"),
            "единственный обход не помечается кратностью");
    }

    [Test]
    public void Since_OfAnEmptyWindow_IsEmpty()
    {
        long mark = SceneScanCounter.Scans;

        Assert.AreEqual(string.Empty, SceneScanCounter.Since(mark),
            "вызов, не трогавший сцену, не должен печатать скобки");
    }

    [Test]
    public void AWindowLongerThanTheRing_SaysHowManyItLost()
    {
        long mark = SceneScanCounter.Scans;
        for (int i = 0; i < SceneScanCounter.MostScansRemembered + 3; i++)
            SceneScanCounter.Note(Somewhere);

        string text = SceneScanCounter.Since(mark);

        Assert.That(text, Does.Contain("старше предела: 3"),
            "кольцо помнит ограниченно; потерянные имена обязаны назвать себя числом, "
            + "иначе сумма имён тихо разойдётся с sceneScans");
    }
}
