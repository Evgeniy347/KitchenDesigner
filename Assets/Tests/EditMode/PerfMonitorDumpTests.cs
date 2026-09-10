using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    [Test]
    public void PerfMonitor_IsCompiledUnconditionally_SoF9StillOpensTheWindowInAReleaseBuild()
    {
        var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Core", "Diagnostics", "PerfMonitor.cs");
        Assert.IsTrue(System.IO.File.Exists(path), "скан не видит файла — проверять было бы нечего");

        var source = System.IO.File.ReadAllText(path);
        StringAssert.DoesNotContain("#if", source,
            "F9 читает этот файл; спрятанный под #if UNITY_EDITOR/DEVELOPMENT_BUILD класс "
            + "целиком выпадает из сборки KitchenDesigner/Build Windows (development=false) — "
            + "именно так окно пропало в собранном плеере в прошлый раз");
    }

    [Test]
    public void Bootstrap_CreatesPerfMonitorUnconditionally_SoTheHotkeyHasSomethingToToggle()
    {
        var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Core", "Infrastructure", "Bootstrap.cs");
        Assert.IsTrue(System.IO.File.Exists(path), "скан не видит файла — проверять было бы нечего");

        var lines = System.IO.File.ReadAllLines(path);
        int guardDepth = 0;
        bool gated = false;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#if")) { guardDepth++; continue; }
            if (trimmed.StartsWith("#endif")) { guardDepth = System.Math.Max(0, guardDepth - 1); continue; }
            if (guardDepth > 0 && line.Contains("AddComponent<PerfMonitor>")) gated = true;
        }

        Assert.IsFalse(gated,
            "если Bootstrap создаёт PerfMonitor только под #if UNITY_EDITOR/DEVELOPMENT_BUILD, "
            + "в обычной (не-dev) сборке компонента на сцене вообще нет — F9 нажимается, "
            + "но переключать нечего, и окно не появляется");
    }

    private static float PerfRefreshInFrames() =>
        PerfMonitor.HudRefreshSeconds * FramesPerSecondAssumedForBudgets;

    private static readonly Regex GuardedMemberDeclaration = new Regex(
        @"(?:public|internal)\s+static\s+[\w<>\[\],\.\?]+\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|=>|\{)",
        RegexOptions.Compiled);

    [Test]
    public void DiagnosticsFiles_NeverCallASymbol_ThatIsItselfHiddenBehindAnEditorOrDevBuildGuard()
    {
        var scriptsRoot = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts");
        var diagnosticsDir = System.IO.Path.Combine(scriptsRoot, "Core", "Diagnostics");
        var diagnosticsFiles = System.IO.Directory.GetFiles(diagnosticsDir, "*.cs");
        Assert.GreaterOrEqual(diagnosticsFiles.Length, 4,
            "скан не видит файлов диагностики — проверять было бы нечего");

        var everyOtherFile = System.IO.Directory
            .GetFiles(scriptsRoot, "*.cs", System.IO.SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(diagnosticsDir))
            .ToArray();

        var guardedSymbols = new Dictionary<string, string>();
        foreach (var file in everyOtherFile)
            foreach (var name in GuardedMembersDeclaredUnderEditorOrDevBuildGuard(file))
                guardedSymbols[name] = System.IO.Path.GetFileName(file);

        var offenders = new List<string>();
        foreach (var diagFile in diagnosticsFiles)
        {
            var text = System.IO.File.ReadAllText(diagFile);
            foreach (var (symbol, definingFile) in guardedSymbols)
            {
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(symbol)}\b"))
                    offenders.Add($"{System.IO.Path.GetFileName(diagFile)} зовёт {symbol} " +
                                  $"(объявлен под #if UNITY_EDITOR/DEVELOPMENT_BUILD в {definingFile})");
            }
        }

        CollectionAssert.IsEmpty(offenders,
            "PerfMonitor/PerfHud/PerfCsvLog/PerfMarkers компилируются в обычном плеере "
            + "безусловно — им нельзя звать метод или свойство, объявленные под "
            + "#if UNITY_EDITOR/DEVELOPMENT_BUILD в другом файле: в обычной сборке символа "
            + "не будет, и получится ровно тот CS0117, из-за которого не собрался плеер: "
            + string.Join("; ", offenders));
    }

    private static IEnumerable<string> GuardedMembersDeclaredUnderEditorOrDevBuildGuard(string file)
    {
        var lines = System.IO.File.ReadAllLines(file);
        var relevantStack = new Stack<bool>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#if"))
            {
                bool relevant = trimmed.Contains("UNITY_EDITOR") || trimmed.Contains("DEVELOPMENT_BUILD");
                relevantStack.Push(relevant || (relevantStack.Count > 0 && relevantStack.Peek()));
                continue;
            }
            if (trimmed.StartsWith("#endif"))
            {
                if (relevantStack.Count > 0) relevantStack.Pop();
                continue;
            }
            if (relevantStack.Count == 0 || !relevantStack.Peek()) continue;

            var match = GuardedMemberDeclaration.Match(line);
            if (match.Success) yield return match.Groups[1].Value;
        }
    }
}
