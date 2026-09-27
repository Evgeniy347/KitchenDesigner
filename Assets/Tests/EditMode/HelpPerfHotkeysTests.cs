using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>Справка F1 раньше была единственным местом, где пользователь узнавал про
/// профилировщик, и рукописной копией того, что умеет <c>PerfMonitor</c>. Она ушла на
/// вкладку «Управление»: саму клавишу теперь называет строка привязки (её меняют, и текст
/// не отстаёт, потому что это не текст, а форматирование живого <c>KeyChord</c>), а путь
/// к файлу записи — единственное, что строка привязки не показывает, — переехал в
/// подсказку «i» этой строки. Тест по-прежнему сверяет описание с РЕАЛЬНЫМ поведением
/// <c>PerfMonitor</c>, а не с памятью автора.</summary>
public class HelpPerfHotkeysTests
{
    private bool _measuringWasOn;

    [SetUp]
    public void SetUp() => _measuringWasOn = PerfMonitor.Enabled;

    [TearDown]
    public void TearDown() => PerfMonitor.Enabled = _measuringWasOn;

    private static PerfMonitor NewMonitor(GameObject host)
    {
        var monitor = host.AddComponent<PerfMonitor>();
        monitor.SimulateAwakeForTests();
        return monitor;
    }

    [Test]
    public void PerfMonitorToggle_DefaultsToF9_AndF9ReallyTogglesIt()
    {
        Assert.AreEqual("F9",
            InputBinding.Format(KeyBindingDefaults.PrimaryOf(InputAction.PerfMonitorToggle)),
            "строка привязки называет клавишу замера сама, по умолчанию — просто F9");

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
    public void PerfMonitorToggleRecording_DefaultsToShiftF9_AndNeverTurnsTheMeasurementOff()
    {
        Assert.AreEqual("Shift+F9",
            InputBinding.Format(KeyBindingDefaults.PrimaryOf(InputAction.PerfMonitorToggleRecording)),
            "запись CSV висит именно на Shift+F9 по умолчанию");

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
    public void PerfRecordingHint_NamesTheFolderAndTheFileName_TheCsvLogReallyWrites()
    {
        string hint = HintText.Of("settings.control.perfRecordingFile");
        StringAssert.Contains("test-results/perf/perf_", hint,
            "подсказка обязана сказать, ГДЕ искать файл и как он называется");

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

    [Test]
    public void Hud_ShowsTheRecordingHint_WhileNotRecording()
    {
        var host = new GameObject("PerfHost");
        try
        {
            var monitor = NewMonitor(host);
            PerfMonitor.Enabled = true;
            monitor.SimulateLateUpdateForTests();

            StringAssert.Contains(PerfMonitor.RecordHintLine, monitor.HudText,
                "пока запись не идёт, оверлей обязан коротко напоминать, как её начать");

            monitor.SimulateOnDestroyForTests();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Hud_ShowsThatRecordingIsInProgress_InsteadOfTheHint()
    {
        var host = new GameObject("PerfHost");
        try
        {
            var monitor = NewMonitor(host);
            PerfMonitor.Enabled = true;
            monitor.SetCsvRecording(true);
            monitor.SimulateLateUpdateForTests();

            StringAssert.Contains("запись CSV идёт", monitor.HudText,
                "во время записи оверлей обязан сказать, что она идёт, а не показывать "
                + "общую подсказку про Shift+F9");
            StringAssert.DoesNotContain(PerfMonitor.RecordHintLine, monitor.HudText);

            string? path = monitor.SetCsvRecording(false);
            if (path != null) File.Delete(path);
            monitor.SimulateOnDestroyForTests();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    /// <summary>Путь печатается в консоль (Debug.Log, значит и в Player.log) и обязан
    /// быть реальным, существующим файлом — не только строкой. Тот же путь на несколько
    /// секунд появляется и в самом оверлее, чтобы не заставлять искать его в логе.</summary>
    [Test]
    public void StoppingTheRecording_LogsAnAbsolutePathThatExists_AndShowsItInTheHudBriefly()
    {
        var host = new GameObject("PerfHost");
        try
        {
            var monitor = NewMonitor(host);
            PerfMonitor.Enabled = true;
            monitor.SetCsvRecording(true);
            monitor.SimulateLateUpdateForTests();
            monitor.SimulateLateUpdateForTests();

            string? path = monitor.SetCsvRecording(false);

            Assert.IsNotNull(path, "хотя бы один кадр обязан был записан за два тика LateUpdate");
            Assert.IsTrue(Path.IsPathRooted(path), "в лог и в оверлей обязан идти АБСОЛЮТНЫЙ путь");
            Assert.IsTrue(File.Exists(path), "путь из лога обязан указывать на реально существующий файл");

            monitor.ForceHudRebuildOnNextSampleForTests();
            monitor.SimulateLateUpdateForTests();
            StringAssert.Contains(path!, monitor.HudText,
                "сразу после остановки записи путь к CSV обязан появиться в самом оверлее, "
                + "а не только в консоли");

            File.Delete(path!);
            monitor.SimulateOnDestroyForTests();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
