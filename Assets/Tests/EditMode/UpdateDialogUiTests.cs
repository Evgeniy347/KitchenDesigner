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

    [Test]
    public void DownloadDialog_ShowRetry_NamesTheAttemptAndIsClearedByTheNextDownload()
    {
        var go = new GameObject("dl");
        var dlg = go.AddComponent<DownloadProgressUI>();
        dlg.Build(_canvas.transform);

        dlg.ShowDownloading("0.700", () => { });
        Assert.IsEmpty(dlg.RetryText,
            "строка повтора при обычной загрузке пуста: «повторная попытка» на "
            + "первой же попытке сообщает о сбое, которого не было");

        dlg.SetProgress(0.6f);
        dlg.ShowRetry(2, 3);

        Assert.That(dlg.RetryText, Does.Contain("2").And.Contain("3"),
            "оба числа обязаны доехать до окна: «повторная попытка 2» без «из 3» "
            + "не говорит пользователю, сколько ещё ждать");
        Assert.AreEqual(0f, dlg.Progress, 0.001f,
            "новая попытка качает файл с нуля — полоса, оставшаяся на 60%, "
            + "показывала бы прогресс уже оборванной загрузки");

        dlg.ShowDownloading("0.701", () => { });
        Assert.IsEmpty(dlg.RetryText,
            "следующее обновление начинается с чистого окна: строка повтора, "
            + "пережившая закрытие, врёт про совершенно другую загрузку");
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DownloadDialog_RetryLine_SitsBetweenTheProgressBarAndTheCancelButton()
    {
        var go = new GameObject("dl");
        var dlg = go.AddComponent<DownloadProgressUI>();
        dlg.Build(_canvas.transform);
        dlg.ShowDownloading("0.700", () => { });
        dlg.ShowRetry(3, 3);

        Assert.IsNotNull(dlg.RetryRect, "строки повтора нет вовсе");
        Assert.IsNotNull(dlg.CancelRect, "кнопки отмены нет вовсе");
        float retryBottom = dlg.RetryRect.TransformPoint(new Vector3(0f, dlg.RetryRect.rect.yMin, 0f)).y;
        float cancelTop = dlg.CancelRect.TransformPoint(new Vector3(0f, dlg.CancelRect.rect.yMax, 0f)).y;

        Assert.GreaterOrEqual(retryBottom, cancelTop,
            "строка добавлена в окно фиксированной высоты: наехав на кнопку, она "
            + "перекрывает единственный способ прервать загрузку");
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DownloadDialog_ProgressBar_CannotBeDraggedByTheUser()
    {
        var go = new GameObject("dl");
        var dlg = go.AddComponent<DownloadProgressUI>();
        dlg.Build(_canvas.transform);
        dlg.ShowDownloading("1.2.3", () => { });
        dlg.SetProgress(0.25f);

        Assert.IsFalse(dlg.ProgressBarIsInteractive,
            "полоса только ПОКАЗЫВАЕТ ход загрузки. Интерактивный Slider ловит "
            + "перетаскивание и переписывает value: пользователь двигает ползунок, "
            + "прогресс перестаёт отражать закачку, а следующий SetProgress дёргает "
            + "его назад");
        Assert.AreEqual(0.25f, dlg.Progress, 0.001f);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void BothDialogs_Backdrop_CoversTheScreenAndSwallowsClicks()
    {
        var updateGo = new GameObject("d");
        var update = updateGo.AddComponent<UpdateDialogUI>();
        update.Build(_canvas.transform);

        var downloadGo = new GameObject("dl");
        var download = downloadGo.AddComponent<DownloadProgressUI>();
        download.Build(_canvas.transform);

        AssertModalBackdrop(update.BackdropRect, "UpdateDialogUI");
        AssertModalBackdrop(download.BackdropRect, "DownloadProgressUI");

        Object.DestroyImmediate(updateGo);
        Object.DestroyImmediate(downloadGo);
    }

    private static void AssertModalBackdrop(RectTransform backdrop, string owner)
    {
        Assert.IsNotNull(backdrop, owner + ": подложки нет вовсе");
        Assert.AreEqual(Vector2.zero, backdrop.anchorMin, owner + ": подложка не от угла");
        Assert.AreEqual(Vector2.one, backdrop.anchorMax, owner + ": подложка не до угла");
        Assert.AreEqual(Vector2.zero, backdrop.offsetMin, owner + ": подложка не во весь экран");
        Assert.AreEqual(Vector2.zero, backdrop.offsetMax, owner + ": подложка не во весь экран");

        var image = backdrop.GetComponent<UnityEngine.UI.Image>();
        Assert.IsNotNull(image, owner + ": подложке нужен Graphic, иначе она не ловит лучи");
        Assert.IsTrue(image.raycastTarget,
            owner + ": окно модальное. Подложка без raycastTarget пропускает клики "
            + "насквозь, и пользователь двигает мебель, пока диалог висит поверх — "
            + "а сцена под ним уже уезжает в обновление");
        Assert.Greater(image.color.a, 0f,
            owner + ": затемнение — единственный видимый признак, что окно модальное");
    }
}
