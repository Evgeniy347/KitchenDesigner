using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

// Кадры малых панелей рядом с mockups/tool-panels.png: «Этажи», «День / Ночь», «Музыка». У «Этажей»
// и «Музыки» здесь же голдены (ui_levels_window, ui_music_panel), у «Дня / Ночи» голден живёт в
// ElementPropertyDiagramTests на живом UIManager. «Замер» снимает MeasureProperties_SavesPng,
// HUD F9 рисуется IMGUI и в RenderTexture не попадает — его геометрию держат PerfHudTests.
public class ToolPanelsDiagramTests
{
    private UiCaptureStage? _stage;
    private int _track;
    private int _volume;

    [SetUp]
    public void RememberTheMusic()
    {
        _track = MusicState.Track;
        _volume = MusicState.VolumePct;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _stage?.Dispose();
        _stage = null;
        ProjectWindows.Clear();
        LevelRegistry.Reset();
        CommandStack.Clear();
        SunController.Reset();
        MusicState.Track = _track;
        MusicState.VolumePct = _volume;
        yield return null;
    }

    private static void Centre(RectTransform panel)
    {
        UIFactory.AnchorCenter(panel);
        panel.anchoredPosition = Vector2.zero;
    }

    private static void Golden(GameObject root, string name) =>
        UiSnapshotEngine.CaptureVerified(root, Path.Combine(Application.dataPath, "..", "test-results", name + ".json"));

    [UnityTest]
    public IEnumerator LevelsWindow_OnTheMockupData_SavesPngAndGolden()
    {
        LevelRegistry.Reset();
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
            new Level("3", "Мансарда", 6000, 2500),
        });
        LevelRegistry.CurrentId = "1";
        _stage = new UiCaptureStage(520, 320);
        var ui = _stage.Host.AddComponent<LevelsWindowUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        Centre(ui.WindowRect!);

        yield return _stage.Capture("levels_window.png");
        Golden(ui.WindowRect!.gameObject, "levels_window");
    }

    [UnityTest]
    public IEnumerator LevelsWindow_ArmedDelete_SavesPng()
    {
        LevelRegistry.Reset();
        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000), new Level("2", "2 этаж", 3000, 3000) });
        _stage = new UiCaptureStage(520, 260);
        var ui = _stage.Host.AddComponent<LevelsWindowUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        Centre(ui.WindowRect!);
        ui.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .First(b => b.name == "LvDelete_1").onClick.Invoke();

        yield return _stage.Capture("levels_window_armed.png");
        ConfirmDeleteButton.DisarmAll();
    }

    [UnityTest]
    public IEnumerator DayNightPanel_OnFormRows_SavesPng()
    {
        _stage = new UiCaptureStage(420, 260);
        var ui = _stage.Host.AddComponent<DayNightPanelUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        Centre(ui.WindowRect!);

        yield return _stage.Capture("daynight_panel.png");
    }

    [UnityTest]
    public IEnumerator MusicPanel_OnTheMockupData_SavesPngAndGolden()
    {
        MusicState.Track = 0;
        MusicState.VolumePct = MusicState.DEFAULT_VOLUME_PCT;
        _stage = new UiCaptureStage(400, 220);
        var ui = _stage.Host.AddComponent<MusicPanelUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        Centre(ui.WindowRect!);

        yield return _stage.Capture("music_panel_golden.png");
        Golden(ui.WindowRect!.gameObject, "music_panel");
    }
}
