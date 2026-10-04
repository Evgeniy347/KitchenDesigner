using System.Collections;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Кадр «Ошибок» на WindowChrome и DataTable (mockups/errors.png): чипы со счётчиками, значки уровня,
// футер «подсказка | К первой ошибке», пустое состояние. Сцена собрана руками и неизменна, поэтому
// голден-JSON ui_error_panel — настоящий: строки не зависят от геометрии камеры и порядка тестов.
public class ErrorPanelDiagramTests
{
    private UiCaptureStage? _stage;
    private GameObject? _host;

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        PartRegistry.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _stage?.Dispose();
        _stage = null;
        if (_host != null) Object.Destroy(_host);
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        yield return null;
        PartRegistry.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();
    }

    private ErrorPanelUI BuildPanel()
    {
        _stage = new UiCaptureStage(980, 600);
        _host = new GameObject("ErrorPanelHost");
        var panel = _host.AddComponent<ErrorPanelUI>();
        panel.Build(_stage.Canvas);
        return panel;
    }

    [UnityTest]
    public IEnumerator WithIssues_SavesPngAndTheGolden()
    {
        ElementFactory.CreateDrawer(DrawerType.B, 450, DrawerColor.Anthracite, 400,
            "Ящик без фасада", new Vector3(0f, 0.55f, 0f));
        ElementFactory.CreateFacade(new Vector3Int(560, 720, 18), "Фасад без зазора",
            new Vector3(1.2f, 0.55f, 0f), 0, 2, 2, 2);
        ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Полка A", new Vector3(2.4f, 0.8f, 0f));
        ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Полка B", new Vector3(2.4f, 0.8f, 0.023f));
        yield return null;

        var panel = BuildPanel();
        panel.SetVisible(true);
        Assert.Greater(panel.VisibleIssueCount, 0, "предпосылка: сцена даёт находки");
        var first = panel.Table.ShownRows[0].Tag is KitchenDesigner.Core.Analysis.AnalysisIssue iss ? iss : default;
        panel.HandleRowClick(first, 0, ctrl: false, shift: false);

        yield return _stage!.Capture("error_panel.png");
        UiSnapshotEngine.CaptureVerified(_stage.Host, System.IO.Path.Combine(Application.dataPath, "..",
            "test-results", "error_panel.json"));
    }

    [UnityTest]
    public IEnumerator NoIssues_SavesPngOfTheEmptyState()
    {
        var panel = BuildPanel();
        panel.SetVisible(true);

        yield return _stage!.Capture("error_panel_empty.png");
    }
}
