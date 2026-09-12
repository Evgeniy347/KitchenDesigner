using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Второй подозреваемый по P1 §3: один обход сцены
/// (<c>PartRegistry.GetAll</c>) стоит 0,7–2,2 МБ мусора и десятки миллисекунд на сцене
/// в 400 деталей, и вопрос «делает ли <c>create_elements</c> обходы» до сих пор не имел
/// прибора — <c>SceneScanLog</c> отвечает ПОКАДРОВО и отдаёт кадр разрушительно, через
/// <c>Take</c>. Вызов MCP живёт не по кадрам, а его наблюдатель — HTTP-поток; забрать
/// у <c>PerfMonitor</c> его кадр значит сломать чужой прибор.
///
/// Поэтому счётчик здесь ОТДЕЛЬНЫЙ и монотонный: он только растёт, наблюдатель берёт
/// разницу за своё окно, и никто ни у кого ничего не отнимает.</summary>
public class SceneScanCounterTests
{
    [Test]
    public void EveryScan_MovesTheCounterByOne()
    {
        long before = SceneScanCounter.Scans;

        SceneScanCounter.Note();
        SceneScanCounter.Note();
        SceneScanCounter.Note();

        Assert.AreEqual(3, SceneScanCounter.Scans - before,
            "три обхода сцены — три единицы; это РАБОТА, а не миллисекунды");
    }

    [Test]
    public void TheCounterOnlyGrows_SoTwoObserversDoNotFightOverIt()
    {
        long first = SceneScanCounter.Scans;
        SceneScanCounter.Note();
        long second = SceneScanCounter.Scans;
        SceneScanCounter.Note();

        Assert.Greater(second, first);
        Assert.Greater(SceneScanCounter.Scans, second,
            "счётчик не сбрасывается чтением: окно наблюдателя — это разница, а не остаток");
    }

    [Test]
    public void TakingTheFrameLog_DoesNotTouchTheCounter()
    {
        SceneScanCounter.Note();
        SceneScanLog.Note("GetAll", @"C:\kd\Assets\Scripts\Core\MCP\McpCommandHandler.cs");
        long before = SceneScanCounter.Scans;

        SceneScanLog.Take();

        Assert.AreEqual(before, SceneScanCounter.Scans,
            "PerfMonitor забирает кадр у SceneScanLog каждый кадр — счётчик вызова MCP "
            + "обязан это пережить, иначе два прибора будут отнимать работу друг у друга");
    }
}
