#nullable disable
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Окна автообновления собираются кодом; проверяем, что они построились,
/// переключают видимость, подставляют версию в текст и что кнопки дёргают нужные
/// колбэки. Сравниваем текст с рантайм-константами UpdateStrings (не литералами),
/// чтобы не зависеть от кодировки исходника. Реальных файлов/процессов нет.</summary>
public class UpdateDialogUiTests
{
    private Canvas _canvas;

    [SetUp]
    public void SetUp()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("UpdateUiTestCanvas");
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
    }

    [Test]
    public void UpdateDialog_Build_StartsHidden()
    {
        var go = new GameObject("d");
        var dlg = go.AddComponent<UpdateDialogUI>();
        dlg.Build(_canvas.transform);
        Assert.IsFalse(dlg.IsVisible);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void UpdateDialog_Show_ThenButtonsInvokeCallbacks()
    {
        var go = new GameObject("d");
        var dlg = go.AddComponent<UpdateDialogUI>();
        dlg.Build(_canvas.transform);

        bool update = false, cancel = false;
        dlg.ShowUpdateAvailable("0.700", () => update = true, () => cancel = true);

        Assert.IsTrue(dlg.IsVisible);
        StringAssert.Contains("0.700", dlg.MessageText);

        dlg.UpdateButton.onClick.Invoke();
        Assert.IsTrue(update);

        dlg.ShowUpdateAvailable("0.701", () => update = true, () => cancel = true);
        dlg.CancelButton.onClick.Invoke();
        Assert.IsTrue(cancel);

        dlg.Hide();
        Assert.IsFalse(dlg.IsVisible);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void UpdateDialog_ShowsTitle_WithVersionInMessage()
    {
        var go = new GameObject("d");
        var dlg = go.AddComponent<UpdateDialogUI>();
        dlg.Build(_canvas.transform);
        dlg.ShowUpdateAvailable("9.9.9", () => { }, () => { });

        // Текст сообщения должен включать номер версии (подстановка {0}).
        Assert.IsNotNull(dlg.MessageText);
        Assert.That(dlg.MessageText, Does.Contain("9.9.9"));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DownloadDialog_Show_SetsVersionTextAndProgress_HideAndCancel()
    {
        var go = new GameObject("dl");
        var dlg = go.AddComponent<DownloadProgressUI>();
        dlg.Build(_canvas.transform);

        Assert.IsFalse(dlg.IsVisible);

        bool cancel = false;
        dlg.ShowDownloading("0.700", () => cancel = true);
        Assert.IsTrue(dlg.IsVisible);
        Assert.That(dlg.MessageText, Does.Contain("0.700"));
        Assert.AreEqual(0f, dlg.Progress);

        dlg.SetProgress(0.4f);
        Assert.AreEqual(0.4f, dlg.Progress, 0.001f);
        dlg.SetProgress(2f);
        Assert.AreEqual(1f, dlg.Progress, 0.001f);

        dlg.CancelButton.onClick.Invoke();
        Assert.IsTrue(cancel);

        dlg.Hide();
        Assert.IsFalse(dlg.IsVisible);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DownloadDialog_Message_DoesNotOverlapTitle()
    {
        var go = new GameObject("dl");
        var dlg = go.AddComponent<DownloadProgressUI>();
        dlg.Build(_canvas.transform);

        // Сообщение должно начинаться ниже прямоугольника заголовка: раньше
        // оно заезжало на заголовок (текст «Загружаем…» перекрывал «Установка
        // обновления»). rect у каждого элемента локальный (свой пивот), поэтому
        // углы переводим в мировые координаты и сравниваем уже их.
        Assert.IsNotNull(dlg.TitleRect);
        Assert.IsNotNull(dlg.MessageRect);
        float titleBottom = dlg.TitleRect.TransformPoint(new Vector3(0f, dlg.TitleRect.rect.yMin, 0f)).y;
        float messageTop = dlg.MessageRect.TransformPoint(new Vector3(0f, dlg.MessageRect.rect.yMax, 0f)).y;
        Assert.LessOrEqual(messageTop, titleBottom);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DownloadDialog_TitleMatchesUpdateStrings()
    {
        var go = new GameObject("dl");
        var dlg = go.AddComponent<DownloadProgressUI>();
        dlg.Build(_canvas.transform);
        dlg.ShowDownloading("1.2.3", () => { });

        // В тексте окна обязано быть предупреждение про авто-перезапуск. Берём
        // вторую фразу из константы (она без {0}, поэтому попадает в форматированный
        // текст дословно) и сверяем — без захардкоженной кириллицы в исходнике.
        var restartSentence = UpdateStrings.DownloadMessage
            .Split(new[] { "\n\n" }, 2, System.StringSplitOptions.None)[1];
        Assert.That(dlg.MessageText, Does.Contain(restartSentence));
        Object.DestroyImmediate(go);
    }
}
