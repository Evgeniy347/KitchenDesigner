using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode: загружает example.save.json, входит в фоторежим и рендерит
/// интерьер в test-results/photo_mode.png. Используется для визуальной проверки
/// освещения/SSGI (картинку смотрим глазами, не снапшот).</summary>
public class PhotoModeScreenshotTests
{
    private const int RenderW = 1280;
    private const int RenderH = 800;

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
        PhotoMode.SetActive(false);
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

    private static readonly CameraState InteriorCamera = new CameraState
    {
        valid = true,
        targetX = 0.3f, targetY = 1.0f, targetZ = -2.6f,
        angleX = 8f, angleY = -1110f, distance = 1.2f, photoDistance = 1.2f
    };

    [UnityTest]
    public IEnumerator CapturePhotoMode()
    {
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

        if (CameraController.Instance != null)
            CameraController.Instance.SetState(InteriorCamera);
        yield return null;

        // Вход в фоторежим: реальные материалы, потолок, тени, качество, ambient-отскок.
        PhotoMode.SetActive(true);
        // Даём несколько кадров на пересборку пайплайна/теней/объёма пост-обработки.
        for (int i = 0; i < 6; i++) yield return null;

        var cam = _testCamera!.GetComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;

        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();

        string outDir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "photo_mode.png");
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Assert.IsTrue(new FileInfo(outPath).Length > 0, "PNG file is empty");
        Debug.Log($"[PhotoMode] Saved: {outPath} ({new FileInfo(outPath).Length} bytes)");

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        yield return null;
    }
}
