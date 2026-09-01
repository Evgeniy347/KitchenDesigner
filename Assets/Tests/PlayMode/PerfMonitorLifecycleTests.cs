#if UNITY_EDITOR || DEVELOPMENT_BUILD

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
}

#endif
