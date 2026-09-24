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
/// PlayMode: строим LoadProjectWindowUI, наполняем список недавних проектов управляемым
/// набором путей (реальные и отсутствующие файлы на диске) и снимаем PNG + UI-снапшот
/// для каждого визуального состояния окна.
/// </summary>
public class LoadProjectWindowDiagramTests
{
    private const int PanelW = 900;
    private const int PanelH = 700;

    private GameObject? _canvasGo;
    private GameObject? _camGo;
    private GameObject? _eventSystem;
    private string[]? _recentBackup;
    private readonly List<string> _tempFiles = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _recentBackup = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_canvasGo != null) Object.Destroy(_canvasGo);
        if (_camGo != null) Object.Destroy(_camGo);
        if (_eventSystem != null) Object.Destroy(_eventSystem);

        if (_recentBackup != null)
            RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = _recentBackup;

        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        _tempFiles.Clear();

        yield return null;
    }

    private string MakeProjectFile(string name, string appVersion, string createdAtUtc)
    {
        string path = Path.Combine(Application.temporaryCachePath, name);
        File.WriteAllText(path,
            "{\"version\":1,\"appVersion\":\"" + appVersion + "\",\"createdAtUtc\":\"" + createdAtUtc + "\"}");
        _tempFiles.Add(path);
        return path;
    }

    private RenderTexture? _rt;

    private LoadProjectWindowUI BuildWindow(int width, int height)
    {
        _canvasGo = new GameObject("TestCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            _eventSystem = new GameObject("EventSystem");
            _eventSystem.AddComponent<EventSystem>();
            _eventSystem.AddComponent<StandaloneInputModule>();
        }

        _camGo = new GameObject("UICamera");
        var cam = _camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = height * 0.5f;
        cam.aspect = (float)width / height;
        cam.cullingMask = 1 << _canvasGo.layer;
        canvas.worldCamera = cam;

        _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = _rt;

        // Вне игры Unity не гоняет полный layout-проход ScreenSpaceCamera на каждый
        // кадр батч-тестов, поэтому Canvas.rect остаётся дефолтным разрешением
        // прогона (640x480), пока его не выставить руками — LoadWindowLayout читает
        // именно этот rect, и без явного значения тест «маленький экран» всегда
        // видел бы одну и ту же (неверную) высоту.
        var canvasRect = (RectTransform)canvas.transform;
        canvasRect.sizeDelta = new Vector2(width, height);

        var ui = _canvasGo.AddComponent<LoadProjectWindowUI>();
        ui.Build(canvas.transform);
        return ui;
    }

    private IEnumerator CaptureAndVerify(int width, int height, string pngName)
    {
        var rt = _rt!;
        yield return null;
        yield return null;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, pngName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderTexture.active = null;
        _camGo!.GetComponent<Camera>().targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[DIAGRAM] Saved: {path}");

        var jsonPath = Path.ChangeExtension(path, ".json");
        UiSnapshotEngine.CaptureVerified(_canvasGo!, jsonPath);
    }

    [UnityTest]
    public IEnumerator Normal_ThreeExistingProjects_SavesPng()
    {
        var a = MakeProjectFile("lpw_normal_a.kdproj", BuildInfo.Version, "2026-01-05T10:00:00Z");
        var b = MakeProjectFile("lpw_normal_b.kdproj", BuildInfo.Version, "2026-02-10T09:30:00Z");
        var c = MakeProjectFile("lpw_normal_c.kdproj", BuildInfo.Version, "2026-03-01T18:15:00Z");
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { a, b, c };

        var ui = BuildWindow(PanelW, PanelH);
        ui.SetVisible(true);
        yield return null;

        yield return CaptureAndVerify(PanelW, PanelH, "load_project_window_normal.png");
    }

    [UnityTest]
    public IEnumerator MissingFile_IsPaintedAndDoesNothingOnClick()
    {
        var real = MakeProjectFile("lpw_missing_real.kdproj", BuildInfo.Version, "2026-01-05T10:00:00Z");
        string missing = Path.Combine(Application.temporaryCachePath, "lpw_missing_gone.kdproj");
        if (File.Exists(missing)) File.Delete(missing);
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { missing, real };

        var ui = BuildWindow(PanelW, PanelH);
        ui.SetVisible(true);
        yield return null;

        var missingRowTitle = _canvasGo!.transform
            .Find("LoadProjectWindow/LoadBody/LoadBodyBody/LoadBodyBodyContent/Row/Title");
        Assert.IsNotNull(missingRowTitle, "первая строка списка — отсутствующий файл");
        var label = missingRowTitle!.GetComponent<TMPro.TMP_Text>();
        Assert.IsTrue(label.text.Contains("Не найден"), "строка обязана явно называть проблему текстом, не только цветом");
        Assert.AreEqual(UIStyle.HighlightError, label.color);

        var button = missingRowTitle.GetComponentInParent<Button>();
        Assert.IsNotNull(button);
        Assert.DoesNotThrow(() => button!.onClick.Invoke());
        Assert.IsFalse(SaveLoadManager.HasLastPath && SaveLoadManager.LastPath == missing,
            "клик по отсутствующему файлу не имеет права ничего открывать");

        yield return CaptureAndVerify(PanelW, PanelH, "load_project_window_missing_file.png");
    }

    [UnityTest]
    public IEnumerator VersionMismatch_IsPaintedRed()
    {
        var mismatched = MakeProjectFile("lpw_version_old.kdproj", "0.1", "2026-01-05T10:00:00Z");
        var current = MakeProjectFile("lpw_version_current.kdproj", BuildInfo.Version, "2026-01-06T10:00:00Z");
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { mismatched, current };

        var ui = BuildWindow(PanelW, PanelH);
        ui.SetVisible(true);
        yield return null;

        var versionLabel = _canvasGo!.transform
            .Find("LoadProjectWindow/LoadBody/LoadBodyBody/LoadBodyBodyContent/Row/Version");
        Assert.IsNotNull(versionLabel);
        var tmp = versionLabel!.GetComponent<TMPro.TMP_Text>();
        Assert.AreEqual(UIStyle.HighlightError, tmp.color);
        Assert.IsTrue(tmp.text.StartsWith("!"), "несовпадение версии помечено не только цветом, но и '!'");

        yield return CaptureAndVerify(PanelW, PanelH, "load_project_window_version_mismatch.png");
    }

    [UnityTest]
    public IEnumerator ManyRows_Scrolled_StaysWithinTheWindow()
    {
        var paths = new List<string>();
        for (int i = 0; i < RecentProjectsList.Capacity; i++)
            paths.Add(MakeProjectFile($"lpw_many_{i}.kdproj", BuildInfo.Version, "2026-01-01T00:00:00Z"));
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = paths.ToArray();

        var ui = BuildWindow(PanelW, PanelH);
        ui.SetVisible(true);
        yield return null;

        var scroll = _canvasGo!.GetComponentInChildren<ScrollRect>();
        Assert.IsNotNull(scroll, "список из " + RecentProjectsList.Capacity + " строк обязан прокручиваться");
        scroll!.verticalNormalizedPosition = 0f;
        yield return null;

        yield return CaptureAndVerify(PanelW, PanelH, "load_project_window_many_rows_scrolled.png");
    }

    [UnityTest]
    public IEnumerator SmallScreen_HeightStaysAtMostHalfTheScreen()
    {
        const int smallW = 1024, smallH = 576;
        var a = MakeProjectFile("lpw_small_a.kdproj", BuildInfo.Version, "2026-01-05T10:00:00Z");
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { a };

        var ui = BuildWindow(smallW, smallH);
        ui.SetVisible(true);
        yield return null;

        var panel = (RectTransform)ui.WindowRect!;
        Assert.LessOrEqual(panel.sizeDelta.y, smallH * LoadWindowLayout.MaxScreenHeightFraction + 0.5f,
            "высота окна обязана оставаться не больше половины высоты экрана");

        yield return CaptureAndVerify(smallW, smallH, "load_project_window_small_screen.png");
    }
}
