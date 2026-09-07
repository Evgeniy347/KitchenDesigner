using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Поведение статус-бара: очередь, время жизни, минимальный таймаут,
/// пустая строка очищает всё. Тесты не строят реальный Canvas — только state
/// через публичный API и перемотку <see cref="StatusBarUI.SetTimeProvider"/>.</summary>
public class StatusBarUITests
{
    private GameObject _go = null!;
    private StatusBarUI _bar = null!;
    private float _fakeTime;

    [SetUp]
    public void SetUp()
    {
        _fakeTime = 0f;
        StatusBarUI.SetTimeProvider(() => _fakeTime);
        _go = new GameObject("StatusBarTest");
        _bar = _go.AddComponent<StatusBarUI>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        StatusBarUI.ResetTimeProvider();
        if (ReferenceEquals(StatusBarUI.Instance, _bar))
            StatusBarUI.SetTimeProvider(() => Time.unscaledTime);
    }

    [Test]
    public void ShowTransient_FirstMessage_BecomesActive()
    {
        _bar.ShowTransient("hello", StatusLevel.Info, 5f);

        Assert.IsTrue(_bar.HasActive);
        Assert.AreEqual("hello", _bar.ActiveText);
        Assert.AreEqual(0, _bar.QueuedCount);
    }

    [Test]
    public void ShowTransient_SecondWhileActive_GoesToQueue()
    {
        _bar.ShowTransient("first", StatusLevel.Info, 5f);
        _bar.ShowTransient("second", StatusLevel.Info, 5f);

        Assert.IsTrue(_bar.HasActive);
        Assert.AreEqual("first", _bar.ActiveText);
        Assert.AreEqual(1, _bar.QueuedCount);
    }

    [Test]
    public void ActiveExpires_AfterTimeout_PopsNextFromQueue()
    {
        _bar.ShowTransient("first", StatusLevel.Info, 3f);
        _bar.ShowTransient("second", StatusLevel.Info, 3f);

        _fakeTime = 4f; // > 3s
        _bar.Tick();

        Assert.IsTrue(_bar.HasActive);
        Assert.AreEqual("second", _bar.ActiveText);
        Assert.AreEqual(0, _bar.QueuedCount);
    }

    [Test]
    public void ActiveExpires_QueueEmpty_GoesIdle()
    {
        _bar.ShowTransient("only", StatusLevel.Info, 3f);

        _fakeTime = 4f;
        _bar.Tick();

        Assert.IsFalse(_bar.HasActive);
        Assert.AreEqual(0, _bar.QueuedCount);
    }

    [Test]
    public void ActiveNotYetExpired_StaysActive()
    {
        _bar.ShowTransient("first", StatusLevel.Info, 3f);
        _bar.ShowTransient("second", StatusLevel.Info, 3f);

        _fakeTime = 2f; // < 3s
        _bar.Tick();

        Assert.IsTrue(_bar.HasActive);
        Assert.AreEqual("first", _bar.ActiveText);
        Assert.AreEqual(1, _bar.QueuedCount);
    }

    [Test]
    public void EmptyText_ClearsActiveAndQueue()
    {
        _bar.ShowTransient("first", StatusLevel.Info, 3f);
        _bar.ShowTransient("second", StatusLevel.Info, 3f);

        _bar.ShowTransient("", StatusLevel.Info, 3f);

        Assert.IsFalse(_bar.HasActive);
        Assert.AreEqual(0, _bar.QueuedCount);
    }

    [Test]
    public void BelowMinTimeout_ClampedToMin()
    {
        // 0.5f явно меньше MinSeconds (3f) — должно клампиться.
        _bar.ShowTransient("snap", StatusLevel.Info, 0.5f);

        Assert.IsTrue(_bar.HasActive);
        // Активное должно жить как минимум MinSeconds от nowProvider.
        Assert.GreaterOrEqual(_bar.QueuedCount, 0);
        // Через 2.9s всё ещё активно.
        _fakeTime = 2.9f;
        _bar.Tick();
        Assert.IsTrue(_bar.HasActive, "below min: still active at 2.9s");
        // Через 3.1s уже истекло.
        _fakeTime = 3.1f;
        _bar.Tick();
        Assert.IsFalse(_bar.HasActive, "below min: expired after 3.1s");
    }

    [Test]
    public void SameTextAndLevelWhileActive_JustProlongs_NoQueue()
    {
        _bar.ShowTransient("snap off", StatusLevel.Info, 3f);
        _bar.ShowTransient("snap off", StatusLevel.Info, 3f); // тот же — продлеваем
        _bar.ShowTransient("snap off", StatusLevel.Info, 3f);

        Assert.AreEqual(0, _bar.QueuedCount, "no queue duplication for same text");
    }

    [Test]
    public void SameTextButAnotherLevel_IsANewMessage_AndQueues()
    {
        _bar.ShowTransient("Сохранено", StatusLevel.Info, 3f);
        _bar.ShowTransient("Сохранено", StatusLevel.Error, 3f);

        Assert.AreEqual(1, _bar.QueuedCount,
            "«продлеваем то же самое» спрашивает про ТЕКСТ И УРОВЕНЬ. Один только текст "
            + "склеил бы «сохранено» с «сохранить не удалось», если они когда-нибудь "
            + "совпадут словами, — и человек увидел бы зелёное вместо красного");
    }

    [Test]
    public void Queue_FIFO_Order()
    {
        _bar.ShowTransient("a", StatusLevel.Info, 3f);
        _bar.ShowTransient("b", StatusLevel.Info, 3f);
        _bar.ShowTransient("c", StatusLevel.Info, 3f);

        Assert.AreEqual("a", _bar.ActiveText);
        Assert.AreEqual(2, _bar.QueuedCount);

        _fakeTime = 4f;
        _bar.Tick();
        Assert.AreEqual("b", _bar.ActiveText);

        _fakeTime = 8f;
        _bar.Tick();
        Assert.AreEqual("c", _bar.ActiveText);

        _fakeTime = 12f;
        _bar.Tick();
        Assert.IsFalse(_bar.HasActive);
    }

    [Test]
    public void MinSeconds_IsAtLeastThree()
    {
        // Контракт API: минимальное время показа >= 3 секунды, иначе мерцает.
        Assert.GreaterOrEqual(StatusBarUI.MinSeconds, 3f);
        Assert.Greater(StatusBarUI.DefaultSeconds, StatusBarUI.MinSeconds,
            "default must be strictly greater than min so even a caller that omits the argument gets >3s");
    }
}