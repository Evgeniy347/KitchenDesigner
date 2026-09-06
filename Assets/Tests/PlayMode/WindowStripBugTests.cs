using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode-тест: стена с двумя пересекающимися окнами — проверка
/// меша на вырожденные треугольники (баг с тонкой полосой в зоне перекрытия)
/// плюс изометрический скриншот для визуальной проверки.</summary>
public class WindowStripBugTests
{
    private const int RenderW = 1024;
    private const int RenderH = 1024;
    private const float IsoFov = 45f;

    private KitchenSettingsData? _settingsBackup;
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

        // Bootstrap открывает демо-проект, а с ним приезжают ЕГО настройки вида.
        // Этот тест про построение меша, и опускание стен ему безразлично — но не
        // наоборот: у опущенной стены вырезов окон нет намеренно (полоска в 100 мм
        // проходит НИЖЕ подоконника, и стена там настоящая). Стоит демо-файлу
        // приехать с включённым «опускать все стены» — и проверка меряет пустой
        // короб вместо выреза. Исход теста не имеет права зависеть от того, какие
        // галочки пользователь сохранил в своём проекте.
        var settings = KitchenSettings.Instance;
        _settingsBackup = settings != null ? settings.ToData() : null;
        if (settings != null)
        {
            settings.NormalView.lowerAllWalls = false;
            settings.NormalView.lowerNearWalls = false;
        }
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
        var settings = KitchenSettings.Instance;
        if (settings != null && _settingsBackup != null) settings.ApplyFrom(_settingsBackup);
        _settingsBackup = null;

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

    /// <summary>Строит стену с двумя ПЕРЕСЕКАЮЩИМИСЯ окнами (геометрия взята
    /// с проекта, где проявлялся баг: центры по Y 1.391 и 2.032 при высоте
    /// 1200 мм → перекрытие ~559 мм, плюс перекрытие по X). Проверяет, что
    /// в меше стены нет вырожденных треугольников — та самая тонкая полоса
    /// в зоне overlap — и рендерит скриншот для визуальной проверки.</summary>
    [UnityTest]
    public IEnumerator TwoOverlappingWindows_NoDegenerateTriangles()
    {
        var wallDims = new Vector3Int(3000, 3000, 100);
        var wallGo = ElementFactory.CreateWall(wallDims, "StripWall", new Vector3(0f, 1.5f, 0f));
        _spawned.Add(wallGo);

        // Окна пересекаются и по Y (1.432..1.991), и по X (−0.15..0.15).
        var winDims = new Vector3Int(900, 1200, 100);
        var win1 = ElementFactory.CreateWindow(winDims, "StripWin_Lower", new Vector3(-0.3f, 1.391f, 0f));
        _spawned.Add(win1);
        var win2 = ElementFactory.CreateWindow(winDims, "StripWin_Upper", new Vector3(0.3f, 2.032f, 0f));
        _spawned.Add(win2);

        // Привязка ЯВНАЯ: SnapToWall берёт ближайшую стену из всей сцены, а Bootstrap
        // мог загрузить в неё целый проект. Тогда окна уезжают на чужую стену, меш
        // StripWall остаётся целым, и тест падает со словами «окна не прилипли» —
        // про порядок тестов, а не про полосу в зоне перекрытия.
        var wall = wallGo.GetComponent<Wall>()!;
        win1.GetComponent<WindowElement>()!.AttachToWall(wall);
        win2.GetComponent<WindowElement>()!.AttachToWall(wall);

        Assert.AreEqual(2, wall.AttachedWindows.Count,
            "оба окна обязаны висеть на СВОЕЙ стене — иначе проверка меша ничего не значит");

        // Даём стене перестроить меш (RebuildMesh вызывается в Update).
        yield return null;
        yield return null;
        yield return null;

        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у стены нет меша");

        var verts = mesh!.vertices;
        var tris = mesh.triangles;

        // Без этой проверки тест был бы пустым: если бы окна не зарегистрировались
        // на стене, меш остался бы простым параллелепипедом без вырожденных рёбер.
        int plainVerts = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout>()).vertexCount;
        Assert.Greater(verts.Length, plainVerts,
            "в меше стены нет вырезов — окна не прилипли, проверка полосы ничего не значит");
        for (int t = 0; t < tris.Length; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            float ab = (a - b).magnitude;
            float bc = (b - c).magnitude;
            float ca = (c - a).magnitude;
            Assert.GreaterOrEqual(ab, WallMeshBuilder.MinCellNorm, $"AB={ab} tri {t / 3}: {a}→{b}");
            Assert.GreaterOrEqual(bc, WallMeshBuilder.MinCellNorm, $"BC={bc} tri {t / 3}: {b}→{c}");
            Assert.GreaterOrEqual(ca, WallMeshBuilder.MinCellNorm, $"CA={ca} tri {t / 3}: {c}→{a}");
        }

        // Камера смотрит на центр стены.
        var (camGo, cam) = CreateCamera(wallGo.transform.position, MmToUnits(wallDims));
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "bug_window_strip.png");
    }

    private static Vector3 MmToUnits(Vector3Int mm) =>
        new Vector3(mm.x, mm.y, mm.z) * AppConstants.MM_TO_UNITS;
}
