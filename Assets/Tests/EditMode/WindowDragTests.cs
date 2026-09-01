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

    // ── Передний план ───────────────────────────────────────────────────

    [Test]
    public void BringToFront_MovesWindowAboveSiblings()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var winA = MakeRect("WinA", parent, new Vector2(300, 200));
        var winB = MakeRect("WinB", parent, new Vector2(300, 200));
        Assert.Less(winA.GetSiblingIndex(), winB.GetSiblingIndex());

        WindowDrag.BringToFront(winA);

        Assert.Greater(winA.GetSiblingIndex(), winB.GetSiblingIndex(),
            "после BringToFront окно должно рисоваться поверх соседей");
    }

    // ── Ресайз окна «Сцена» ─────────────────────────────────────────────

    private WindowResizeHandle BuildHierarchyWithResize(out RectTransform panel)
    {
        var canvas = MakeCanvas();
        var go = new GameObject("Hierarchy");
        _spawned.Add(go);
        go.AddComponent<HierarchyPanelUI>().Build(canvas.transform);
        panel = (RectTransform)canvas.transform.Find("HierarchyPanel");
        var handle = panel.GetComponentInChildren<WindowResizeHandle>();
        Assert.NotNull(handle, "у окна «Сцена» должен быть хэндл ресайза");

        // В batchmode rect канваса непредсказуем — переносим панель в родителя
        // фиксированного размера, чтобы кламп по низу экрана был детерминирован.
        var parent = MakeRect("FixedParent", null, new Vector2(1920, 1080));
        panel.SetParent(parent, false);
        return handle;
    }

    [Test]
    public void HierarchyResize_ChangesHeight_AndStretchesViewport()
    {
        var handle = BuildHierarchyWithResize(out var panel);
        float w = panel.sizeDelta.x;
        var viewport = (RectTransform)panel.Find("HierViewport");
        float insets = panel.sizeDelta.y - viewport.rect.height; // шапка + отступы

        handle.ResizeTo(400f);

        Assert.AreEqual(400f, panel.sizeDelta.y, 0.01f);
        Assert.AreEqual(w, panel.sizeDelta.x, 0.01f, "ширина не должна меняться");
        Assert.AreEqual(400f - insets, viewport.rect.height, 0.01f,
            "скролл-зона должна растянуться вместе с окном");
    }

    [Test]
    public void HierarchyResize_ClampsToMinHeight()
    {
        var handle = BuildHierarchyWithResize(out var panel);

        handle.ResizeTo(10f);

        Assert.AreEqual(160f, panel.sizeDelta.y, 0.01f,
            "высота не должна опускаться ниже минимума");
    }

    [Test]
    public void HierarchyResize_ClampsToParentBottom()
    {
        var handle = BuildHierarchyWithResize(out var panel);
        var parent = (RectTransform)panel.parent;
        // Верх окна в координатах родителя: до низа родителя и есть максимум.
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        float top = ((Vector2)parent.InverseTransformPoint(corners[1])).y;
        float maxH = top - parent.rect.yMin;

        handle.ResizeTo(99999f);

        Assert.AreEqual(maxH, panel.sizeDelta.y, 0.5f,
            "низ окна не должен уходить за нижний край экрана");
    }

    private static UnityEngine.EventSystems.RaycastResult Hit(GameObject go) =>
        new UnityEngine.EventSystems.RaycastResult { gameObject = go };

    [Test]
    public void TopHitInsideWindow_RaisesIt_EvenWhenAControlSwallowedTheClick()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));
        var button = MakeRect("Button", window, new Vector2(80, 24));
        var other = MakeRect("Other", parent, new Vector2(300, 200));

        var hits = new List<UnityEngine.EventSystems.RaycastResult> { Hit(button.gameObject) };

        Assert.IsTrue(WindowDragHandle.TopHitBelongsTo(hits, window),
            "клик по кнопке ВНУТРИ окна обязан поднимать окно: PointerDown до панели "
            + "не всплывает, если его обработал дочерний контрол, поэтому решение "
            + "принимается по верхнему хиту raycast");
        Assert.IsFalse(WindowDragHandle.TopHitBelongsTo(
            new List<UnityEngine.EventSystems.RaycastResult> { Hit(other.gameObject) }, window),
            "клик по соседнему окну своё окно поднимать не должен");
        Assert.IsFalse(WindowDragHandle.TopHitBelongsTo(
            new List<UnityEngine.EventSystems.RaycastResult>(), window),
            "клик по пустому месту не поднимает ничего");
    }

    [Test]
    public void PressOnInteractiveControl_DoesNotDragTheWindow()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));
        var button = MakeRect("Button", window, new Vector2(80, 24));
        button.gameObject.AddComponent<UnityEngine.UI.Button>();
        var caption = MakeRect("Caption", window, new Vector2(200, 24));

        Assert.IsTrue(WindowDragHandle.PressedOnInteractiveControl(button.gameObject),
            "нажатие на кнопке, дропдауне, поле или слайдере окно не тянет — иначе "
            + "любая правка в окне уезжала бы вместе с ним");
        Assert.IsFalse(WindowDragHandle.PressedOnInteractiveControl(caption.gameObject),
            "за немой лейбл заголовка окно тянуть можно");
        Assert.IsFalse(WindowDragHandle.PressedOnInteractiveControl(null));
    }

    [Test]
    public void OnlyTheTitleBarStripDragsTheWindow()
    {
        const float top = 100f;
        const float handleHeight = 30f;

        Assert.IsTrue(WindowDragHandle.InsideTitleBar(top - 1f, top, handleHeight));
        Assert.IsTrue(WindowDragHandle.InsideTitleBar(top - handleHeight, top, handleHeight),
            "нижняя граница полосы заголовка входит в неё");
        Assert.IsFalse(WindowDragHandle.InsideTitleBar(top - handleHeight - 1f, top, handleHeight),
            "ниже полосы заголовка окно не тянется: там живёт содержимое, и "
            + "протяжка по нему должна доставаться списку, а не окну");
    }

    [Test]
    public void ResizeHandle_IsInvisible_ButCatchesTheRaycast()
    {
        var parent = MakeRect("Parent", null, new Vector2(1920, 1080));
        var window = MakeRect("Win", parent, new Vector2(300, 200));

        WindowDrag.AttachResizeBottom(window, 160f);

        var handle = window.Find("ResizeHandle");
        var image = handle.GetComponent<UnityEngine.UI.Image>();
        Assert.AreEqual(0f, image.color.a, 0.001f, "полоса ресайза не должна быть видна");
        Assert.IsTrue(image.raycastTarget,
            "без картинки-приёмника нижний край окна не ловил бы протяжку вовсе");

        var grip = handle.Find("Grip");
        Assert.IsNotNull(grip, "видимый грип — единственная подсказка, что за край можно тянуть");
        Assert.IsFalse(grip.GetComponent<UnityEngine.UI.Image>().raycastTarget,
            "грип нарисован поверх полосы: ловил бы клики он — тянулась бы только "
            + "его узкая середина");
    }
}
