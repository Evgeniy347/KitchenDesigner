using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// PlayMode: изометрические снэпшоты 3D-объектов через Camera → RenderTexture.
/// Снимает фасады (все 18 DoorMode, дверь открыта), деталь и помещение
/// (стены подняты/опущены). Ракурс — 3/4 спереди-справа-сверху.
/// </summary>
public class IsoScreenshotTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;
    private const float IsoFov = 45f;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    // ── Render helpers ───────────────────────────────────────

    /// <summary>Направление камеры: 3/4 спереди-справа-сверху (~30°).</summary>
    private static readonly Vector3 IsoDir =
        new Vector3(0.5f, 0.5f, -0.866f).normalized;

    /// <summary>Создать камеру для изометрического рендера объекта.
    /// Расстояние вычисляется из размера объекта так, чтобы он занимал
    /// ~70% высоты кадра. Ракурс 3/4 спереди-справа-сверху.</summary>
    private (GameObject camGo, Camera cam) CreateIsoCamera(Vector3 center, Vector3 size, float distanceScale)
    {
        var camGo = new GameObject("IsoCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = IsoFov;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        float maxDim = Mathf.Max(size.x, size.y, size.z);
        float distance = Mathf.Max(maxDim * distanceScale, 0.5f);

        camGo.transform.position = center + IsoDir * distance;
        camGo.transform.LookAt(center);

        return (camGo, cam);
    }

    private static Vector3 MmToUnits(Vector3Int mm) =>
        new Vector3(mm.x, mm.y, mm.z) * AppConstants.MM_TO_UNITS;

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
        Debug.Log($"[ISO] Saved: {path}");

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    // ── Spawn helpers ────────────────────────────────────────

    private KitchenElement SpawnFacadeAt(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private KitchenElement SpawnPartAt(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private KitchenElement SpawnWallAt(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreateWall(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    // ── Facade isometric screenshots (18 DoorModes, door OPEN) ─

    [UnityTest]
    public IEnumerator IsoFacade_AllDoorModes()
    {
        var dims = new Vector3Int(350, 556, 18);
        // Поднимаем фасад на половину высоты — стоит на полу, центр на y = h/2.
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var facade = SpawnFacadeAt("IsoFacade", dims, pos);
        Assert.IsNotNull(facade);
        var fe = facade as FacadeElement;
        Assert.IsNotNull(fe);

        // Камера смотрит на центр фасада.
        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        for (int i = 0; i < FacadeDoor.Count; i++)
        {
            var mode = (DoorMode)i;
            fe!.Mode = mode;
            fe.ForceClose();
            yield return null;

            // Открываем дверцу и мгновенно доводим анимацию до конца.
            fe.SetOpen(true);
            fe.StepDoor(1f); // dt=1 > OpenSeconds(0.4) → прогресс доходит до 1
            yield return null;

            string fileName = $"iso_facade_{i:D2}.png";
            yield return RenderToPng(cam, fileName);
        }

        Object.DestroyImmediate(camGo);
    }

    // ── Board (part) isometric screenshot ────────────────────

    [UnityTest]
    public IEnumerator IsoBoard_537x716()
    {
        var dims = new Vector3Int(537, 716, 18);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var board = SpawnPartAt("IsoBoard", dims, pos);
        Assert.IsNotNull(board);

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_board_537x716.png");

        Object.DestroyImmediate(camGo);
    }

    // ── Drawer (GTV AXIS PRO) isometric screenshot ───────────

    [UnityTest]
    public IEnumerator IsoDrawer_TypeC_500()
    {
        // Тип C (боковина 168), длина 500, проём LW=400, антрацит.
        const int lw = 400;
        const int nl = 500;
        var type = DrawerType.C;

        // Контурный бокс ящика (проём) ставится низом на пол.
        int openingH = DrawerConstants.GetMinOpeningHeight(type);
        Vector3 pos = new Vector3(0f, openingH * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateDrawer(type, nl, DrawerColor.Anthracite, lw, "IsoDrawer", pos);
        _spawned.Add(go);
        var drawer = go.GetComponent<DrawerElement>();
        Assert.IsNotNull(drawer);

        // Контур (чёрные рёбра) по параллелепипеду с зазорами.
        KitchenSettings.Instance.EdgeOutline = true;
        yield return null;

        Vector3 size = MmToUnits(new Vector3Int(lw, openingH, nl));
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_drawer_C_500.png");

        Object.DestroyImmediate(camGo);
    }

    // ── Room screenshots (4 walls, raised + lowered) ─────────

    [UnityTest]
    public IEnumerator IsoRoom_Raised()
    {
        KitchenSettings.Instance.WallsEnabled = true;
        KitchenSettings.Instance.LowerNearWalls = false;

        yield return RenderRoom("iso_room_raised.png");
    }

    [UnityTest]
    public IEnumerator IsoRoom_Lowered()
    {
        KitchenSettings.Instance.WallsEnabled = true;
        KitchenSettings.Instance.LowerNearWalls = true;

        yield return RenderRoom("iso_room_lowered.png");
    }

    private IEnumerator RenderRoom(string fileName)
    {
        // Помещение 3000×3000 мм, стены 3000×2500×100 мм.
        const float wallH = 2.5f;
        const float halfRoom = 1.5f;

        // 4 стены по периметру.
        SpawnWallAt("Wall_N", new Vector3Int(3000, 2500, 100), new Vector3(0, wallH * 0.5f, -halfRoom));
        SpawnWallAt("Wall_S", new Vector3Int(3000, 2500, 100), new Vector3(0, wallH * 0.5f, halfRoom));
        SpawnWallAt("Wall_W", new Vector3Int(100, 2500, 3000), new Vector3(-halfRoom, wallH * 0.5f, 0));
        SpawnWallAt("Wall_E", new Vector3Int(100, 2500, 3000), new Vector3(halfRoom, wallH * 0.5f, 0));

        yield return null;

        // Выключаем основную камеру, чтобы WallManager использовал изо-камеру
        // для определения «ближних» стен (LowerNearWalls).
        _mainCamera!.SetActive(false);

        // Камера снаружи, смотрит в центр помещения.
        Vector3 roomSize = new Vector3(3f, wallH, 3f);
        var (camGo, cam) = CreateIsoCamera(Vector3.zero, roomSize, 2.5f);
        camGo.tag = "MainCamera";
        _spawned.Add(camGo);

        // Дать WallManager.LateUpdate отработать с новой камерой.
        yield return null;
        yield return null;

        yield return RenderToPng(cam, fileName);

        camGo.tag = "Untagged";
        _mainCamera!.SetActive(true);
        Object.DestroyImmediate(camGo);
    }
}
