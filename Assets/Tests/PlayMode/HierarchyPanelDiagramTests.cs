using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>
/// Скриншот- и JSON-снапшот-тесты окна «Сцена» (HierarchyPanelUI):
/// - hierarchy_panel.png/.json — само окно с группами, ящиком и вложенным фасадом;
/// - toolbar_scene_button.png/.json — тулбар: где находится кнопка «Сцена»;
/// - ui_full_interface.png — весь интерфейс 1920x1080 (тулбар + сайдбар + окно
///   «Сцена» справа под тулбаром).
/// PNG сохраняются в /test-results; JSON голден-матчится через UiSnapshotEngine.
/// </summary>
public class HierarchyPanelDiagramTests
{
    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private Canvas? _uiCanvas;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera.tag = "MainCamera";
        _mainCamera.AddComponent<Camera>();
        _mainCamera.transform.position = new Vector3(0f, 3f, -5f);
        _mainCamera.transform.LookAt(Vector3.zero);

        SaveLoadManager.LastPath = "";
        var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        if (File.Exists(autoPath)) File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;

        _uiCanvas = UIManager.Instance!.Canvas;
        Assert.IsNotNull(_uiCanvas, "Canvas should be created by Bootstrap");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GroupManager.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            if (es != null) Object.Destroy(es.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    /// <summary>Наполнить сцену: модуль из двух досок, ящик с прикреплённым
    /// фасадом, свободная полка — все ветки дерева на одном скрине.</summary>
    private static void SpawnShowcase()
    {
        var sideL = ElementFactory.CreatePart(new Vector3Int(18, 720, 500), "А3_боковина_L",
            new Vector3(0f, 0.36f, 0f)).GetComponent<KitchenElement>();
        var sideR = ElementFactory.CreatePart(new Vector3Int(18, 720, 500), "А3_боковина_R",
            new Vector3(0.6f, 0.36f, 0f)).GetComponent<KitchenElement>();
        var g = GroupManager.Link(new List<KitchenElement> { sideL, sideR });
        GroupManager.Rename(g!, "Модуль А3");

        var drawer = ElementFactory.CreateDrawer(DrawerType.B, 450, DrawerColor.Anthracite, 400,
            "Верхний ящик", new Vector3(0.3f, 0.55f, 0f)).GetComponent<DrawerElement>();
        ElementFactory.CreateFacade(new Vector3Int(560, 140, 18), "Фасад ящика",
            new Vector3(0.3f, 0.55f, -0.27f), 2, 2, 2, 2);
        drawer.AttachedFacadeName = "Фасад ящика";
        GroupManager.AddTo(g!, drawer);

        ElementFactory.CreatePart(new Vector3Int(600, 18, 400), "Свободная полка",
            new Vector3(1.5f, 0.8f, 0f));
    }

    /// <summary>
    /// Рендер части UI в PNG (в /test-results) + JSON-снапшот через UiSnapshotEngine.
    /// panelName == null — снимается ВЕСЬ канвас (соседние панели не прячутся).
    /// forcedW/H > 0 — явный размер кадра; иначе берётся фактический размер панели.
    /// </summary>
    private IEnumerator Capture(string? panelName, string fileName,
        int forcedW, int forcedH, bool recenter, System.Action? setup, bool goldenJson = true)
    {
        var hidden = new List<GameObject>();
        RectTransform? panelRt = null;
        Vector2 origAnchorMin = default, origAnchorMax = default, origPivot = default, origPos = default;

        if (panelName != null)
        {
            foreach (Transform child in _uiCanvas!.transform)
            {
                if (child.name != panelName && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    hidden.Add(child.gameObject);
                }
            }
            var panelT = _uiCanvas!.transform.Find(panelName);
            Assert.IsNotNull(panelT, $"Panel '{panelName}' not found under canvas");
            panelRt = panelT!.GetComponent<RectTransform>();
            origAnchorMin = panelRt.anchorMin;
            origAnchorMax = panelRt.anchorMax;
            origPivot = panelRt.pivot;
            origPos = panelRt.anchoredPosition;
        }

        var scaler = _uiCanvas!.GetComponent<CanvasScaler>();
        var origScaleMode = scaler.uiScaleMode;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        var origRenderMode = _uiCanvas!.renderMode;
        _uiCanvas!.renderMode = RenderMode.ScreenSpaceCamera;
        _uiCanvas!.planeDistance = 1f;

        if (recenter && panelRt != null)
        {
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
        }

        setup?.Invoke();
        yield return null; // Layout

        int w = forcedW > 0 ? forcedW
            : Mathf.Max(1, Mathf.CeilToInt(panelRt!.sizeDelta.x));
        int h = forcedH > 0 ? forcedH
            : Mathf.Max(1, Mathf.CeilToInt(panelRt!.sizeDelta.y));
        if (recenter && panelRt != null)
            panelRt.anchoredPosition = Vector2.zero;

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = h * 0.5f;
        cam.aspect = (float)w / h;
        cam.cullingMask = 1 << _uiCanvas!.gameObject.layer;
        _uiCanvas!.worldCamera = cam;

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[SCREENSHOT] Saved: {path} ({w}x{h})");

        // JSON-снапшот структуры: для панели — её поддерево, для полного кадра — канвас.
        var jsonRoot = panelName != null
            ? _uiCanvas!.transform.Find(panelName)!.gameObject
            : _uiCanvas!.gameObject;
        var jsonPath = Path.ChangeExtension(path, ".json");
        if (goldenJson)
            UiSnapshotEngine.CaptureVerified(jsonRoot, jsonPath); // + голден-матч
        else
            UiSnapshotEngine.Capture(jsonRoot, jsonPath);         // только файл

        _uiCanvas!.renderMode = origRenderMode;
        _uiCanvas!.worldCamera = null;
        scaler.uiScaleMode = origScaleMode;

        if (panelRt != null)
        {
            panelRt.anchorMin = origAnchorMin;
            panelRt.anchorMax = origAnchorMax;
            panelRt.pivot = origPivot;
            panelRt.anchoredPosition = origPos;
        }
        foreach (var go in hidden)
            go.SetActive(true);

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
    }

    // ── Тесты ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator HierarchyPanel_WithGroupsAndDrawer_SavesPngAndJson()
    {
        SpawnShowcase();
        yield return null; // панель подхватит изменения по событию Changed

        yield return Capture("HierarchyPanel", "hierarchy_panel.png", 0, 0, recenter: true,
            setup: () =>
            {
                HierarchyPanelUI.Instance!.SetVisible(true);
                // Выделяем элемент — на скрине видна подсветка строки.
                var shelf = PartRegistry.GetAll().Find(e => e.PartName == "Свободная полка");
                if (shelf != null) SelectionManager.Instance?.Select(shelf);
            });
    }

    [UnityTest]
    public IEnumerator Toolbar_SceneButton_SavesPngAndJson()
    {
        // Тулбар целиком (1920x52): кнопка «Сцена» — вторая слева, после «Спецификация».
        yield return Capture("Toolbar", "toolbar_scene_button.png", 1920, 52, recenter: false, setup: null);
    }

    [UnityTest]
    public IEnumerator FullInterface_SavesPng()
    {
        SpawnShowcase();
        yield return null;

        // Весь интерфейс: тулбар сверху, сайдбар слева, окно «Сцена» справа под
        // тулбаром. JSON всего канваса волатилен (индикаторы, версия) —
        // голден-матч выключен, файл пишется для глаз.
        yield return Capture(null, "ui_full_interface.png", 1920, 1080, recenter: false,
            setup: () => HierarchyPanelUI.Instance!.SetVisible(true), goldenJson: false);
    }
}
