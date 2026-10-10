using System.Collections;
using System.IO;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;
using KitchenDesigner.Tests;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

// docs/ui-redesign/dialogs.md: кадры модальных диалогов, тоста и «Инструкций проекта» на стенде
// UiCaptureStage. Четыре окна заводят голдены (ui_dialog_demo, ui_dialog_newer_version,
// ui_toast_undo, ui_project_instructions); остальные кадры — PNG, на которые смотрят глазами
// и сверяют с макетом mockups/dialogs.png. Порядок кнопок — решение пользователя 2026-10-04,
// «Отмена | Основная»: макет в этом месте нарисован наоборот.
public class DialogDiagramTests
{
    private const string NewerVersionMessage = "Проект сохранён более новой версией программы (0.2100 против вашей 0.2092).";

    private UiCaptureStage? _stage;

    [SetUp]
    public void SetUp() => LanguageStartup.PinSourceLanguageForTestRun();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _stage?.Dispose();
        _stage = null;
        ProjectWindows.Clear();
        ProjectInstructions.Reset();
        yield return null;
    }

    private IEnumerator CaptureWithGolden(string name)
    {
        yield return _stage!.Capture(name + ".png");
        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        UiSnapshotEngine.CaptureVerified(_stage.Host, Path.Combine(dir, name + ".json"));
    }

    private static IEnumerator UntilShown(ToastNotification toast)
    {
        yield return new WaitForSeconds(0.4f);
    }

    [UnityTest]
    public IEnumerator DemoModeDialog_SavesPng_AndGolden()
    {
        _stage = new UiCaptureStage(600, 300);
        var ui = _stage.Host.AddComponent<DemoModeDialogUI>();
        ui.Build(_stage.Canvas);
        ui.Show();

        yield return CaptureWithGolden("dialog_demo");

        Assert.IsTrue(ui.IsVisible);
        ui.Hide();
    }

    [UnityTest]
    public IEnumerator NewerVersionDialog_SavesPng_AndGolden()
    {
        _stage = new UiCaptureStage(600, 360);
        var ui = _stage.Host.AddComponent<NewerVersionDialogUI>();
        ui.Build(_stage.Canvas);
        ui.Ask(NewerVersionMessage, () => { });

        yield return CaptureWithGolden("dialog_newer_version");

        Assert.IsTrue(ui.IsVisible);
    }

    [UnityTest]
    public IEnumerator UpdateDialog_SavesPng()
    {
        _stage = new UiCaptureStage(600, 360);
        var ui = _stage.Host.AddComponent<UpdateDialogUI>();
        ui.Build(_stage.Canvas);
        ui.ShowUpdateAvailable("0.2100", () => { }, () => { });

        yield return _stage.Capture("dialog_update_available.png");

        Assert.IsTrue(ui.IsVisible);
    }

    [UnityTest]
    public IEnumerator DeleteConfirmDialog_Danger_SavesPng()
    {
        _stage = new UiCaptureStage(600, 300);
        var dialog = ModalDialog.Build(_stage.Canvas, "DeleteProject");
        dialog.Show(new ModalDialogContent
        {
            Title = "Удалить проект?",
            Body = "«dacha_2.kdproj» будет удалён с диска. Отменить это нельзя.",
            PrimaryCaption = "Удалить",
            Danger = true,
        });

        yield return _stage.Capture("dialog_delete_confirm.png");

        Assert.IsTrue(dialog.IsVisible);
        dialog.Hide();
    }

    [UnityTest]
    public IEnumerator Toast_Undo_SavesPng_AndGolden()
    {
        _stage = new UiCaptureStage(520, 160);
        var toast = _stage.Host.AddComponent<ToastNotification>();
        toast.Build(_stage.Canvas);
        toast.Show("Удалено: Полка", 600f, "Отменить", () => { }, StatusLevel.Success);
        yield return UntilShown(toast);

        yield return CaptureWithGolden("toast_undo");

        Assert.AreEqual(1f, toast.View!.Group.alpha);
    }

    [UnityTest]
    public IEnumerator Toast_Levels_SavePngs()
    {
        _stage = new UiCaptureStage(760, 160);
        var toast = _stage.Host.AddComponent<ToastNotification>();
        toast.Build(_stage.Canvas);
        var cases = new (string Name, string Text, StatusLevel Level, string? Action)[]
        {
            ("info", "Проект сохранён: kuhnya.kdproj", StatusLevel.Info, null),
            ("warning", "Не больше 8 пазов на деталь", StatusLevel.Warning, null),
            ("error", "Не удалось сохранить: диск только для чтения", StatusLevel.Error, "Подробнее"),
            ("long", Loc.T("toast.photoLookReset"), StatusLevel.Info, null),
        };

        foreach (var (name, text, level, action) in cases)
        {
            toast.Show(text, 600f, action, action != null ? () => { } : null, level);
            yield return UntilShown(toast);
            yield return _stage.Capture("toast_" + name + ".png");
        }

        Assert.AreEqual(1f, toast.View!.Group.alpha);
    }

    [UnityTest]
    public IEnumerator ProjectInstructions_SavesPng_AndGolden()
    {
        _stage = new UiCaptureStage(700, 620);
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200\ncountertop_height_mm: 900";
        var ui = _stage.Host.AddComponent<ProjectInstructionsPanelUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        var input = ui.WindowRect!.GetComponentInChildren<TMP_InputField>(true);
        input.text += "\n\nФасады — без ручек, Blum TIP-ON.";

        yield return CaptureWithGolden("project_instructions");

        Assert.IsTrue(ui.HasUnsavedChanges, "правка без сохранения — футер показывает «Есть несохранённые изменения»");
    }

    [UnityTest]
    public IEnumerator ProjectInstructions_Empty_ShowsThePlaceholder_SavesPng()
    {
        _stage = new UiCaptureStage(700, 620);
        ProjectInstructions.Text = "";
        var ui = _stage.Host.AddComponent<ProjectInstructionsPanelUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);

        yield return _stage.Capture("project_instructions_empty.png");

        var placeholder = ui.WindowRect!.GetComponentInChildren<TMP_InputField>(true).placeholder;
        Assert.IsNotNull(placeholder, "у пустого поля есть подсказка внутри (dialogs.md, п. 6)");
        Assert.IsTrue(placeholder.gameObject.activeInHierarchy, "и она видна, пока поле пусто");
    }
}
