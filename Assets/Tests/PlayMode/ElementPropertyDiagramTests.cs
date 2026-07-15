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

public class ElementPropertyDiagramTests
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

    /// <summary>
    /// Hide all sibling panels, center <paramref name="panelName"/>,
    /// switch canvas to ScreenSpaceCamera + ConstantPixelSize, render to PNG.
    /// Camera size is derived from the panel's actual RectTransform after setupPanel
    /// runs (Layout may resize it), so content is never clipped.
    /// </summary>
    private IEnumerator CapturePanel(string panelName, string fileName,
        System.Action setupPanel, System.Action? teardownPanel = null)
    {
        var hidden = new List<GameObject>();
        foreach (Transform child in _uiCanvas!.transform)
        {
            if (child.name != panelName)
            {
                child.gameObject.SetActive(false);
                hidden.Add(child.gameObject);
            }
        }

        var panelT = _uiCanvas!.transform.Find(panelName);
        Assert.IsNotNull(panelT, $"Panel '{panelName}' not found under canvas");
        var panelRt = panelT.GetComponent<RectTransform>();
        var origAnchorMin = panelRt.anchorMin;
        var origAnchorMax = panelRt.anchorMax;
        var origPivot = panelRt.pivot;
        var origPos = panelRt.anchoredPosition;
        var origSize = panelRt.sizeDelta;

        // Switch canvas to ScreenSpaceCamera + ConstantPixelSize before setupPanel
        // so that Layout() computes sizes in pixel units (not scaled).
        var scaler = _uiCanvas!.GetComponent<CanvasScaler>();
        var origScaleMode = scaler.uiScaleMode;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        var origRenderMode = _uiCanvas!.renderMode;
        _uiCanvas!.renderMode = RenderMode.ScreenSpaceCamera;
        _uiCanvas!.planeDistance = 1f;

        // Re-anchor to center so the panel sits in the middle of the canvas.
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;

        // Run setupPanel (opens the panel, triggers Layout which may resize it).
        setupPanel?.Invoke();
        yield return null; // let Layout settle

        // Use the panel's ACTUAL size after Layout — never the hardcoded estimate.
        int panelW = Mathf.Max(1, Mathf.CeilToInt(panelRt.sizeDelta.x));
        int panelH = Mathf.Max(1, Mathf.CeilToInt(panelRt.sizeDelta.y));

        // Re-center after Layout may have changed sizeDelta.
        panelRt.anchoredPosition = Vector2.zero;

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = panelH * 0.5f;
        cam.aspect = (float)panelW / panelH;
        cam.cullingMask = 1 << _uiCanvas!.gameObject.layer;
        _uiCanvas!.worldCamera = cam;

        var rt = new RenderTexture(panelW, panelH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(panelW, panelH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, panelW, panelH), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[SCREENSHOT] Saved: {path} ({panelW}x{panelH})");

        var jsonPath = Path.ChangeExtension(path, ".json");
        UiSnapshotEngine.Capture(panelT.gameObject, jsonPath);

        teardownPanel?.Invoke();

        _uiCanvas!.renderMode = origRenderMode;
        _uiCanvas!.worldCamera = null;
        scaler.uiScaleMode = origScaleMode;

        panelRt.anchorMin = origAnchorMin;
        panelRt.anchorMax = origAnchorMax;
        panelRt.pivot = origPivot;
        panelRt.anchoredPosition = origPos;
        panelRt.sizeDelta = origSize;

        foreach (var go in hidden)
            go.SetActive(true);

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
    }

    // ── Spawn helpers ────────────────────────────────────────

    private static KitchenElement SpawnPart(string name)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), name,
            new Vector3(1f, 0.2f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnFacade(string name)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 400, 18), name,
            new Vector3(2f, 0.2f, 0f), 2, 2, 2, 2);
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnAssembled(string name)
    {
        var go = ElementFactory.CreateAssembledFacade(new Vector3Int(450, 700, 18), name,
            new Vector3(3f, 0.35f, 0f), AssembledFill.Blind);
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnRadial(string name)
    {
        var go = ElementFactory.CreateRadialShelf(300, 18, name,
            new Vector3(4f, 0.009f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnDrawer(string name)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name,
            new Vector3(5f, 0.043f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnTable(string name)
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1200, 750, 600), name,
            new Vector3(6f, 0.375f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    // ── Tests ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ContextMenu_Part_SavesPng()
    {
        var el = SpawnPart("Деталь_800x400");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_part.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Facade_SavesPng()
    {
        var el = SpawnFacade("Фасад_600x400");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_facade.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_AssembledFacade_SavesPng()
    {
        var el = SpawnAssembled("Сборный_450x700");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_assembled.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_RadialShelf_SavesPng()
    {
        var el = SpawnRadial("Радиусная_300");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_radial.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Drawer_SavesPng()
    {
        var el = SpawnDrawer("Ящик_A_350");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_drawer.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator FloorSettings_SavesPng()
    {
        var floor = GameObject.FindWithTag("Floor");
        Assert.IsNotNull(floor, "Floor (BasePlate) should exist after Bootstrap");
        yield return CapturePanel("FloorSettings", "floorsettings.png",
            () => { FloorSettingsUI.Instance?.Open(); },
            () => { FloorSettingsUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator Sidebar_SavesPng()
    {
        yield return CapturePanel("Sidebar", "sidebar.png",
            () => { },
            () => { });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Table_SavesPng()
    {
        var el = SpawnTable("Стол_2000x750x1000");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_table.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_RadiusTable_SavesPng()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateRadiusTable(dims, "Радиусный стол", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_radius_table.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }
}
