using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Строка CSV обязана быть строкой ОДНОГО кадра. Половина её величин приходит от
/// движка с опозданием на кадр (<c>main_ms</c>, <c>gc_bytes</c>, <c>dt_ms</c>, draw/batches/
/// setpass), половина — нет (маркеры, <c>getall_calls</c>), и пока их писали одним семплом,
/// файл предсказуемо врал: работа клика в строке 3001, его же 206 мс — в строке 3002.
///
/// Правило проведено ЧЕРЕЗ ЗАПИСЬ: строка пишется, когда известны обе половины, то есть на
/// семпл позже, а остановку записи дописывает <c>FlushTheFrameInFlight</c>, чтобы последний
/// кадр не пропал. Это законно ровно потому, что <c>PerfMonitor</c> исполняется последним в
/// кадре (за этим следит <see cref="PerfMonitorDrainTimingTests"/>): в любой момент между
/// его сливами «последний завершившийся кадр» у рекордеров движка и у самой строки — один и
/// тот же.
///
/// Проверять склейку по САМИМ счётчикам движка в EditMode нечем: <c>ProfilerRecorder</c>
/// там не отдаёт значений, а <c>Time.frameCount</c> не растёт. Поэтому правило разложено
/// надвое: пара половин живёт в <see cref="AlignedFrameRow"/> и проверяется на быстром пути
/// (<c>AlignedFrameRowTests</c>), а здесь проверяется ПРОВОДКА — что ни один кадр не
/// потерян, что работа кадра ждёт своих счётчиков, и что ни одна колонка не заполняется
/// мимо согласованной строки.</summary>
public class PerfCsvRowAlignmentTests
{
    private GameObject? _host;
    private PerfMonitor? _monitor;
    private bool _enabledBefore;

    [SetUp]
    public void SetUp()
    {
        _enabledBefore = PerfMonitor.Enabled;
        PerfMonitor.Enabled = false;
        PerfMarkers.DropEverythingMeasuredSoFar();
    }

    [TearDown]
    public void TearDown()
    {
        if (_monitor != null) _monitor.SimulateOnDestroyForTests();
        _monitor = null;
        if (_host != null) Object.DestroyImmediate(_host);
        _host = null;
        PerfMonitor.Enabled = _enabledBefore;
        PerfMarkers.DropEverythingMeasuredSoFar();
    }

    /// <summary>Запись НЕ останавливается ни в одном тесте этого класса: остановка пишет
    /// файл в <c>test-results/perf</c>, а проверять здесь нужно счётчик строк, а не диск.
    /// Дописывание последнего кадра зовётся тем же методом, которым его зовёт остановка.</summary>
    private PerfMonitor RecordingMonitor()
    {
        _host = new GameObject("PerfMonitor сторожа выравнивания");
        var monitor = _host.AddComponent<PerfMonitor>();
        PerfMonitor.Enabled = true;
        monitor.SimulateAwakeForTests();
        monitor.SetCsvRecording(true);
        _monitor = monitor;
        return monitor;
    }

    private static int SlotOf(string markerName)
    {
        var names = PerfMarkers.NamesInDeclarationOrder;
        for (int i = 0; i < names.Count; i++)
            if (names[i] == markerName) return i;
        return -1;
    }

    private const string AMarkerThatRunsInLateUpdate = "SceneChangeTracker.Poll";

    private static long FiveMillisecondsInTicks =>
        (long)(System.Diagnostics.Stopwatch.Frequency * 0.005);

    [Test]
    public void EveryRecordedFrame_ReachesTheFile_TheLastOneIncluded()
    {
        var monitor = RecordingMonitor();

        monitor.SimulateLateUpdateForTests();
        Assert.AreEqual(0, monitor.CsvRows,
            "первый семпл знает работу своего кадра и НЕ знает его счётчиков движка: писать "
            + "нечего, и строка с пустой половиной как раз и была тем враньём");

        monitor.SimulateLateUpdateForTests();
        monitor.SimulateLateUpdateForTests();
        Assert.AreEqual(2, monitor.CsvRows, "каждый следующий семпл дописывает предыдущий кадр");

        monitor.FlushTheFrameInFlight();

        Assert.AreEqual(3, monitor.CsvRows,
            "выравнивание при записи не имеет права стоить последнего кадра: остановка "
            + "дописывает кадр в полёте тем же методом");
        Assert.AreEqual(0, monitor.FrameInFlight!.FramesLost,
            "порядок «дописать прошлый кадр, потом запомнить нынешний» соблюдён");
    }

    [Test]
    public void TheWorkOfAFrame_WaitsForItsOwnCounters_InsteadOfLeavingForTheNextRow()
    {
        var monitor = RecordingMonitor();
        int slot = SlotOf(AMarkerThatRunsInLateUpdate);
        Assert.GreaterOrEqual(slot, 0, $"маркера {AMarkerThatRunsInLateUpdate} больше нет — "
            + "тест обязан ссылаться на существующий маркер, иначе он не проверяет ничего");

        PerfMarkers.AddTicks(slot, FiveMillisecondsInTicks);
        monitor.SimulateLateUpdateForTests();

        Assert.AreEqual(5f, monitor.FrameInFlight!.MarkersMs[slot], 1f,
            "работа кадра ждёт в строке, а не уезжает в файл без своих миллисекунд");

        monitor.SimulateLateUpdateForTests();

        Assert.AreEqual(0f, monitor.FrameInFlight!.MarkersMs[slot], 1e-3f,
            "следующий кадр ничего не мерил: унаследуй он чужие 5 мс — и сдвиг вернулся бы, "
            + "только теперь внутри одной строки");
    }

    [Test]
    public void EveryCsvColumn_IsFilledFromTheAlignedRow_NotFromARecorderDirectly()
    {
        var body = BodyOf("private void WriteCsvRow(AlignedFrameRow row)");

        var assignments = Regex.Matches(body, @"_row\[[^\]]+\]\s*=\s*([^;]+);");
        Assert.GreaterOrEqual(assignments.Count, 9,
            "скан не нашёл присваиваний колонок — сторож зеленел бы, ничего не проверив");

        foreach (Match assignment in assignments)
            StringAssert.Contains("row.", assignment.Groups[1].Value,
                "колонка, заполненная мимо согласованной строки, снова смешивает два кадра — "
                + "и заметно это будет только в дампе, месяц спустя: " + assignment.Value);
    }

    [Test]
    public void TheSlowFrameLine_IsBuiltFromTheSameAlignedRow_AsTheCsvColumns()
    {
        var body = BodyOf("private static void ReportTheFrameThatJustEnded(AlignedFrameRow row)");

        var call = Regex.Match(body, @"SlowFrameLine\((?<args>[^;]+)\)\)");
        Assert.IsTrue(call.Success,
            "скан не нашёл вызова SlowFrameLine — сторож зеленел бы, ничего не проверив");

        var args = call.Groups["args"].Value.Split(',');
        Assert.AreEqual(8, args.Length,
            "аргументов у строки медленного кадра восемь; их число изменилось — перечитай, "
            + "откуда берётся каждый");

        foreach (var arg in args)
            StringAssert.Contains("row.", arg,
                "«[Perf] кадр N: …» и строка N в CSV обязаны быть про ОДИН кадр: величина, "
                + "взятая мимо согласованной строки, разведёт лог и файл: " + arg.Trim());
    }

    private static string BodyOf(string signature)
    {
        var path = Path.Combine(Application.dataPath, "Scripts", "Core", "Diagnostics", "PerfMonitor.cs");
        Assert.IsTrue(File.Exists(path), $"сканер смотрит не туда — не найден {path}");

        var lines = File.ReadAllLines(path);
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains(signature)) { start = i; break; }

        Assert.GreaterOrEqual(start, 0,
            $"в PerfMonitor.cs нет метода «{signature}» — сторож ниже проверял бы пустую строку");

        var body = new System.Text.StringBuilder();
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (lines[i] == "        }") break;
            body.AppendLine(lines[i]);
        }
        return body.ToString();
    }
}
