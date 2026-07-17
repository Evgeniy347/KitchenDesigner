using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode-тест: загружает example.save.json (два окна на LeftWall
/// пересекаются по Y) и рендерит изометрический скриншот для визуальной
/// проверки бага с полосой между окнами.</summary>
public class WindowStripBugTests
{
    private const int RenderW = 1024;
    private const int RenderH = 1024;
    private const float IsoFov = 45f;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera!.tag = "MainCamera";
        _mainCamera!.AddComponent<Camera>();
        _mainCamera!.transform.position = new Vector3(0f, 3f, -5f);
        _mainCamera!.transform.LookAt(Vector3.zero);

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
        foreach (var go in _spawned)
            if (go != null) Object.Destroy(go);
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    /// <summary>Направление камеры: смотрим на левую стену (где два окна)
    /// сбоку-спереди-сверху.</summary>
    private static readonly Vector3 LeftWallDir =
        new Vector3(1f, 0.3f, 0.5f).normalized;

    private (GameObject camGo, Camera cam) CreateCamera(Vector3 center, Vector3 size)
    {
        var camGo = new GameObject("BugCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = IsoFov;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        float maxDim = Mathf.Max(size.x, size.y, size.z);
        float distance = maxDim * 1.5f;

        camGo.transform.position = center + LeftWallDir * distance;
        camGo.transform.LookAt(center);

        return (camGo, cam);
    }

    private IEnumerator RenderToPng(Camera cam, string fileName)
    {
        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[BUG] Saved: {path}");

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    /// <summary>Загружает example.save.json и рендерит сцену. Окна на LeftWall
    /// пересекаются по Y (1.391±0.6 и 2.032±0.6 →overlap ~559мм). Скриншот
    /// покажет, есть ли визуальная полоса в зоне перекрытия.</summary>
    [UnityTest]
    public IEnumerator LoadExampleSave_RenderLeftWall()
    {
        // Путь к example.save.json относительно проекта.
        string savePath = Path.Combine(Application.dataPath, "..", "docs", "example.save.json");
        Assert.IsTrue(File.Exists(savePath), $"Save file not found: {savePath}");

        var data = SaveLoadManager.LoadFromFile(savePath);
        Assert.IsNotNull(data, "Failed to deserialize save file");

        SaveLoadManager.ClearBoards(PartRegistry.GetAll());
        var created = SaveLoadManager.RestoreScene(data!);
        Assert.IsTrue(created.Count > 0, "No elements created from save");

        // Даём окнам прилипнуть к стенам (SnapToWall вызывается в Update).
        yield return null;
        yield return null;
        yield return null;

        // Найти LeftWall для фокусировки камеры.
        GameObject? leftWall = null;
        foreach (var el in PartRegistry.GetAll())
        {
            if (el != null && el.gameObject.name == "LeftWall")
            {
                leftWall = el.gameObject;
                break;
            }
        }
        Assert.IsNotNull(leftWall, "LeftWall not found in scene");

        var wallEl = leftWall!.GetComponent<KitchenElement>();
        Assert.IsNotNull(wallEl);

        // Камера смотрит на центр стены.
        var (camGo, cam) = CreateCamera(leftWall.transform.position, MmToUnits(wallEl.DimensionsMM));
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "bug_window_strip.png");
    }

    private static Vector3 MmToUnits(Vector3Int mm) =>
        new Vector3(mm.x, mm.y, mm.z) * AppConstants.MM_TO_UNITS;
}
