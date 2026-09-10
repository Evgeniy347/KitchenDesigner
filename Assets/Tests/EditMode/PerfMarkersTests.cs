using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PerfMarkersTests
{
    private static FieldInfo[] MarkerFields() =>
        typeof(PerfMarkers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PerfMarker))
            .ToArray();

    [Test]
    public void TheScan_ActuallySeesTheMarkers()
    {
        Assert.Greater(MarkerFields().Length, 10,
            "рефлексия не нашла маркеров — тест ниже зеленел бы, ничего не проверив");
    }

    [Test]
    public void TouchingTheNameList_RegistersEveryMarker_SoEverySlotHasItsName()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;

        Assert.AreEqual(MarkerFields().Length, names.Count,
            "обращение к списку имён прогревает класс целиком: после него ВСЕ маркеры "
            + "созданы, и у каждого есть слот под замер. Расхождение значит, что список "
            + "наполняется не каждым Reg() — а если объявить его НИЖЕ маркеров, "
            + "инициализаторы полей пойдут по порядку объявления и Reg() упадёт в null "
            + "ещё в статическом конструкторе");
    }

    [Test]
    public void EveryMarkerName_IsFilledIn_AndUnique()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;
        CollectionAssert.AllItemsAreNotNull(names);
        Assert.IsFalse(names.Any(string.IsNullOrWhiteSpace), "пустое имя маркера");
        Assert.AreEqual(names.Count, names.Distinct().Count(),
            "два маркера с одним именем сольются в одну строку дампа, и один из замеров "
            + "молча пропадёт");
    }

    [Test]
    public void FirstName_PinsTheDeclarationOrder()
    {
        // Раньше тест фиксировал ещё и ПОСЛЕДНЕЕ имя — но само его сообщение требовало
        // дописывать новые маркеры в конец списка, а значит любой добавленный маркер красил
        // тест в красный без единого дефекта. Первый маркер защищать от сдвига можно и нужно
        // (это самая старая колонка CSV), последний — нет: он по определению меняется чаще
        // всего.
        var names = PerfMarkers.NamesInDeclarationOrder;
        Assert.AreEqual("CameraController.Update", names[0],
            "порядок имён — это порядок колонок CSV: перестановка объявлений сдвигает "
            + "колонки уже собранных файлов относительно новых");
    }

    [Test]
    public void AnOpenScope_AddsRealTimeToItsOwnSlot_AndTakeFrameMsDrainsIt()
    {
        int slot = PerfMarkers.NamesInDeclarationOrder.ToList().IndexOf("CameraController.Update");
        Assume.That(slot, Is.GreaterThanOrEqualTo(0));

        bool wasMeasuring = PerfMarkers.Measuring;
        try
        {
            PerfMarkers.Measuring = true;
            PerfMarkers.TakeFrameMs(slot);

            using (PerfMarkers.CameraUpdate.Auto()) BurnAtLeastOneMillisecond();

            float measured = PerfMarkers.TakeFrameMs(slot);

            Assert.Greater(measured, 0.5f,
                "это ЕДИНСТВЕННЫЙ тест, который доказывает, что сенсор вообще меряет "
                + "время. Раньше замер шёл через ProfilerRecorder, а тот в не-dev "
                + "плеере не подключается ни к одному маркеру — профиль печатал список "
                + "имён и «нет данных» против каждого");
            Assert.AreEqual(0f, PerfMarkers.TakeFrameMs(slot), 0.0001f,
                "TakeFrameMs забирает накопленное и обнуляет слот: иначе замер "
                + "предыдущего кадра приписался бы следующему");
        }
        finally
        {
            PerfMarkers.Measuring = wasMeasuring;
            PerfMarkers.DropEverythingMeasuredSoFar();
        }
    }

    /// <summary>Раньше называлась `...CostsNothing_AndRecordsNothing`, но проверяла только
    /// «ничего не записывает» — «ничего не стоит» она не измеряла вовсе, а без правки D4
    /// (компиляция Scope под `#if UNITY_EDITOR || DEVELOPMENT_BUILD || KD_PERF`) этот тест
    /// оставался бы зелёным. Стоимость в релизном плеере доказывает не EditMode-тест (здесь
    /// UNITY_EDITOR всегда определён, и полная ветка всегда компилируется), а сам исходник —
    /// смотри `PerfMarkerScope_CompilesAwayOutsideEditorDevBuildOrKdPerf` ниже.</summary>
    [Test]
    public void WithMeasuringOff_TheScopeRecordsNothing()
    {
        int slot = PerfMarkers.NamesInDeclarationOrder.ToList().IndexOf("CameraController.Update");
        bool wasMeasuring = PerfMarkers.Measuring;
        try
        {
            PerfMarkers.Measuring = false;
            PerfMarkers.TakeFrameMs(slot);

            using (PerfMarkers.CameraUpdate.Auto()) BurnAtLeastOneMillisecond();

            Assert.AreEqual(0f, PerfMarkers.TakeFrameMs(slot), 0.0001f,
                "пока F9 не нажат, ни один маркер не имеет права записать время — иначе "
                + "выключенный замер всё равно копит такты, которые никто не сливает");
        }
        finally
        {
            PerfMarkers.Measuring = wasMeasuring;
            PerfMarkers.DropEverythingMeasuredSoFar();
        }
    }

    /// <summary>Дефект D4: конструктор и Dispose `PerfMarker.Scope` раньше исполнялись
    /// безусловно — в релизном плеере, где ни редактор, ни dev-сборка не определяют
    /// `ENABLE_PROFILER`, это была стоимость без единого читателя. Тест не может запустить
    /// сборку под релизными символами внутри EditMode-прогона, поэтому проверяет источник:
    /// тело обоих методов обязано жить под `#if UNITY_EDITOR || DEVELOPMENT_BUILD ||
    /// KD_PERF`, а не под ней самой (`PerfMarkers.cs` — файл другого сторожа и обязан
    /// остаться без `#if`).</summary>
    [Test]
    public void PerfMarkerScope_CompilesAwayOutsideEditorDevBuildOrKdPerf()
    {
        var source = File.ReadAllText(Path.Combine(DiagnosticsDir(), "PerfMarker.cs"));

        StringAssert.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD || KD_PERF", source,
            "без этого условия конструктор и Dispose Scope платят на каждом входе даже в "
            + "релизном плеере — а маркеры стоят на ~37 горячих путях каждый кадр");
    }

    [Test]
    public void PerfMarkers_IsCompiledUnconditionally_BecauseTheMarkersSitInProductionCode()
    {
        var source = File.ReadAllText(MarkersSourcePath());
        StringAssert.DoesNotContain("#if", source,
            "маркеры расставлены по боевому коду: под #if их пришлось бы обкладывать "
            + "директивами в каждом вызывающем файле");
    }

    [Test]
    public void PerfMarker_MeasuresWithStopwatch_NotWithProfilerRecorder()
    {
        var source = File.ReadAllText(Path.Combine(DiagnosticsDir(), "PerfMarker.cs"));

        StringAssert.Contains("Stopwatch.GetTimestamp", source,
            "замер обязан идти собственным секундомером. ProfilerMarker.Begin/End "
            + "помечены [Conditional(\"ENABLE_PROFILER\")], а этот символ определён "
            + "только в редакторе и в development-сборке — в обычном плеере, куда "
            + "PerfMonitor попал коммитом c39d22d1, они вырезаются, и ProfilerRecorder "
            + "не цепляется НИ К ОДНОМУ маркеру");
    }

    private static string DiagnosticsDir() =>
        Path.Combine(Application.dataPath, "Scripts", "Core", "Diagnostics");

    private static string MarkersSourcePath() => Path.Combine(DiagnosticsDir(), "PerfMarkers.cs");

    private static void BurnAtLeastOneMillisecond()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 2.0) { }
    }
}
