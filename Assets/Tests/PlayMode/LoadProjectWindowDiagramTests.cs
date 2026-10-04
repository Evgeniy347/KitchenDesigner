using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private string? _prevLastPath;
    private readonly List<string> _tempFiles = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _recentBackup = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
        _prevLastPath = SaveLoadManager.LastPath;
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
        SaveLoadManager.LastPath = _prevLastPath!;

        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        _tempFiles.Clear();

        yield return null;
    }

    // Дата в столбце "Изменён" читается с диска (File.GetLastWriteTimeUtc), а не из
    // JSON — File.WriteAllText сам метит файл текущим моментом, и без явной
    // перестановки золотой UI-снапшот несёт время ПРОГОНА и красится на следующий
    // день. Оба штампа (mtime и, на будущее, ctime) фиксируются одним значением для
    // всех фикстур этого класса, чтобы дата в кадре не зависела от часов машины.
    private static readonly System.DateTime FixedModifiedUtc =
        new System.DateTime(2026, 4, 1, 9, 15, 0, System.DateTimeKind.Utc);

    private string MakeProjectFile(string name, string appVersion, string createdAtUtc)
    {
        string path = Path.Combine(Application.temporaryCachePath, name);
        File.WriteAllText(path,
            "{\"version\":1,\"appVersion\":\"" + appVersion + "\",\"createdAtUtc\":\"" + createdAtUtc + "\"}");
        File.SetLastWriteTimeUtc(path, FixedModifiedUtc);
        File.SetCreationTimeUtc(path,
            System.DateTime.TryParse(createdAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal, out var created)
                ? created
                : FixedModifiedUtc);
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

    // Баг-репорт: пользователь работает в проекте, открывает «Загрузить» — список
    // недавних пуст. RecentProjects.Paths() сидирует список текущим проектом
    // (SaveLoadManager.LastPath), когда список пуст, а текущий проект есть — этот
    // кадр показывает результат: одна строка вместо «Недавних проектов пока нет».
    [UnityTest]
    public IEnumerator EmptyRecentList_ButACurrentProjectIsOpen_ShowsSeededRow_SavesPng()
    {
        var current = MakeProjectFile("lpw_seeded_current.kdproj", BuildInfo.Version, "2026-03-20T12:00:00Z");
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new string[0];
        SaveLoadManager.LastPath = current;

        var ui = BuildWindow(PanelW, PanelH);
        ui.SetVisible(true);
        yield return null;

        var table = _canvasGo!.GetComponentInChildren<LoadProjectWindowUI>().Table;
        Assert.AreEqual(1, table.ShownRows.Count, "текущий проект обязан появиться строкой вместо пустого состояния");
        Assert.IsNotNull(table.RowRect(0).Find(RowBadge.NodePrefix + "current"), "и пометиться меткой «открыт»");

        yield return CaptureAndVerify(PanelW, PanelH, "load_project_window_seeded_current_project.png");
    }

    [UnityTest]
    public IEnumerator MissingFile_IsLabelledRed_AndCannotBeOpened()
    {
        var real = MakeProjectFile("lpw_missing_real.kdproj", BuildInfo.Version, "2026-01-05T10:00:00Z");
        string missing = Path.Combine(Application.temporaryCachePath, "lpw_missing_gone.kdproj");
        if (File.Exists(missing)) File.Delete(missing);
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { missing, real };

        var ui = BuildWindow(PanelW, PanelH);
        ui.SetVisible(true);
        yield return null;

        var table = ui.Table;
        var gone = table.ShownRows.Single(r => !r.Enabled);
        int index = table.ShownRows.ToList().IndexOf(gone);
        var badge = table.RowRect(index).Find(RowBadge.NodePrefix + "missing")!.GetComponentInChildren<TMPro.TMP_Text>();
        Assert.AreEqual("файл не найден", badge.text, "строка обязана явно называть проблему текстом, не только цветом");
        Assert.AreEqual(UIStyle.TextError, badge.color);

        table.Activate(gone);
        Assert.IsFalse(SaveLoadManager.HasLastPath && SaveLoadManager.LastPath == missing,
            "двойной клик по отсутствующему файлу не имеет права ничего открывать");

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

        var mismatch = ui.Table.ShownRows.ToList().FindIndex(r => ((RecentProjectRow)r.Tag!).VersionMismatch);
        Assert.GreaterOrEqual(mismatch, 0, "предпосылка: файл прежней версии в списке");
        var tmp = ui.Table.CellLabel(mismatch, LoadProjectRows.VersionColumn)!;
        Assert.AreEqual(UIStyle.TextError, tmp.color);
        Assert.IsTrue(tmp.text.Contains("!"), "несовпадение версии помечено не только цветом, но и '!'");

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

        var scroll = ui.Table.Body.Scroll;
        Assert.Greater(ui.Table.ContentHeight, ui.Table.Body.Viewport.rect.height,
            "список из " + RecentProjectsList.Capacity + " строк обязан прокручиваться");
        scroll.verticalNormalizedPosition = 0f;
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
