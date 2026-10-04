using System.Collections;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Кадры оболочки 1:1 для сверки с docs/ui-redesign/mockups/shell.png: тулбар с открытым окном
// «Сцена», счётчиком ошибок и двумя этажами; раскрытый каталог с одной открытой группой; та же
// полоса на ширине 1366 — бюджет ширины из shell.md. PNG смотрят глазами при каждой правке
// ToolbarUI/SidebarUI; голдены структуры живут в HierarchyPanelDiagramTests и SidebarPanelTests.
public class ShellDiagramTests
{
    private sealed class OpenSceneHost : IToolbarHost
    {
        public void TogglePanel(ToolbarPanel panel) { }
        public bool IsPanelVisible(ToolbarPanel panel) => panel == ToolbarPanel.Hierarchy;
        public void SaveCurrent() { }
        public void NewProject() { }
        public void SaveAs() { }
        public void LoadDialog() { }
    }

    private UiCaptureStage? _stage;
    private GameObject? _sidebarHost;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        EditModeManager.Reset();
        SidebarUI.ResetLastUsedGroupForTests();
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 2800),
        });
        LevelRegistry.CurrentId = "1";
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_sidebarHost != null) Object.DestroyImmediate(_sidebarHost);
        _stage?.Dispose();
        _stage = null;
        LevelRegistry.Reset();
        EditModeManager.Reset();
        SidebarUI.ResetLastUsedGroupForTests();
        yield return null;
    }

    private void BuildShell(int width, int height)
    {
        _stage = new UiCaptureStage(width, height);
        var toolbar = new ToolbarUI();
        toolbar.Build(_stage.Canvas, new OpenSceneHost());
        toolbar.Refresh();

        var errors = _stage.Canvas.Find("Toolbar/Errors");
        Assert.IsNotNull(errors, "кнопка «Ошибки» на тулбаре");
        ToolbarIssueBadge.Create(errors!).Show(2, true);

        _sidebarHost = new GameObject("SidebarHost");
        var sidebar = _sidebarHost.AddComponent<SidebarUI>();
        sidebar.Build(_stage.Canvas);
        sidebar.SetExpandedForTests(true);
    }

    [UnityTest]
    public IEnumerator Shell_AtFullHd_SavesPng()
    {
        BuildShell(1920, 1080);

        yield return _stage!.Capture("shell_1920.png");
    }

    [UnityTest]
    public IEnumerator Shell_AtTheLaptopWidth_SavesPng()
    {
        BuildShell(1366, 768);

        yield return _stage!.Capture("shell_1366.png");

        var bar = _stage.Canvas.Find("Toolbar")!;
        float flowRight = 0f, rightGroupLeft = float.MaxValue;
        foreach (RectTransform child in bar)
        {
            if (child.anchorMin.x == 0f && child.anchorMax.x == 0f)
                flowRight = Mathf.Max(flowRight, child.anchoredPosition.x + child.sizeDelta.x);
            else if (child.anchorMin.x == 1f)
                rightGroupLeft = Mathf.Min(rightGroupLeft, _stage.Width + child.anchoredPosition.x - child.sizeDelta.x);
        }

        Assert.Less(flowRight, rightGroupLeft, $"на 1366 левый поток ({flowRight:0}) доходит до правой группы ({rightGroupLeft:0})");
    }
}
