using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Справка F1 — единственное место, где пользователь узнаёт про профилировщик,
/// и она рукописная копия того, что умеет <c>PerfMonitor</c>. Копия расходится молча:
/// строка про клавишу остаётся верной на вид и после того, как клавишу поменяли. Поэтому
/// каждая строка справки про замер проверяется здесь ПРОТИВ самого замера, а не против
/// памяти автора.</summary>
public class HelpPerfHotkeysTests
{
    private GameObject? _canvasGo;
    private string _helpText = "";
    private bool _measuringWasOn;

    [SetUp]
    public void SetUp()
    {
        _measuringWasOn = PerfMonitor.Enabled;

        _canvasGo = new GameObject("HelpHotkeyTestCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();
        _canvasGo.AddComponent<HelpUI>().Build(canvas.transform);

        var body = _canvasGo.transform.Find("HelpPanel/HelpText");
        Assert.IsNotNull(body, "справка обязана построить свой текст — иначе проверять нечего");
        _helpText = body!.GetComponent<TextMeshProUGUI>().text;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_canvasGo!);
        PerfMonitor.Enabled = _measuringWasOn;
    }

    private static PerfMonitor NewMonitor(GameObject host)
    {
        var monitor = host.AddComponent<PerfMonitor>();
        monitor.SimulateAwakeForTests();
        return monitor;
    }

    [Test]
    public void Help_SaysF9TurnsTheMeasurementOn_AndF9ReallyTogglesIt()
    {
        StringAssert.Contains("F9 ", _helpText, "справка обязана называть клавишу замера");

        var host = new GameObject("PerfHost");
        try
        {
            var monitor = NewMonitor(host);
            PerfMonitor.Enabled = false;

            monitor.SimulateF9ForTests();
            Assert.IsTrue(PerfMonitor.Enabled, "F9 без Shift включает замер — так написано в справке");

            monitor.SimulateF9ForTests();
            Assert.IsFalse(PerfMonitor.Enabled, "и вторым нажатием выключает");

            monitor.SimulateOnDestroyForTests();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    /// <summary>Shift+F9 — не «то же самое с Shift»: он ВКЛЮЧАЕТ замер, если тот был
    /// выключен, и никогда его не выключает. Если бы справка описала его как второй
    /// переключатель, пользователь жал бы его на включённом замере и терял бы запись.</summary>
    [Test]
    public void Help_SaysShiftF9StartsTheRecording_AndShiftF9NeverTurnsTheMeasurementOff()
    {
        StringAssert.Contains("Shift + F9", _helpText,
            "запись CSV висит именно на Shift+F9 — справка обязана назвать сочетание целиком");

        var host = new GameObject("PerfHost");
        try
        {
            var monitor = NewMonitor(host);
            PerfMonitor.Enabled = false;

            monitor.SimulateF9ForTests(shiftHeld: true);
            Assert.IsTrue(PerfMonitor.Enabled,
                "запись без замера невозможна: Shift+F9 поднимает замер сам");

            monitor.SimulateF9ForTests(shiftHeld: true);
            Assert.IsTrue(PerfMonitor.Enabled,
                "остановка записи не выключает замер — иначе HUD пропадал бы вместе с файлом");

            monitor.SimulateOnDestroyForTests();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    /// <summary>Путь из справки — обещание, по которому пользователь идёт искать файл.
    /// Сверяется он с тем путём, который лог реально возвращает, а не с константой рядом.</summary>
    [Test]
    public void Help_NamesTheFolderAndTheFileName_TheCsvLogReallyWrites()
    {
        StringAssert.Contains("test-results/perf/perf_", _helpText,
            "справка обязана сказать, ГДЕ искать файл и как он называется");

        var log = new PerfCsvLog(new[] { "frame", "dt_ms" }, 4);
        log.Start();
        log.Append(new[] { 1f, 16.7f });

        var path = log.Stop();

        Assert.IsNotNull(path, "строка с данными обязана попасть в файл");
        try
        {
            var file = new FileInfo(path!);
            Assert.AreEqual("perf", file.Directory!.Name);
            Assert.AreEqual("test-results", file.Directory!.Parent!.Name);
            StringAssert.StartsWith("perf_", file.Name);
            StringAssert.EndsWith(".csv", file.Name);
        }
        finally
        {
            File.Delete(path!);
        }
    }
}
