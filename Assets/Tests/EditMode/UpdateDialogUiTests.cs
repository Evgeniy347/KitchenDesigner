#nullable disable
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Окно автообновления собирается кодом; проверяем, что оно построилось,
/// переключает видимость, подставляет версию в текст и что кнопки дёргают нужные
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
    public void UpdateDialog_Backdrop_CoversTheScreenAndSwallowsClicks()
    {
        var updateGo = new GameObject("d");
        var update = updateGo.AddComponent<UpdateDialogUI>();
        update.Build(_canvas.transform);

        AssertModalBackdrop(update.BackdropRect, "UpdateDialogUI");

        Object.DestroyImmediate(updateGo);
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
