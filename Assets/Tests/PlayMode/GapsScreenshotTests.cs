using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode: загружает example.save.json, включает EdgeOutline
/// (чёрные контуры зазоров) и делает скриншот в docs/gaps_overview.png.
/// Две камеры: основная (3D) + UI-оверлей → один RenderTexture.</summary>
public class GapsScreenshotTests
{
    private const int RenderW = 1920;
    private const int RenderH = 1080;

    private GameObject? _bootstrap;
    private GameObject? _testCamera;
    private GameObject? _sunGo;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _testCamera = new GameObject("Main Camera");
        _testCamera!.tag = "MainCamera";
        _testCamera!.AddComponent<Camera>();
        _testCamera!.transform.position = Vector3.zero;
        _testCamera!.transform.rotation = Quaternion.identity;

        _sunGo = new GameObject("Directional Light");
        var light = _sunGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        RenderSettings.sun = light;

        SunController.Reset();

        SaveLoadManager.LastPath = "";
        var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        if (File.Exists(autoPath)) File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        KitchenSettings.Instance.NormalView.edgeOutline = false;
        // example.save.json несёт handleMode="Move" → RestoreScene выставил
        // глобальный статик. Возвращаем дефолт, иначе тулбар «Ручки: перенос»
        // течёт в снапшоты последующих фикстур (Iso*, тулбар).
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);

        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_testCamera != null) Object.Destroy(_testCamera);
        if (_sunGo != null) Object.Destroy(_sunGo);
        yield return null;
    }

    private static readonly CameraState SavedCamera = new CameraState
    {
        valid = true,
        targetX = 0.32920516f,
        targetY = 2.111027f,
        targetZ = -3.0358293f,
        angleX = -1.6001312f,
        angleY = -1107.7942f,
        distance = 1.2f
    };

    [UnityTest]
    public IEnumerator CaptureGapsOverview()
    {
        // ── 1. Загрузить сцену из example.save.json ──────────────────────
        string savePath = Path.Combine(Application.dataPath, "..", "docs", "example.save.json");
        Assert.IsTrue(File.Exists(savePath), $"Save file not found: {savePath}");

        var data = SaveLoadManager.LoadFromFile(savePath);
        Assert.IsNotNull(data, "Failed to deserialize save file");

        SaveLoadManager.ClearBoards(PartRegistry.GetAll());
        var created = SaveLoadManager.RestoreScene(data!);
        Assert.IsTrue(created.Count > 0, "No elements created from save");

        // Состав окон задаёт тест, а не сейв (см. ProjectWindowsTestState).
        ProjectWindowsTestState.ShowOnly(null);

        yield return null;
        yield return null;
        yield return null;

        // ── 2. Включить контуры зазоров ─────────────────────────────────
        KitchenSettings.Instance.NormalView.edgeOutline = true;
        yield return null;

        // ── 3. Камера из сейва ───────────────────────────────────────────
        if (CameraController.Instance != null)
            CameraController.Instance.SetState(SavedCamera);

        yield return null;

        // ── 4. Две камеры → один RT ─────────────────────────────────────
        var cam3d = _testCamera!.GetComponent<Camera>();
        cam3d.clearFlags = CameraClearFlags.SolidColor;
        cam3d.backgroundColor = new Color(0.85f, 0.87f, 0.9f, 1f);
        cam3d.fieldOfView = 50f;
        cam3d.nearClipPlane = 0.1f;
        cam3d.farClipPlane = 200f;
        cam3d.depth = 0;

        // UI-камера: рисует ВСЁ (Canvases и есть UI), очищает только depth.
        var uiCamGo = new GameObject("UI Camera");
        var uiCam = uiCamGo.AddComponent<Camera>();
        uiCam.clearFlags = CameraClearFlags.Nothing;
        uiCam.depth = 1;

        // Переключаем все Canvas на UI-камеру.
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = uiCam;
        }

        yield return null;

        // ── 5. Рендер ────────────────────────────────────────────────────
        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        cam3d.targetTexture = rt;
        uiCam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();

        // ── 6. Сохранение PNG ────────────────────────────────────────────
        string outDir = Path.Combine(Application.dataPath, "..", "docs");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "gaps_overview.png");
        File.WriteAllBytes(outPath, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(outPath), $"PNG was not created at {outPath}");
        Assert.IsTrue(new FileInfo(outPath).Length > 0, "PNG file is empty");
        Debug.Log($"[Gaps] Saved: {outPath} ({new FileInfo(outPath).Length} bytes)");

        // ── 7. Очистка ───────────────────────────────────────────────────
        RenderTexture.active = null;
        cam3d.targetTexture = null;
        uiCam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(uiCamGo);

        foreach (var c in canvases)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.worldCamera = null;
        }

        KitchenSettings.Instance.NormalView.edgeOutline = false;
        yield return null;
    }
}
