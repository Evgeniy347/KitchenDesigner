using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// docs/ui-redesign/dialogs.md: у DemoModeDialogUI и NewerVersionDialogUI заголовок налезал на
// первую строку текста, а между текстом и кнопками висели 100 px пустоты — оба числа были
// константами рядом с размещением. ModalDialog считает высоту по содержимому; сторож проверяет это
// на всех десяти языках, потому что длина текста — свойство перевода, а не кода.
public class ModalDialogTests
{
    private GameObject? _canvas;
    private GameObject? _host;

    [SetUp]
    public void SetUp()
    {
        _canvas = UiTestCanvas.Create("ModalCanvas");
        _host = new GameObject("ModalHost");
    }

    [TearDown]
    public void TearDown()
    {
        LanguageStartup.PinSourceLanguageForTestRun();
        if (_host != null) Object.DestroyImmediate(_host);
        UiTestCanvas.Release(_canvas);
    }

    private static Rect RectIn(RectTransform panel, RectTransform rt)
    {
        Vector2 min = panel.InverseTransformPoint(rt.TransformPoint(rt.rect.min));
        Vector2 max = panel.InverseTransformPoint(rt.TransformPoint(rt.rect.max));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private DemoModeDialogUI ShowDemoDialog()
    {
        var ui = _host!.AddComponent<DemoModeDialogUI>();
        ui.Build(_canvas!.transform);
        ui.Show();
        Canvas.ForceUpdateCanvases();
        return ui;
    }

    [Test]
    public void DemoDialog_OnEveryLanguage_TitleNeverOverlapsTheBody_AndTheHeightIsTheContent()
    {
        var offenders = new List<string>();
        foreach (var language in Loc.Languages.Select(l => l.Code).ToList())
        {
            Loc.SetLanguage(language);
            var ui = ShowDemoDialog();
            var dialog = ui.Dialog!;
            var panel = dialog.Panel;
            var title = RectIn(panel, dialog.Title.rectTransform);
            var body = RectIn(panel, dialog.Body.rectTransform);
            var buttons = RectIn(panel, (RectTransform)dialog.PrimaryButton.transform);

            if (title.yMin < body.yMax - 0.5f) offenders.Add($"{language}: заголовок заходит на текст на {body.yMax - title.yMin:0.#} px");
            if (body.yMin < buttons.yMax + UIStyle.Space5 - 0.5f) offenders.Add($"{language}: текст ближе {UIStyle.Space5} px к кнопкам");
            float expected = dialog.Layout().Height;
            if (Mathf.Abs(panel.sizeDelta.y - expected) > 0.5f) offenders.Add($"{language}: высота {panel.sizeDelta.y} ≠ {expected}");
            if (dialog.Title.textInfo.lineCount > 0 && dialog.Title.isTextOverflowing)
                offenders.Add($"{language}: заголовок не помещается");

            dialog.Hide();
            Object.DestroyImmediate(ui);
            Object.DestroyImmediate(dialog.Root.gameObject);
        }

        Assert.GreaterOrEqual(Loc.Languages.Count, 10, "обход обязан видеть все языки");
        Assert.IsEmpty(offenders, "модальный диалог на каком-то языке собран внахлёст:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void DemoDialog_Buttons_PrimaryRightmost_CancelLeftOfIt_NoCloseCross()
    {
        var dialog = ShowDemoDialog().Dialog!;
        var panel = dialog.Panel;
        var primary = RectIn(panel, (RectTransform)dialog.PrimaryButton.transform);
        var cancel = RectIn(panel, (RectTransform)dialog.SecondaryButton.transform);

        Assert.Greater(primary.xMin, cancel.xMax, "решение пользователя 2026-10-04: «Отмена | Основная», основная справа");
        Assert.AreEqual(panel.rect.xMax - UIStyle.ModalPad, primary.xMax, 0.5f, "кнопки у правого края с паддингом 24");
        Assert.AreEqual(UIStyle.Accent, ((Image)dialog.PrimaryButton.targetGraphic).color);
        Assert.AreEqual(Loc.T("common.cancel"), dialog.SecondaryButton.GetComponentInChildren<TMP_Text>().text);
        Assert.IsFalse(panel.GetComponentsInChildren<Button>(true).Any(b => b.name == WindowChrome.CloseButtonName),
            "у модального диалога с «Отменой» нет × — две двери с одним смыслом путают (§7, D5)");
        Assert.AreEqual(UIStyle.DialogW, panel.sizeDelta.x);
    }

    [Test]
    public void ModalDialog_ConfirmAndCancel_HideAndRunTheirActions_AndClaimEscape()
    {
        var dialog = ModalDialog.Build(_canvas!.transform, "Probe");
        int confirmed = 0, cancelled = 0;
        var content = new ModalDialogContent
        {
            Title = "Удалить проект?",
            Body = "Отменить это нельзя.",
            PrimaryCaption = "Удалить",
            OnPrimary = () => confirmed++,
            OnSecondary = () => cancelled++,
            Danger = true,
        };

        dialog.Show(content);
        Assert.IsTrue(ModalDialog.AnyVisible, "открытый диалог забирает Escape у панелей под подложкой");
        Assert.AreEqual(UIStyle.Danger, ((Image)dialog.PrimaryButton.targetGraphic).color,
            "деструктивная основная — Danger (D5)");
        dialog.Confirm();
        Assert.AreEqual(1, confirmed);
        Assert.IsFalse(dialog.IsVisible);
        Assert.IsFalse(ModalDialog.AnyVisible);

        dialog.Show(content);
        dialog.Cancel();
        Assert.AreEqual(1, cancelled);
        Assert.IsFalse(dialog.IsVisible);
        Object.DestroyImmediate(dialog.Root.gameObject);
    }

    [Test]
    public void ModalDialog_Note_AppearsOnlyWhenGiven()
    {
        var dialog = ModalDialog.Build(_canvas!.transform, "Probe");
        dialog.Show(new ModalDialogContent { Title = "Т", Body = "Тело", PrimaryCaption = "OK" });
        float without = dialog.Panel.sizeDelta.y;
        Assert.IsFalse(dialog.Note.gameObject.activeSelf);

        dialog.Show(new ModalDialogContent { Title = "Т", Body = "Тело", Note = "Пояснение", PrimaryCaption = "OK" });
        Assert.IsTrue(dialog.Note.gameObject.activeSelf);
        Assert.Greater(dialog.Panel.sizeDelta.y, without, "пояснение добавляет свою строку к высоте");
        Object.DestroyImmediate(dialog.Root.gameObject);
    }
}
