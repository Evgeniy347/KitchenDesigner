using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class WindowDragTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private RectTransform MakeRect(string name, Transform? parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent == null) _spawned.Add(go);
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    // Углы окна в локальных координатах родителя (тот же расчёт, что в клампе).
    private static (Vector2 min, Vector2 max) CornersInParent(RectTransform window)
    {
        var parent = (RectTransform)window.parent;
        var corners = new Vector3[4];
        window.GetWorldCorners(corners);
        return (parent.InverseTransformPoint(corners[0]),
                parent.InverseTransformPoint(corners[2]));
    }

    // ── ClampToParent ───────────────────────────────────────────────────

    [Test]
    public void Clamp_WindowInside_DoesNotMove()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));
        window.anchoredPosition = new Vector2(100, 50);

        WindowDrag.ClampToParent(window);

        Assert.AreEqual(new Vector2(100, 50), window.anchoredPosition,
            "окно целиком на экране не должно сдвигаться");
    }

    [Test]
    public void Clamp_WindowBeyondRightEdge_PulledBack()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));
        window.anchoredPosition = new Vector2(1000, 0); // right = 1150 > 960

        WindowDrag.ClampToParent(window);

        var (min, max) = CornersInParent(window);
        Assert.AreEqual(960f, max.x, 0.01f, "правый край должен прижаться к границе");
        Assert.AreEqual(660f, min.x, 0.01f, "ширина окна должна сохраниться");
    }

    [Test]
    public void Clamp_WindowBeyondBottomLeft_PulledBack()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));
        window.anchoredPosition = new Vector2(-2000, -2000); // полностью за экраном

        WindowDrag.ClampToParent(window);

        var (min, max) = CornersInParent(window);
        Assert.AreEqual(-960f, min.x, 0.01f, "левый край на границе");
        Assert.AreEqual(-540f, min.y, 0.01f, "нижний край на границе");
        Assert.AreEqual(-660f, max.x, 0.01f);
        Assert.AreEqual(-340f, max.y, 0.01f);
    }

    [Test]
    public void Clamp_WindowLargerThanParent_TopLeftPriority()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(2400, 1400));
        window.anchoredPosition = new Vector2(300, -300);

        WindowDrag.ClampToParent(window);

        var (min, max) = CornersInParent(window);
        Assert.AreEqual(-960f, min.x, 0.01f, "у окна шире экрана виден левый край");
        Assert.AreEqual(540f, max.y, 0.01f, "у окна выше экрана виден верхний край (заголовок)");
    }

    [Test]
    public void Clamp_RespectsNonTrivialAnchorsAndPivot()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));
        UIFactory.AnchorTopRight(window); // как у ContextMenu/DayNight/Hierarchy
        window.anchoredPosition = new Vector2(500, 90); // за правым и верхним краями

        WindowDrag.ClampToParent(window);

        var (min, max) = CornersInParent(window);
        Assert.AreEqual(960f, max.x, 0.01f, "правый край на границе");
        Assert.AreEqual(540f, max.y, 0.01f, "верхний край на границе");
    }

    // ── Подключение к окнам ─────────────────────────────────────────────

    private Canvas MakeCanvas()
    {
        var canvas = UIFactory.CreateCanvas("TestCanvas");
        _spawned.Add(canvas.gameObject);
        return canvas;
    }

    private void AssertDraggable(Transform canvas, string panelName)
    {
        var panel = canvas.Find(panelName);
        Assert.NotNull(panel, $"панель {panelName} должна существовать");
        Assert.NotNull(panel.GetComponent<WindowDragHandle>(),
            $"{panelName}: нет WindowDragHandle — окно не перетаскивается");
        Assert.NotNull(panel.GetComponent<WindowScreenGuard>(),
            $"{panelName}: нет WindowScreenGuard — окно может уйти за экран");
    }

    [Test]
    public void ContextMenu_IsDraggable()
    {
        var canvas = MakeCanvas();
        var go = new GameObject("CtxMenu");
        _spawned.Add(go);
        go.AddComponent<ContextMenuUI>().Build(canvas.transform);
        AssertDraggable(canvas.transform, "ContextMenu");
    }

    [Test]
    public void DayNightPanel_IsDraggable()
    {
        var canvas = MakeCanvas();
        var go = new GameObject("DayNight");
        _spawned.Add(go);
        go.AddComponent<DayNightPanelUI>().Build(canvas.transform);
        AssertDraggable(canvas.transform, "DayNightPanel");
    }

    [Test]
    public void HierarchyPanel_IsDraggable()
    {
        var canvas = MakeCanvas();
        var go = new GameObject("Hierarchy");
        _spawned.Add(go);
        go.AddComponent<HierarchyPanelUI>().Build(canvas.transform);
        AssertDraggable(canvas.transform, "HierarchyPanel");
    }

    [Test]
    public void GroupMenu_IsDraggable()
    {
        var canvas = MakeCanvas();
        var go = new GameObject("GroupMenu");
        _spawned.Add(go);
        go.AddComponent<GroupMenuUI>().Build(canvas.transform);
        AssertDraggable(canvas.transform, "GroupMenu");
    }
}
