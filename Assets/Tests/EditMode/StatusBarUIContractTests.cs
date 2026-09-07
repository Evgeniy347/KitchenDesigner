using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

public class StatusBarUIContractTests
{
    private GameObject _go = null!;
    private StatusBarUI _bar = null!;
    private float _fakeTime;

    [SetUp]
    public void SetUp()
    {
        _fakeTime = 0f;
        StatusBarUI.SetTimeProvider(() => _fakeTime);
        _go = new GameObject("StatusBarContract");
        _bar = _go.AddComponent<StatusBarUI>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        StatusBarUI.ResetTimeProvider();
    }

    [Test]
    public void StatusBar_InfiniteMessage_NeverExpires_AndHoldsTheQueueBehindIt()
    {
        _bar.ShowTransient("подключение…", StatusLevel.Info, float.PositiveInfinity);

        _fakeTime = 10_000f;
        _bar.Tick();
        Assert.AreEqual("подключение…", _bar.ActiveText,
            "seconds == +бесконечность значит «висит постоянно»: минимальный таймаут не "
            + "должен подменять бесконечность");

        _bar.ShowTransient("готово", StatusLevel.Info, 5f);
        _fakeTime = 20_000f;
        _bar.Tick();

        Assert.AreEqual("подключение…", _bar.ActiveText,
            "Следующее сообщение бесконечное НЕ вытесняет — оно встаёт в очередь за ним и "
            + "не показывается никогда. Единственный способ снять бесконечное — пустая "
            + "строка; полагаться на «постоянно до следующего ShowTransient» нельзя");
        Assert.AreEqual(1, _bar.QueuedCount, "и очередь копится за ним");

        _bar.ShowTransient("", StatusLevel.Info);
        Assert.IsFalse(_bar.HasActive, "пустая строка очищает и активное, и очередь");
        Assert.AreEqual(0, _bar.QueuedCount);
    }

    [Test]
    public void StatusBar_ChipWidth_IgnoresChangesSmallerThanHalfAPixel()
    {
        Assert.IsFalse(StatusBarUI.WidthChangedNoticeably(230f, 230.4f),
            "Ширина плашки центрируется и меняется в обе стороны, поэтому пересчёт на "
            + "доли пикселя дёргал бы раскладку каждый кадр");
        Assert.IsTrue(StatusBarUI.WidthChangedNoticeably(230f, 260f),
            "а реальное изменение ширины плашка обязана отработать");
    }
}
