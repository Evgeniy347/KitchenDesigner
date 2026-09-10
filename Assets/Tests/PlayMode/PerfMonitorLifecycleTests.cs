using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class PerfMonitorLifecycleTests
{
    private GameObject? _host;
    private bool _enabledBefore;

    [SetUp]
    public void SetUp() => _enabledBefore = PerfMonitor.Enabled;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (PerfMonitor.Instance != null) PerfMonitor.Instance.SetCsvRecording(false);
        PerfMonitor.Enabled = _enabledBefore;
        if (_host != null) Object.DestroyImmediate(_host);
        _host = null;
        yield return null;
    }

    private IEnumerator Spawn()
    {
        PerfMonitor.Enabled = false;
        _host = new GameObject("PerfMonitorUnderTest");
        _host.AddComponent<PerfMonitor>();
        yield return null;
    }

    [UnityTest]
    public IEnumerator FreshMonitor_StaysOff_SoItDoesNotFloodTheLogOfEveryPlayModeTest()
    {
        yield return Spawn();

        Assert.IsFalse(PerfMonitor.Enabled,
            "Bootstrap поднимает PerfMonitor в КАЖДОМ PlayMode-тесте; включённый по "
            + "умолчанию замер сыпал бы дамп в лог каждые 120 кадров и топил бы в нём "
            + "настоящие ошибки прогона");
        Assert.AreEqual("", PerfMonitor.Instance!.HudText,
            "пока замер выключен, HUD-строки нет — и оверлею нечего рисовать");
    }

    [UnityTest]
    public IEnumerator Monitor_BringsItsOwnHud_SoTheOverlayNeedsNoSceneWiring()
    {
        yield return Spawn();

        Assert.IsNotNull(_host!.GetComponent<PerfHud>(),
            "оверлей — часть инструмента, а не сцены: иначе он существовал бы только "
            + "там, где кто-то не забыл добавить компонент");
    }

    [UnityTest]
    public IEnumerator StartingCsvRecording_TurnsMeasurementOnToo_BecauseRowsWithoutSamplingStayEmpty()
    {
        yield return Spawn();
        Assume.That(PerfMonitor.Enabled, Is.False);

        PerfMonitor.Instance!.SetCsvRecording(true);

        Assert.IsTrue(PerfMonitor.Enabled,
            "строки CSV пишет Sample(), а Sample() зовут только при включённом замере: "
            + "запись без замера дала бы пустой файл и молчаливое «всё записалось»");
    }

    [UnityTest]
    public IEnumerator StoppingCsvRecording_ThatWasNeverStarted_ReturnsNoPath()
    {
        yield return Spawn();

        Assert.IsNull(PerfMonitor.Instance!.SetCsvRecording(false),
            "нечего сохранять — нечего и возвращать: путь к несуществующему файлу "
            + "профилировочный прогон принял бы за успешную запись");
    }

    // F9 is a straight key-read in production (HandleHotkeys -> Input.GetKeyDown), which cannot
    // be driven from a test. ApplyF9 is the same call with the key-read factored out — exactly
    // the split SidebarUI.HandleKey/SimulateKeyForTests already uses for Escape and the slash
    // shortcut — so these tests exercise the real toggle path, not a stand-in for it.

    [UnityTest]
    public IEnumerator PressingF9_MakesTheWindowVisible_JudgedByTheSameCheckTheOverlayUses()
    {
        yield return Spawn();
        Assume.That(WindowWouldBeVisible(), Is.False,
            "до F9 замер выключен — окну ещё нечего показывать");

        PerfMonitor.Instance!.ApplyF9(shiftHeld: false);
        yield return null; // one Update(): Sample() builds the HUD line now that Enabled is true

        Assert.IsTrue(WindowWouldBeVisible(),
            "F9 обязан не просто выставить флаг, а довести дело до видимого окна: "
            + "PerfHud рисует только когда Enabled=true И HudText непуст");
    }

    [UnityTest]
    public IEnumerator PressingF9Twice_HidesTheWindowAgain()
    {
        yield return Spawn();
        PerfMonitor.Instance!.ApplyF9(shiftHeld: false);
        yield return null;
        Assume.That(WindowWouldBeVisible(), Is.True,
            "первый F9 обязан был уже открыть окно — иначе второй нажатие ничего не проверяет");

        PerfMonitor.Instance!.ApplyF9(shiftHeld: false);
        yield return null;

        Assert.IsFalse(WindowWouldBeVisible(),
            "F9 переключает: второе нажатие обязано снова спрятать окно, а не оставить "
            + "его висеть с застывшими цифрами");
    }

    [UnityTest]
    public IEnumerator PressingShiftF9_AlsoOpensTheWindow_ButAdditionallyStartsCsvRecording()
    {
        yield return Spawn();
        Assume.That(WindowWouldBeVisible(), Is.False);

        PerfMonitor.Instance!.ApplyF9(shiftHeld: true);
        yield return null;

        Assert.IsTrue(WindowWouldBeVisible(),
            "Shift+F9 запускает запись CSV, а SetCsvRecording(true) заодно включает и сам "
            + "замер — окно обязано появиться так же, как от обычного F9");
        StringAssert.Contains("запись CSV", PerfMonitor.Instance!.HudText,
            "в этом и разница между F9 и Shift+F9: не будь она видна в самом HUD, "
            + "оба нажатия выглядели бы для пользователя одинаково");
    }

    private static bool WindowWouldBeVisible() =>
        PerfHud.ShouldPaint(EventType.Repaint, PerfMonitor.Enabled, PerfMonitor.Instance!.HudText);
}
