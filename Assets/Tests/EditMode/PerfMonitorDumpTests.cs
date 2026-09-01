#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Linq;
using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core;

public class PerfMonitorDumpTests
{
    private const int FramesPerSecondAssumedForBudgets = 60;

    private static PerfMonitor.MarkerSlot Slot(string name, params float[] framesMs)
    {
        var slot = new PerfMonitor.MarkerSlot { Name = name };
        foreach (var ms in framesMs) slot.RecordFrame(ms);
        return slot;
    }

    private static string WindowTop(params PerfMonitor.MarkerSlot[] slots)
    {
        var sb = new StringBuilder();
        PerfMonitor.AppendWindowTop(sb, slots);
        return sb.ToString();
    }

    [Test]
    public void MarkerSlot_CountsAFrameWithZeroMilliseconds_SoTheAverageIsPerFrameNotPerHit()
    {
        var slot = Slot("A", 0f, 2f);

        Assert.AreEqual(2, slot.FramesMeasured,
            "кадр, в котором маркер не сработал, — законный семпл: он тоже был");
        Assert.AreEqual(1f, slot.AverageMsPerFrame, 0.0001f,
            "среднее считается ПО КАДРАМ, а не по срабатываниям: иначе маркер, "
            + "который срабатывает раз в десять кадров, выглядит в десять раз дороже, "
            + "чем стоит на самом деле");
        Assert.AreEqual(2f, slot.MaxMs, 0.0001f);
    }

    [Test]
    public void MarkerSlot_WithNoFramesAtAll_ReportsZeroAverage_WithoutDividingByZero()
    {
        var slot = new PerfMonitor.MarkerSlot { Name = "A" };
        Assert.AreEqual(0f, slot.AverageMsPerFrame, 0.0001f);
    }

    [Test]
    public void IndicesByDescendingValue_PutsTheMostExpensiveFirst()
    {
        var order = PerfMonitor.IndicesByDescendingValue(new[] { 0.1f, 5f, 1f });
        CollectionAssert.AreEqual(new[] { 1, 2, 0 }, order,
            "дамп читают сверху вниз: первым обязан идти самый дорогой маркер");
    }

    [Test]
    public void IndicesByDescendingValue_OnAnEmptyArray_ReturnsEmpty()
    {
        Assert.IsEmpty(PerfMonitor.IndicesByDescendingValue(new float[0]));
    }

    [Test]
    public void WindowTop_ShowsAMarkerThatNeverAttached_AsNoData_NotAsZeroMilliseconds()
    {
        string text = WindowTop(new PerfMonitor.MarkerSlot { Name = "Never.Attached" });

        StringAssert.Contains("Never.Attached", text);
        StringAssert.Contains(PerfMonitor.MarkerNeverAttachedText, text,
            "маркер, к которому не удалось подключиться, без этой пометки выглядел бы "
            + "как «этот метод ничего не стоит» — и оптимизировали бы не то");
        StringAssert.DoesNotContain(
            PerfMonitor.MarkerLine("Never.Attached", 0f, PerfMonitor.DumpNameColumnWidth), text,
            "вместо честного «нет данных» это была бы строка с нулём миллисекунд");
    }

    [Test]
    public void WindowTop_HidesMarkersCheaperThanTheDumpThreshold()
    {
        float belowThreshold = PerfMonitor.DumpThresholdMs / 2f;
        Assume.That(belowThreshold, Is.LessThan(PerfMonitor.DumpThresholdMs));

        string text = WindowTop(Slot("Cheap.Noise", belowThreshold, belowThreshold));

        StringAssert.DoesNotContain("Cheap.Noise", text,
            "маркеры дешевле порога — шум: в дампе на 14 строк они вытеснили бы "
            + "то, ради чего дамп читают");
    }

    [Test]
    public void WindowTop_KeepsAMarkerWhoseMaxSpiked_EvenWhenItsAverageIsBelowTheThreshold()
    {
        var spiky = Slot("Rare.Spike", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, PerfMonitor.DumpThresholdMs * 6f);
        Assume.That(spiky.AverageMsPerFrame, Is.LessThan(PerfMonitor.DumpThresholdMs),
            "среднее обязано быть НИЖЕ порога, иначе тест зелен по другой причине");

        StringAssert.Contains("Rare.Spike", WindowTop(spiky),
            "рывок делает редкий дорогой кадр, а не среднее: маркер со спайком "
            + "остаётся в дампе, даже если в среднем он дёшев");
    }

    [Test]
    public void WindowTop_ShowsNoMoreThanTheTopMarkers()
    {
        var slots = Enumerable.Range(0, PerfMonitor.TopMarkersInDump + 5)
            .Select(i => Slot("Marker." + i, 1f + i))
            .ToArray();

        int lines = WindowTop(slots).Split('\n').Count(l => l.Contains("Marker."));

        Assert.AreEqual(PerfMonitor.TopMarkersInDump, lines,
            "дамп печатается в лог каждые две секунды с небольшим: без ограничения "
            + "он вытеснит из консоли всё остальное");
    }

    [Test]
    public void MarkerLine_PutsTheMilliseconds_AtTheSameColumn_ForNamesOfDifferentLength()
    {
        string shortName = PerfMonitor.MarkerLine("A", 1.5f, PerfMonitor.DumpNameColumnWidth);
        string longName = PerfMonitor.MarkerLine("A.Very.Long.Marker.Name", 1.5f, PerfMonitor.DumpNameColumnWidth);

        Assert.AreEqual(shortName.Length, longName.Length,
            "колонки в дампе и в HUD выровнены ПРОБЕЛАМИ: поэтому HUD и просит "
            + "моноширинный шрифт — без него столбцы поплывут");
        Assert.AreEqual(shortName.IndexOf("ms"), longName.IndexOf("ms"));
        Assert.Greater(shortName.Length, PerfMonitor.DumpNameColumnWidth);
    }

    [Test]
    public void CsvCapacity_HoldsAtLeastAMinuteOfFramesAt60Fps()
    {
        Assert.GreaterOrEqual(PerfMonitor.CsvCapacityFrames / FramesPerSecondAssumedForBudgets, 60,
            "буфер преаллоцирован целиком, поэтому его размер — это решение «сколько "
            + "секунд прогона мы вообще можем записать»; меньше минуты не покрывает "
            + "даже один проход по сцене");
    }

    [Test]
    public void Hud_IsRebuiltFarRarerThanEveryFrame()
    {
        float framesBetweenRebuilds = PerfRefreshInFrames();
        Assert.Greater(framesBetweenRebuilds, 5f,
            "строка HUD собирается через StringBuilder и сортировку: делать это каждый "
            + "кадр значит платить в том самом кадре, который измеряем");
    }

    [Test]
    public void NoProductionFile_TurnsTheProfilerOn_ExceptPerfMonitorItself()
    {
        var scriptsRoot = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts");
        var files = System.IO.Directory.GetFiles(scriptsRoot, "*.cs", System.IO.SearchOption.AllDirectories);
        Assert.Greater(files.Length, 100, "скан не видит исходников — он зеленел бы, ничего не проверив");

        var offenders = files
            .Where(f => System.IO.File.ReadAllText(f).Contains("PerfMonitor.Enabled ="))
            .Select(System.IO.Path.GetFileName)
            .ToArray();

        CollectionAssert.IsEmpty(offenders,
            "PerfMonitor включает только F9 и запуск записи CSV. Стоит любому другому "
            + "файлу поднять флаг — и дамп полетит в лог каждого PlayMode-теста, "
            + "потому что Bootstrap поднимает монитор всегда: " + string.Join(", ", offenders));
    }

    private static float PerfRefreshInFrames() =>
        PerfMonitor.HudRefreshSeconds * FramesPerSecondAssumedForBudgets;
}

#endif
