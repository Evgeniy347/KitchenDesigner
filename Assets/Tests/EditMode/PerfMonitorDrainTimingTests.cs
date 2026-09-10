using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Дефект D5. Профайлер сливал показания маркеров в `Update`, а треть маркеров
/// работает в `LateUpdate`: `WallManager.LateUpdate`, `SceneChangeTracker.Poll`,
/// `ElementOutline`, `ResizeHandleManager`, `MeasureLabelsUI`, `EdgeSubstrate` копили такты
/// уже ПОСЛЕ слива и попадали в следующий кадр. «Худший кадр» поэтому сводил `dt` одного
/// кадра со временем маркеров другого — ровно ту связь, ради которой дамп и существует.
///
/// Тесты проверяют ПОРЯДОК, а не миллисекунды: такты, накопленные после `Update`, обязаны
/// попасть в замер ЭТОГО кадра, а сам слив обязан идти последним в кадре — за это отвечает
/// `DefaultExecutionOrder`, и его значение сторожится здесь же, чтобы новый компонент с
/// большим порядком не оказался ПОСЛЕ слива.</summary>
public class PerfMonitorDrainTimingTests
{
    private const string AMarkerThatRunsInLateUpdate = "SceneChangeTracker.Poll";

    private GameObject? _host;
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
        if (_host != null) Object.DestroyImmediate(_host);
        _host = null;
        PerfMonitor.Enabled = _enabledBefore;
        PerfMarkers.DropEverythingMeasuredSoFar();
    }

    private static int SlotOf(string markerName)
    {
        var names = PerfMarkers.NamesInDeclarationOrder;
        for (int i = 0; i < names.Count; i++)
            if (names[i] == markerName) return i;
        return -1;
    }

    private PerfMonitor MakeMonitor()
    {
        _host = new GameObject("PerfMonitor сторожа");
        var monitor = _host.AddComponent<PerfMonitor>();
        PerfMonitor.Enabled = true;
        return monitor;
    }

    /// <summary>Пять миллисекунд — заведомо выше порога показа (0,05 мс), поэтому маркер
    /// обязан появиться в тексте HUD, если его такты попали в замер этого кадра.</summary>
    private static long FiveMillisecondsInTicks => (long)(Stopwatch.Frequency * 0.005);

    [Test]
    public void TicksAccumulatedAfterUpdate_LandInTheSampleOfTheSameFrame()
    {
        int slot = SlotOf(AMarkerThatRunsInLateUpdate);
        Assert.GreaterOrEqual(slot, 0, $"маркера {AMarkerThatRunsInLateUpdate} больше нет — "
            + "тест обязан ссылаться на существующий маркер, иначе он не проверяет ничего");

        var monitor = MakeMonitor();

        PerfMarkers.AddTicks(slot, FiveMillisecondsInTicks);
        monitor.SimulateLateUpdateForTests();

        Assert.AreEqual(0f, PerfMarkers.TakeFrameMs(slot), 1e-6f,
            "слив обязан был забрать такты — иначе они удвоятся в следующем кадре");
        StringAssert.Contains(AMarkerThatRunsInLateUpdate, monitor.HudText,
            "маркер, закрывшийся в LateUpdate, обязан попасть в замер ЭТОГО кадра. Пока слив "
            + "жил в Update, его такты доставались следующему кадру — и «худший кадр» сводил "
            + "dt одного кадра с маркерами другого");
    }

    /// <summary>Отрицательный контроль: `Update` не имеет права сливать. Без него зелёный
    /// выше был бы совместим и со сливом в обоих местах — то есть с двойным счётом.</summary>
    [Test]
    public void Update_DoesNothingButHotkeys_SoTheFrameIsDrainedExactlyOnce()
    {
        var path = Path.Combine(
            RepoPaths.Subdir("Assets", "Scripts", "Core", "Diagnostics"), "PerfMonitor.cs");
        Assert.IsTrue(File.Exists(path), $"сканер смотрит не туда — не найден {path}");
        var source = File.ReadAllText(path);

        StringAssert.Contains("private void Update() => HandleHotkeys();", source,
            "слив в кадре ровно один, и он последний: Update оставлен только под горячие "
            + "клавиши. Второй слив — это двойной счёт, ноль вместо замера в одном из мест");
        StringAssert.Contains("private void LateUpdate()", source,
            "замер снимается в LateUpdate — иначе треть маркеров снова достаётся "
            + "следующему кадру");
    }

    /// <summary>Порядок исполнения — несущая часть правки, а не украшение: если чей-то
    /// `LateUpdate` окажется ПОСЛЕ слива, дефект вернётся ровно в том же виде.</summary>
    [Test]
    public void PerfMonitor_RunsAfterEveryOtherOrderedComponent()
    {
        var mine = typeof(PerfMonitor).GetCustomAttribute<DefaultExecutionOrder>();
        Assert.IsNotNull(mine,
            "без DefaultExecutionOrder порядок LateUpdate между компонентами не определён — "
            + "слив может снова оказаться раньше половины маркеров");

        var later = new List<string>();
        foreach (var type in typeof(PerfMonitor).Assembly.GetTypes())
        {
            if (type == typeof(PerfMonitor)) continue;
            if (!typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
            var order = type.GetCustomAttribute<DefaultExecutionOrder>();
            if (order != null && order.order >= mine!.order) later.Add($"{type.Name}={order.order}");
        }

        CollectionAssert.IsEmpty(later,
            "эти компоненты исполняются не раньше профайлера, значит их маркеры копятся уже "
            + "после слива: " + string.Join(", ", later));
    }
}
