using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;
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

    private List<(string Name, ModalDialog Dialog, System.Action Show)> BuildEveryModal()
    {
        var parent = _canvas!.transform;
        var demo = _host!.AddComponent<DemoModeDialogUI>();
        demo.Build(parent);
        var newer = _host.AddComponent<NewerVersionDialogUI>();
        newer.Build(parent);
        var update = _host.AddComponent<UpdateDialogUI>();
        update.Build(parent);
        return new List<(string, ModalDialog, System.Action)>
        {
            ("демо", demo.Dialog!, demo.Show),
            ("проект новее", newer.Dialog!, () => newer.Ask(NewerVersionStrings.Message("0.2100", "0.2092"), () => { })),
            ("обновление", update.Dialog!, () => update.ShowUpdateAvailable("0.2100", () => { }, () => { })),
        };
    }

    [Test]
    public void EveryModal_OnEveryLanguage_TitleNeverOverlapsTheBody_AndTheHeightIsTheContent()
    {
        var offenders = new List<string>();
        var modals = BuildEveryModal();
        foreach (var language in Loc.Languages.Select(l => l.Code).ToList())
        {
            Loc.SetLanguage(language);
            foreach (var (name, dialog, show) in modals)
            {
                show();
                Canvas.ForceUpdateCanvases();
                CheckStacking(offenders, $"{language}/{name}", dialog);
                dialog.Hide();
            }
        }

        Assert.GreaterOrEqual(Loc.Languages.Count, 10, "обход обязан видеть все языки");
        Assert.AreEqual(3, modals.Count);
        Assert.IsEmpty(offenders, "модальный диалог на каком-то языке собран внахлёст:\n" + string.Join("\n", offenders));
    }

    private static void CheckStacking(List<string> offenders, string where, ModalDialog dialog)
    {
        var panel = dialog.Panel;
        var title = RectIn(panel, dialog.Title.rectTransform);
        var body = RectIn(panel, dialog.Body.rectTransform);
        var buttons = RectIn(panel, (RectTransform)dialog.SecondaryButton.transform);

        float lowest = body.yMin;
        if (dialog.Extra.gameObject.activeSelf)
        {
            var extra = RectIn(panel, dialog.Extra);
            if (extra.yMax > body.yMin + 0.5f) offenders.Add($"{where}: вставка заходит на текст");
            lowest = Mathf.Min(lowest, extra.yMin);
        }
        if (dialog.Note.gameObject.activeSelf)
        {
            var note = RectIn(panel, dialog.Note.rectTransform);
            if (note.yMax > lowest + 0.5f) offenders.Add($"{where}: пояснение заходит на то, что над ним");
            lowest = Mathf.Min(lowest, note.yMin);
        }

        if (title.yMin < body.yMax - 0.5f) offenders.Add($"{where}: заголовок заходит на текст на {body.yMax - title.yMin:0.#} px");
        if (lowest < buttons.yMax + UIStyle.Space5 - 0.5f) offenders.Add($"{where}: текст ближе {UIStyle.Space5} px к кнопкам");
        float expected = dialog.Layout().Height;
        if (Mathf.Abs(panel.sizeDelta.y - expected) > 0.5f) offenders.Add($"{where}: высота {panel.sizeDelta.y} ≠ {expected}");
        if (dialog.Title.textInfo.lineCount > 0 && dialog.Title.isTextOverflowing)
            offenders.Add($"{where}: заголовок не помещается");
        if (buttons.xMin < panel.rect.xMin + UIStyle.ModalPad - 0.5f || buttons.xMax > panel.rect.xMax - UIStyle.ModalPad + 0.5f)
            offenders.Add($"{where}: кнопки выходят за паддинг панели");
    }

    [Test]
    public void NewerVersionDialog_OpenAnywayOpens_CancelDoesNot_AndBothClose()
    {
        var ui = _host!.AddComponent<NewerVersionDialogUI>();
        ui.Build(_canvas!.transform);
        int opened = 0;

        ui.Ask(NewerVersionStrings.Message("0.2100", "0.2092"), () => opened++);
        Assert.IsTrue(ui.IsVisible);
        StringAssert.Contains("0.2100", ui.MessageText);
        Assert.AreEqual(Loc.T("dialog.newerVersion.open"), ui.OpenButton!.GetComponentInChildren<TMP_Text>().text);
        ui.CancelButton!.onClick.Invoke();
        Assert.IsFalse(ui.IsVisible);
        Assert.AreEqual(0, opened, "«Отмена» не открывает проект");

        ui.Ask(NewerVersionStrings.Message("0.2100", "0.2092"), () => opened++);
        ui.OpenButton.onClick.Invoke();
        Assert.IsFalse(ui.IsVisible);
        Assert.AreEqual(1, opened);
    }

    [Test]
    public void NewerVersionDialog_WithoutABuiltDialog_OpensTheProjectAtOnce()
    {
        var ui = _host!.AddComponent<NewerVersionDialogUI>();
        int opened = 0;
        ui.Ask("x", () => opened++);
        Assert.AreEqual(1, opened, "нет окна — нет и вопроса: проект открывается, как до появления диалога");
    }

    [Test]
    public void ModalWithOnlyCancel_SitsAtTheRightEdge_AndEnterDoesNothing()
    {
        var dialog = ModalDialog.Build(_canvas!.transform, "OnlyCancelProbe");
        int cancelled = 0;
        dialog.Show(new ModalDialogContent
        {
            Title = "t",
            Body = "b",
            SecondaryCaption = "x",
            OnSecondary = () => cancelled++,
        });
        Canvas.ForceUpdateCanvases();

        Assert.IsFalse(dialog.PrimaryButton.gameObject.activeSelf, "у окна без основного действия нет основной кнопки — только «Отмена»");
        var cancel = RectIn(dialog.Panel, (RectTransform)dialog.SecondaryButton.transform);
        Assert.AreEqual(dialog.Panel.rect.xMax - UIStyle.ModalPad, cancel.xMax, 0.5f,
            "одинокая «Отмена» стоит у правого края, а не отступает на место скрытой основной");

        dialog.Confirm();
        Assert.IsTrue(dialog.IsVisible, "Enter без основной кнопки ничего не делает: он не должен срабатывать как «Отмена»");
        Assert.AreEqual(0, cancelled);
        dialog.Cancel();
        Assert.AreEqual(1, cancelled);
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
