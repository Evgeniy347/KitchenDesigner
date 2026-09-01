using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

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

    private static void CallAwake(StatusBarUI bar) =>
        bar.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);

    private static void CallOnDestroy(StatusBarUI bar) =>
        bar.SendMessage("OnDestroy", SendMessageOptions.DontRequireReceiver);

    [Test]
    public void StatusBar_OnDestroy_ReleasesTheStaticInstance()
    {
        CallAwake(_bar);
        Assume.That(StatusBarUI.Instance, Is.SameAs(_bar));

        CallOnDestroy(_bar);

        Assert.IsNull(StatusBarUI.Instance,
            "Статик обязан отпустить уничтоженный объект: иначе после смены сцены или "
            + "загрузки другого проекта первый же ShowTransient падает с "
            + "MissingReferenceException");
    }

    [Test]
    public void StatusBar_OnDestroy_OfAnOldBar_KeepsTheCurrentOne()
    {
        CallAwake(_bar);

        var freshGo = new GameObject("StatusBarFresh");
        var fresh = freshGo.AddComponent<StatusBarUI>();
        CallAwake(fresh);

        CallOnDestroy(_bar);

        Assert.AreSame(fresh, StatusBarUI.Instance,
            "сравнение идёт по ReferenceEquals: умирающий старый экземпляр не должен "
            + "обнулять текущий");
        Object.DestroyImmediate(freshGo);
    }

    [Test]
    public void StatusBar_InfiniteMessage_StaysUntilTheNextOne()
    {
        _bar.ShowTransient("подключение…", Color.white, float.PositiveInfinity);

        _fakeTime = 10_000f;
        _bar.Tick();

        Assert.AreEqual("подключение…", _bar.ActiveText,
            "seconds == +бесконечность значит «висит постоянно», а не «истекает через "
            + "минимум»: минимальный таймаут не должен подменять бесконечность");

        _bar.ShowTransient("готово", Color.white, 5f);
        _fakeTime = 20_000f;
        _bar.Tick();

        Assert.AreEqual("готово", _bar.ActiveText, "и уступает место следующему сообщению");
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
