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

public class SpecificationDiagramTests
{
    private GameObject _bootstrap;
    private GameObject _mainCamera;
    private Canvas _uiCanvas;

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

        _uiCanvas = UIManager.Instance.Canvas;
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

    private IEnumerator CapturePanel(string panelName, string fileName,
        System.Action setupPanel, System.Action teardownPanel = null)
    {
        var hidden = new List<GameObject>();
        foreach (Transform child in _uiCanvas.transform)
        {
            if (child.name != panelName)
            {
                child.gameObject.SetActive(false);
                hidden.Add(child.gameObject);
            }
        }

        var panelT = _uiCanvas.transform.Find(panelName);
        Assert.IsNotNull(panelT, $"Panel '{panelName}' not found under canvas");
        var panelRt = panelT.GetComponent<RectTransform>();
        var origAnchorMin = panelRt.anchorMin;
        var origAnchorMax = panelRt.anchorMax;
        var origPivot = panelRt.pivot;
        var origPos = panelRt.anchoredPosition;
        var origSize = panelRt.sizeDelta;

        var scaler = _uiCanvas.GetComponent<CanvasScaler>();
        var origScaleMode = scaler.uiScaleMode;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        var origRenderMode = _uiCanvas.renderMode;
        _uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _uiCanvas.planeDistance = 1f;

        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;

        setupPanel?.Invoke();
        yield return null;
        yield return null;
        yield return null;

        int panelW = Mathf.Max(1, Mathf.CeilToInt(panelRt.sizeDelta.x));
        int panelH = Mathf.Max(1, Mathf.CeilToInt(panelRt.sizeDelta.y));

        panelRt.anchoredPosition = Vector2.zero;

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = panelH * 0.5f;
        cam.aspect = (float)panelW / panelH;
        cam.cullingMask = 1 << _uiCanvas.gameObject.layer;
        _uiCanvas.worldCamera = cam;

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

        teardownPanel?.Invoke();

        _uiCanvas.renderMode = origRenderMode;
        _uiCanvas.worldCamera = null;
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

    private static void SpawnPart(string name, int w, int h, int d, float posX, float posZ)
    {
        float y = h * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreatePart(new Vector3Int(w, h, d), name,
            new Vector3(posX, y, posZ));
        Assert.IsNotNull(go);
    }

    private static void SpawnFacade(string name, int w, int h, float posX, float posZ,
        int gl = 2, int gr = 2, int gt = 2, int gb = 2)
    {
        float y = h * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateFacade(new Vector3Int(w, h, 18), name,
            new Vector3(posX, y, posZ), gl, gr, gt, gb);
        Assert.IsNotNull(go);
    }

    private static void SpawnAssembled(string name, int w, int h, float posX, float posZ,
        AssembledFill fill = AssembledFill.Blind)
    {
        float y = h * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateAssembledFacade(new Vector3Int(w, h, 18), name,
            new Vector3(posX, y, posZ), fill);
        Assert.IsNotNull(go);
    }

    private static void SpawnDrawer(string name, DrawerType type, int length, DrawerColor color,
        int width, float posX, float posZ)
    {
        float y = DrawerConstants.GetTypeHeight(type) * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateDrawer(type, length, color, width, name,
            new Vector3(posX, y, posZ));
        Assert.IsNotNull(go);
    }

    // ── Tests ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SpecificationTable_20PlusRows_SavesPng()
    {
        // ── 10 regular parts (10 unique rows) ──

        SpawnPart("Боковина", 600, 720, 18, 0f, 0f);
        SpawnPart("Боковина", 600, 720, 18, 0f, -0.65f);

        SpawnPart("Полка", 564, 400, 18, 0.7f, 0f);
        SpawnPart("Полка", 564, 400, 18, 0.7f, -0.45f);
        SpawnPart("Полка", 564, 400, 18, 0.7f, -0.9f);

        SpawnPart("Дно", 600, 500, 18, 1.4f, 0f);
        SpawnPart("Дно", 600, 500, 18, 1.4f, -0.55f);

        SpawnPart("Крышка", 600, 600, 18, 2.1f, 0f);

        SpawnPart("Задняя стенка", 564, 600, 18, 2.8f, 0f);

        SpawnPart("Цоколь", 600, 100, 18, 3.5f, 0f);
        SpawnPart("Цоколь", 600, 100, 18, 3.5f, -0.15f);
        SpawnPart("Цоколь", 600, 100, 18, 3.5f, -0.30f);

        SpawnPart("Планка", 564, 80, 18, 4.2f, 0f);
        SpawnPart("Планка", 564, 80, 18, 4.2f, -0.10f);

        SpawnPart("Дверца", 300, 716, 18, 4.9f, 0f);
        SpawnPart("Дверца", 300, 716, 18, 4.9f, -0.75f);

        SpawnPart("Полка угловая", 800, 400, 18, 5.6f, 0f);

        SpawnPart("Столешница", 1200, 600, 18, 6.3f, 0f);

        // ── 2 regular facades (2 rows) ──

        SpawnFacade("Фасад навесной", 600, 400, 0f, -2f);
        SpawnFacade("Фасад нижний", 600, 720, 0.7f, -2f);

        // ── 2 assembled facades (10 rows) ──

        SpawnAssembled("Сборный 450×700", 450, 700, 1.4f, -2f, AssembledFill.Blind);
        SpawnAssembled("Сборный 600×500", 600, 500, 2.1f, -2f, AssembledFill.Glass);

        // ── 4 drawers (4 rows) ──

        SpawnDrawer("Ящик A350", DrawerType.A, 350, DrawerColor.Anthracite, 400, 2.8f, -2f);
        SpawnDrawer("Ящик B450", DrawerType.B, 450, DrawerColor.White, 500, 3.5f, -2f);
        SpawnDrawer("Ящик C500", DrawerType.C, 500, DrawerColor.Black, 600, 4.2f, -2f);
        SpawnDrawer("Ящик D250", DrawerType.D, 250, DrawerColor.Anthracite, 450, 4.9f, -2f);

        // ── 8 more regular parts (8 rows) ──

        SpawnPart("Перегородка", 564, 400, 18, 7.0f, 0f);
        SpawnPart("Перегородка", 564, 400, 18, 7.0f, -0.45f);

        SpawnPart("Стойка узкая", 150, 720, 18, 7.7f, 0f);
        SpawnPart("Стойка узкая", 150, 720, 18, 7.7f, -0.20f);

        SpawnPart("Полка маленькая", 300, 300, 18, 8.4f, 0f);
        SpawnPart("Полка маленькая", 300, 300, 18, 8.4f, -0.35f);

        SpawnPart("Дверца маленькая", 200, 500, 18, 9.1f, 0f);
        SpawnPart("Дверца маленькая", 200, 500, 18, 9.1f, -0.55f);

        SpawnPart("Цоколь короткий", 400, 100, 18, 9.8f, 0f);
        SpawnPart("Цоколь короткий", 400, 100, 18, 9.8f, -0.15f);

        SpawnPart("Планка длинная", 800, 80, 18, 10.5f, 0f);

        SpawnPart("Крышка маленькая", 400, 400, 18, 11.2f, 0f);

        SpawnPart("Задняя стенка мал.", 400, 500, 18, 11.9f, 0f);

        // ── 2 more facades (2 rows) ──

        SpawnFacade("Фасад 450", 450, 400, 7.0f, -2f);
        SpawnFacade("Фасад 800", 800, 400, 7.7f, -2f);

        // ── 2 more assembled facades (6 rows) ──

        SpawnAssembled("Сборный 800×600", 800, 600, 8.4f, -2f, AssembledFill.Blind);
        SpawnAssembled("Сборный 450×500", 450, 500, 9.1f, -2f, AssembledFill.Glass);

        // ── 2 more drawers (2 rows) ──

        SpawnDrawer("Ящик A400", DrawerType.A, 400, DrawerColor.White, 450, 9.8f, -2f);
        SpawnDrawer("Ящик B500", DrawerType.B, 500, DrawerColor.Black, 550, 10.5f, -2f);

        yield return null;

        yield return CapturePanel("SpecPanel", "specification_table.png",
            () =>
            {
                var specUI = UIManager.Instance.GetComponent<SpecificationPanelUI>();
                specUI.SetVisible(true);
            },
            () =>
            {
                var specUI = UIManager.Instance.GetComponent<SpecificationPanelUI>();
                specUI.SetVisible(false);
            });
    }
}
