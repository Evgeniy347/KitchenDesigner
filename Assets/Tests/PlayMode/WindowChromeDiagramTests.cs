using System.Collections;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Кадры, на которых видно, что общие компоненты T3 собираются как на макете
// docs/ui-redesign/mockups/dialogs.png: шапка D5 и футер у «Инструкций проекта», модальный
// диалог демо-проекта. Голдены этих окон лежат в DialogDiagramTests (T9); здесь — PNG, который
// смотрят глазами при каждой правке WindowChrome / ModalDialog.
public class WindowChromeDiagramTests
{
    private UiCaptureStage? _stage;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _stage?.Dispose();
        _stage = null;
        ProjectWindows.Clear();
        ProjectInstructions.Reset();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ProjectInstructions_OnWindowChrome_SavesPng()
    {
        _stage = new UiCaptureStage(700, 620);
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200\ncountertop_height_mm: 900";
        var ui = _stage.Host.AddComponent<ProjectInstructionsPanelUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        var input = ui.WindowRect!.GetComponentInChildren<TMPro.TMP_InputField>(true);
        input.text += "\n\nФасады — без ручек.";

        yield return _stage.Capture("chrome_project_instructions.png");

        Assert.IsTrue(ui.HasUnsavedChanges, "правка без сохранения — футер показывает «Есть несохранённые изменения»");
    }

    [UnityTest]
    public IEnumerator DemoModeDialog_OnModalDialog_SavesPng()
    {
        _stage = new UiCaptureStage(600, 320);
        var ui = _stage.Host.AddComponent<DemoModeDialogUI>();
        ui.Build(_stage.Canvas);
        ui.Show();

        yield return _stage.Capture("dialog_demo_mode.png");

        Assert.IsTrue(ui.IsVisible);
        ui.Hide();
    }
}
