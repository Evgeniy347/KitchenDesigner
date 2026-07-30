using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode-тест: загружает example.save.json и делает скриншот
/// всей сцены (3D + UI: тулбар, левая панель инструментов).
/// Сохраняет в docs/overview.png.</summary>
[Explicit("генератор docs/overview.png — см. tools\\artifacts.ps1")]
public class OverviewScreenshotTests
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
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_testCamera != null) Object.Destroy(_testCamera);
        if (_sunGo != null) Object.Destroy(_sunGo);
        yield return null;
    }

    /// <summary>Камера: target, углы и дистанция из example.save.json.
    /// Зашиты жёстко, чтобы тест не зависел от сейва.</summary>
    private static readonly CameraState SavedCamera = new CameraState
    {
        valid = true,
        targetX = 0.7281801f,
        targetY = 1.1341923f,
        targetZ = -2.7433026f,
        angleX = 26.399876f,
        angleY = -582.8002f,
        distance = 3.9f
    };

    [UnityTest]
    public IEnumerator CaptureOverview()
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

        // Даём системе прилипнуть к стенам и отрисоваться.
        yield return null;
        yield return null;
        yield return null;

        // ── 2. Камера из сейва ───────────────────────────────────────────
        if (CameraController.Instance != null)
            CameraController.Instance.SetState(SavedCamera);

        yield return null;

        // ── 3. Подготовка к скриншоту: переключаем Canvas на ScreenSpaceCamera,
        // чтобы Canvas попал в RenderTexture (вместе с 3D-сценой). ────────
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var cam = _testCamera!.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.85f, 0.87f, 0.9f, 1f);
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;

        var originals = new (Canvas canvas, RenderMode mode)[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            originals[i] = (canvases[i], canvases[i].renderMode);
            canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            canvases[i].worldCamera = cam;
        }

        // Кадр на переключение Canvas.
        yield return null;

        // ── 4. Рендер в RenderTexture 1920×1080 ──────────────────────────
        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();

        // ── 5. Сохранение PNG ────────────────────────────────────────────
        string outDir = Path.Combine(Application.dataPath, "..", "docs");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "overview.png");
        File.WriteAllBytes(outPath, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(outPath), $"PNG was not created at {outPath}");
        Assert.IsTrue(new FileInfo(outPath).Length > 0, "PNG file is empty");
        Debug.Log($"[Overview] Saved: {outPath} ({new FileInfo(outPath).Length} bytes)");

        // ── 6. Очистка ───────────────────────────────────────────────────
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        for (int i = 0; i < originals.Length; i++)
        {
            originals[i].canvas.renderMode = originals[i].mode;
            originals[i].canvas.worldCamera = null;
        }

        yield return null;
    }
}
