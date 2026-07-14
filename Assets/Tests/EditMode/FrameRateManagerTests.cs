using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Логика «окна бодрствования» динамического FPS: активность держит активный FPS
/// заданное время, затем менеджер уходит в простой (idle FPS). Время передаётся
/// явно, поэтому тесты не зависят от Time.unscaledTime и жизненного цикла Awake.
/// </summary>
public class FrameRateManagerTests
{
    private GameObject _go = null!;
    private FrameRateManager _fr = null!;
    private int _prevTargetFps;

    [SetUp]
    public void Setup()
    {
        // OnEnable менеджера трогает глобальный Application.targetFrameRate — сохраняем.
        _prevTargetFps = Application.targetFrameRate;
        _go = new GameObject("FR");
        _fr = _go.AddComponent<FrameRateManager>();
        // Фиксируем значения, чтобы тест не зависел от платформенных дефолтов.
        _fr.ActiveFps = 30;
        _fr.IdleFps = 1;
    }

    [TearDown]
    public void Teardown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        Application.targetFrameRate = _prevTargetFps;
    }

    [Test]
    public void FreshInstance_IsIdle()
    {
        Assert.IsFalse(_fr.IsAwake(0f));
        Assert.AreEqual(1, _fr.TargetFpsAt(0f), "без активности — idle FPS");
    }

    [Test]
    public void MarkActive_AwakeWithinGrace_ThenSleeps()
    {
        _fr.MarkActive(100f, 0.7f);

        Assert.IsTrue(_fr.IsAwake(100.5f), "в пределах grace — бодрствует");
        Assert.IsFalse(_fr.IsAwake(100.7f), "на границе grace — уже спит");
        Assert.IsFalse(_fr.IsAwake(101f), "после grace — спит");
    }

    [Test]
    public void TargetFpsAt_ReflectsAwakeState()
    {
        _fr.MarkActive(100f, 0.7f);

        Assert.AreEqual(30, _fr.TargetFpsAt(100.3f), "активность → активный FPS");
        Assert.AreEqual(1, _fr.TargetFpsAt(101f), "простой → idle FPS");
    }

    [Test]
    public void MarkActive_DoesNotShrinkWindow()
    {
        _fr.MarkActive(100f, 0.7f);      // окно до 100.7
        _fr.MarkActive(100.1f, 0.1f);    // окно до 100.2 — короче, не должно сокращать

        Assert.IsTrue(_fr.IsAwake(100.5f), "более раннее короткое окно не сокращает бодрствование");
        Assert.IsFalse(_fr.IsAwake(100.7f));
    }

    [Test]
    public void MarkActive_ExtendsWindow()
    {
        _fr.MarkActive(100f, 0.7f);      // окно до 100.7
        _fr.MarkActive(100.5f, 0.7f);    // окно до 101.2 — продлевает

        Assert.IsTrue(_fr.IsAwake(101f), "новая активность продлевает окно");
        Assert.IsFalse(_fr.IsAwake(101.2f));
    }

    [Test]
    public void MarkActive_NegativeGrace_ClampedToZero()
    {
        Assert.DoesNotThrow(() => _fr.MarkActive(100f, -5f));
        Assert.IsFalse(_fr.IsAwake(100f), "отрицательный grace обрезается до 0 — сразу простой");
    }

    [Test]
    public void KeepAwake_NoInstance_DoesNotThrow()
    {
        Object.DestroyImmediate(_go);
        _go = null!;
        // Instance сброшен в OnDestroy — статический вызов должен быть безопасным.
        Assert.DoesNotThrow(() => FrameRateManager.KeepAwake(1f));
    }
}
