using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

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
        // Возвращаем дефолтный режим ручек: некоторые тесты могли изменить
        // глобальный статик ResizeHandleManager, иначе он течёт в снапшоты
        // следующих тестов (см. AGENTS.md про глобальное состояние снапшотов).
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);

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
        // Кадр всегда 512 на 512, а не размер экрана батча: без явного
        // aspect WorldToViewportPoint считает по экрану, и проверка «деталь
        // влезла в кадр» мерила бы не тот кадр, который потом рисуется.
        cam.aspect = (float)RenderW / RenderH;
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
        // Окно «Сцена» наполняется не по событию создания элемента, а дешёвым
        // поллингом раз в 0.5 с (HierarchyPanelUI.Update). В батч-прогоне кадры
        // идут быстрее интервала, поэтому строки дерева (BasePlate, Кухня,
        // IsoBoard…) то успевали попасть в снапшот канваса, то нет — снапшот
        // флакал. Форсируем перестройку (SetVisible(true) вызывает Refresh)
        // и ждём кадр, чтобы Destroy старых строк успел отработать.
        var hierarchy = HierarchyPanelUI.Instance;
        if (hierarchy != null)
        {
            hierarchy.SetVisible(true);
            yield return null;
        }

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

        var jsonPath = Path.ChangeExtension(path, ".json");
        var canvas = UIManager.Instance?.Canvas;
        if (canvas != null)
            UiSnapshotEngine.CaptureVerified(canvas.gameObject, jsonPath);

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
        KitchenSettings.Instance.NormalView.edgeOutline = true;
        yield return null;

        Vector3 size = MmToUnits(new Vector3Int(lw, openingH, nl));
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_drawer_C_500.png");

        Object.DestroyImmediate(camGo);
    }

    // ── Table isometric screenshot ──────────────────────────

    [UnityTest]
    public IEnumerator IsoTable_2000x750x1000()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateTable(dims, "IsoTable", pos);
        _spawned.Add(go);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_table_2000x750x1000.png");

        Object.DestroyImmediate(camGo);
    }

    // ─ RadiusTable isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoRadiusTable_2000x750x1000()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateRadiusTable(dims, "IsoRadiusTable", pos);
        _spawned.Add(go);
        var radiusTable = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(radiusTable);
        radiusTable.LegInsetMM = 100;

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_radius_table_2000x750x1000.png");

        Object.DestroyImmediate(camGo);
    }

    [UnityTest]
    public IEnumerator IsoRadiusTable_2000x750x1000_ZeroLegInset()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateRadiusTable(dims, "IsoRadiusTableZeroInset", pos);
        _spawned.Add(go);
        var radiusTable = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(radiusTable);
        radiusTable.LegInsetMM = 0;

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_radius_table_2000x750x1000_zero_inset.png");

        Object.DestroyImmediate(camGo);
    }

    // ─ Stool isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoStool_360x450x360_Square()
    {
        yield return RenderStool(new Vector3Int(StoolElement.DefaultWidthMM,
            StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM), 0,
            "IsoStoolSquare", "iso_stool_360x450x360_square.png");
    }

    [UnityTest]
    public IEnumerator IsoStool_360x450x360_Round()
    {
        var dims = new Vector3Int(StoolElement.DefaultWidthMM,
            StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM);
        yield return RenderStool(dims, StoolElement.MaxCornerRadiusMM(dims),
            "IsoStoolRound", "iso_stool_360x450x360_round.png");
    }

    [UnityTest]
    public IEnumerator IsoStool_600x450x360_Capsule()
    {
        var dims = new Vector3Int(600, StoolElement.DefaultHeightMM, 360);
        yield return RenderStool(dims, StoolElement.MaxCornerRadiusMM(dims),
            "IsoStoolCapsule", "iso_stool_600x450x360_capsule.png");
    }

    private IEnumerator RenderStool(Vector3Int dims, int cornerRadiusMM, string name, string png)
    {
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateStool(dims, cornerRadiusMM, name, pos);
        _spawned.Add(go);
        var stool = go.GetComponent<StoolElement>();
        Assert.IsNotNull(stool);
        Assert.AreEqual(cornerRadiusMM, stool!.CornerRadiusMM,
            "снимок обязан показывать ту форму, которую заказали: радиус не должен "
            + "молча схлопнуться при создании");

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, png);

        Object.DestroyImmediate(camGo);
    }

    // ─ Chair isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoChair_400x900x400_Square()
    {
        yield return RenderChair(new Vector3Int(ChairElement.DefaultWidthMM,
            ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM), 0,
            AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT,
            "IsoChairSquare", "iso_chair_400x900x400_square.png");
    }

    [UnityTest]
    public IEnumerator IsoChair_400x900x400_RoundSeat_LowSeat()
    {
        var dims = new Vector3Int(ChairElement.DefaultWidthMM,
            ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM);
        yield return RenderChair(dims, ChairElement.MaxCornerRadiusMM(dims), 350,
            "IsoChairRound", "iso_chair_400x900x400_round.png");
    }

    private IEnumerator RenderChair(Vector3Int dims, int cornerRadiusMM, int seatHeightMM,
        string name, string png)
    {
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateChair(dims, cornerRadiusMM, seatHeightMM, name, pos);
        _spawned.Add(go);
        var chair = go.GetComponent<ChairElement>();
        Assert.IsNotNull(chair);
        Assert.AreEqual(cornerRadiusMM, chair!.CornerRadiusMM,
            "снимок обязан показывать ту форму, которую заказали: радиус не должен "
            + "молча схлопнуться при создании");
        Assert.AreEqual(seatHeightMM, chair.SeatHeightMM,
            "и ту высоту сиденья, которую заказали");
        Assert.IsNotNull(go.transform.Find(ChairElement.BackrestChildName),
            "спинка — отдельный ребёнок с именем: снимок стула без спинки был бы "
            + "снимком табуретки");

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, png);

        Object.DestroyImmediate(camGo);
    }

    // ─ Sofa isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoSofa_2000x800x900_Default()
    {
        yield return RenderSofa(new Vector3Int(SofaElement.DefaultWidthMM,
            SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM),
            SofaElement.DefaultCornerRadiusMM, SofaElement.DefaultSeatHeightMM,
            "IsoSofaDefault", "iso_sofa_2000x800x900_default.png");
    }

    [UnityTest]
    public IEnumerator IsoSofa_1400x800x900_SquareBase_LowSeat()
    {
        yield return RenderSofa(new Vector3Int(1400, SofaElement.DefaultHeightMM,
            SofaElement.DefaultDepthMM), 0, 300,
            "IsoSofaSquare", "iso_sofa_1400x800x900_square.png");
    }

    private IEnumerator RenderSofa(Vector3Int dims, int cornerRadiusMM, int seatHeightMM,
        string name, string png)
    {
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateSofa(dims, cornerRadiusMM, seatHeightMM, name, pos);
        _spawned.Add(go);
        var sofa = go.GetComponent<SofaElement>();
        Assert.IsNotNull(sofa);
        Assert.AreEqual(cornerRadiusMM, sofa!.CornerRadiusMM,
            "снимок обязан показывать ту форму, которую заказали: радиус основания не "
            + "должен молча схлопнуться при создании");
        Assert.AreEqual(seatHeightMM, sofa.SeatHeightMM,
            "и ту высоту основания, которую заказали");
        foreach (var child in new[]
                 {
                     SofaLayout.BackRailName, SofaLayout.ArmCushionLeftName,
                     SofaLayout.ArmCushionRightName, SofaLayout.BackCushionLeftName,
                     SofaLayout.BackCushionRightName,
                 })
            Assert.IsNotNull(go.transform.Find(child),
                "у дивана нет подлокотников — вместо них подушки; снимок без ребёнка «"
                + child + "» показывал бы не тот предмет");

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, png);

        Object.DestroyImmediate(camGo);
    }

    // ─ Radial shelf isometric screenshot ───────────────────

    [UnityTest]
    public IEnumerator IsoRadialShelf_300()
    {
        // Доска 600×400×18 с одним скруглённым углом R=200.
        const int width = 600, depth = 400, thickness = 18, cornerRadius = 200;
        Vector3 pos = new Vector3(0f, thickness * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateRadialShelf(width, depth, thickness, cornerRadius, "IsoRadialShelf", pos);
        _spawned.Add(go);
        var shelf = go.GetComponent<RadialShelfElement>();
        Assert.IsNotNull(shelf);

        // Меш центрирован на pivot — камера смотрит на позицию детали.
        Vector3 size = MmToUnits(new Vector3Int(width, thickness, depth));
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_radial_shelf_300.png");

        Object.DestroyImmediate(camGo);
    }

    // ─ Radial shelf top-down screenshot ───────────────────
    // Строго вид сверху: прямоугольник 600×400 с одним скруглённым
    // углом (правый-верхний, R=200), три угла прямые — как на референсе.
    // Контур (чёрные рёбра AABB) включён: деталь должна лежать внутри него.

    [UnityTest]
    public IEnumerator TopDownRadialShelf_300()
    {
        KitchenSettings.Instance.NormalView.edgeOutline = true;
        const int width = 600, depth = 400, thickness = 18, cornerRadius = 200;
        Vector3 pos = new Vector3(0f, thickness * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateRadialShelf(width, depth, thickness, cornerRadius, "TopDownRadialShelf", pos);
        _spawned.Add(go);
        var shelf = go.GetComponent<RadialShelfElement>();
        Assert.IsNotNull(shelf);

        Vector3 size = MmToUnits(new Vector3Int(width, thickness, depth));
        Vector3 center = pos;

        var camGo = new GameObject("TopDownCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(size.x, size.z) * 0.65f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        // Камера точно над центром, экранный «верх» = +Z: скруглённый угол
        // (x=W, z=D) оказывается справа-сверху.
        camGo.transform.position = center + Vector3.up * 2f;
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _spawned.Add(camGo);

        yield return null;
        yield return null;

        yield return RenderToPng(cam, "topdown_radial_shelf_300.png");

        KitchenSettings.Instance.NormalView.edgeOutline = false;
        Object.DestroyImmediate(camGo);
    }

        // ── Window isometric screenshots ────────────────────────
        // Окно живёт только на стене: ставим стену, окно прилипает к ней
        // (Start → SnapToWall), в стене появляется вырез. Подоконник разворачиваем
        // к камере (окно создаётся с yaw=180°, снап сохраняет разворот).

        private WindowElement SpawnWindowOnWall(string name, Vector3Int dims,
            GlassTint tint = GlassTint.Clear, int sillProtrusionMM = 50)
        {
            SpawnWallAt(name + "_Wall", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));
            var go = ElementFactory.CreateWindow(dims, name, new Vector3(0f, 1.2f, 0f), tint, sillProtrusionMM);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _spawned.Add(go);
            var window = go.GetComponent<WindowElement>();
            Assert.IsNotNull(window);
            return window!;
        }

        [UnityTest]
        public IEnumerator IsoWindow_Default()
        {
            var dims = new Vector3Int(900, 1200, 100);
            var window = SpawnWindowOnWall("IsoWindowDef", dims);
            yield return null; // Start → прилипание к стене

            Assert.IsNotEmpty(window.AttachedWallName, "окно должно прилипнуть к стене");

            Vector3 size = MmToUnits(dims);
            var (camGo, cam) = CreateIsoCamera(window.transform.position, size, 2.5f);
            _spawned.Add(camGo);

            yield return RenderToPng(cam, "iso_window_default.png");

            Object.DestroyImmediate(camGo);
        }

        [UnityTest]
        public IEnumerator IsoWindow_Open()
        {
            var dims = new Vector3Int(900, 1200, 100);
            var window = SpawnWindowOnWall("IsoWindowOpen", dims);
            yield return null;

            window.SetOpen(true);
            window.StepDoor(1f); // мгновенно довести анимацию до конца
            yield return null;

            Vector3 size = MmToUnits(dims);
            var (camGo, cam) = CreateIsoCamera(window.transform.position, size, 2.5f);
            _spawned.Add(camGo);

            yield return RenderToPng(cam, "iso_window_open.png");

            Object.DestroyImmediate(camGo);
        }

        [UnityTest]
        public IEnumerator IsoWindow_Tinted()
        {
            var dims = new Vector3Int(900, 1200, 100);
            var window = SpawnWindowOnWall("IsoWindowTinted", dims, GlassTint.Tinted, 70);
            yield return null;

            Vector3 size = MmToUnits(dims);
            var (camGo, cam) = CreateIsoCamera(window.transform.position, size, 2.5f);
            _spawned.Add(camGo);

            yield return RenderToPng(cam, "iso_window_tinted.png");

            Object.DestroyImmediate(camGo);
        }

        // ── Door isometric screenshot ────────────────────────────

        [UnityTest]
        public IEnumerator IsoDoor_Standard()
        {
            // Пол: плита 3000×3000×100 мм, верхняя плоскость на y=0.
            var floorGo = ElementFactory.CreateFloor(
                new Vector3Int(3000, 100, 3000), "IsoDoorFloor", new Vector3(0f, -0.05f, 0f));
            _spawned.Add(floorGo);

            // Стена: 3000×2500×100 мм.
            SpawnWallAt("IsoDoorWall", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));

            // Дверь: 800×2000 мм, стекло. Разворот 180° — фасад к камере.
            var dims = new Vector3Int(800, 2000, 100);
            var doorGo = ElementFactory.CreateDoor(dims, "IsoDoor", new Vector3(0f, 1.0f, 0f));
            doorGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _spawned.Add(doorGo);
            var door = doorGo.GetComponent<DoorElement>();
            Assert.IsNotNull(door);

            yield return null; // Start → SnapToWall

            Assert.IsNotEmpty(door!.AttachedWallName, "дверь должна прилипнуть к стене");

            Vector3 size = MmToUnits(dims);
            var (camGo, cam) = CreateIsoCamera(door.transform.position, size, 2.5f);
            _spawned.Add(camGo);

            yield return RenderToPng(cam, "iso_door_default.png");

            Object.DestroyImmediate(camGo);
        }

        // ── Room screenshots (4 walls, raised + lowered) ─────────

        [UnityTest]
        public IEnumerator IsoRoom_Raised()
    {
        KitchenSettings.Instance.NormalView.wallsEnabled = true;
        KitchenSettings.Instance.NormalView.lowerNearWalls = false;

        yield return RenderRoom("iso_room_raised.png");
    }

    [UnityTest]
    public IEnumerator IsoRoom_Lowered()
    {
        KitchenSettings.Instance.NormalView.wallsEnabled = true;
        KitchenSettings.Instance.NormalView.lowerNearWalls = true;

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

    [UnityTest]
    public IEnumerator IsoPillar_Default()
    {
        const int midH = 75;
        int totalH = PillarElement.TopHeightMM + midH + PillarElement.BottomHeightMM;
        Vector3 pos = new Vector3(0f, totalH * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreatePillar(midH, "IsoPillar", pos);
        _spawned.Add(go);
        var pillar = go.GetComponent<PillarElement>();
        Assert.IsNotNull(pillar);

        Vector3 size = MmToUnits(new Vector3Int(PillarElement.DiameterMM_Default, totalH, PillarElement.DiameterMM_Default));
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_pillar.png");

        Object.DestroyImmediate(camGo);
    }

    /// <summary>Мойка, врезанная в столешницу: борт лежит на пласти, чаша уходит
    /// в сквозной проём, сзади стоит смеситель.</summary>
    [UnityTest]
    public IEnumerator IsoSink_InCountertop()
    {
        var topDims = new Vector3Int(1200, 600, 38);
        // Столешница лежит на плите основания (низ на y = 0) — иначе она висит
        // в воздухе, валидатор пишет «нет опоры», и снапшот UI ловит бейдж ошибок.
        Vector3 topPos = new Vector3(0f, topDims.z * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var top = SpawnPartAt("IsoCountertop", topDims, topPos);
        // Кладём деталь плашмя: локальная +Z смотрит вверх.
        top.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        // Опускаем мойку в полосу захвата над пластью — оттуда она садится сама.
        float topFaceY = topPos.y + topDims.z * 0.5f * AppConstants.MM_TO_UNITS;
        var sinkGo = ElementFactory.CreateSink("IsoSink", new Vector3(0f, topFaceY + 0.05f, 0f));
        _spawned.Add(sinkGo);
        var sink = sinkGo.GetComponent<SinkElement>();
        Assert.IsNotNull(sink);
        sink!.SnapToPart();
        yield return null;

        Assert.IsTrue(top.HasCutout(sink), "мойка врезана в столешницу");

        Vector3 size = MmToUnits(new Vector3Int(topDims.x, 600, topDims.y));
        var (camGo, cam) = CreateIsoCamera(topPos, size, 1.4f);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, "iso_sink.png");

        Object.DestroyImmediate(camGo);
    }


    // ─ Составные элементы: рамка по мировому AABB, а не по корню ─
    // Шаблон из CONVENTIONS берёт GetComponent на КОРНЕ и считает кадр из dims
    // корня. Для варочной, духовки и посудомойки это неверно дважды: рендерера
    // на корне нет вовсе (ElementRoot.NewEmpty), а дочерние коробки не
    // центрированы на pivot и местами выходят за объявленный габарит — ручка
    // духовки торчит на HANDLE_PROTRUSION_MM/2 дальше половины DEPTH_MM.
    // Поэтому кадр строится по мировому AABB всех рендереров, а тест
    // УТВЕРЖДАЕТ, что деталь целиком попала в кадр, а не просто сохраняет PNG.

    /// <summary>Доля кадра, которая обязана остаться полем вокруг детали.
    /// Ноль означал бы «краем пикселя коснулось — сойдёт».</summary>
    private const float FramePadding = 0.02f;

    private static Bounds RendererBoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers,
            "у элемента нет ни одного рендерера: снимок вышел бы пустым кадром, а тест "
            + "зелёным — " + go.name);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void AssertFitsInFrame(Camera cam, Bounds bounds, string what)
    {
        var min = bounds.min;
        var max = bounds.max;
        var outside = new List<string>();

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);

            var v = cam.WorldToViewportPoint(corner);
            bool inFrame = v.z > 0f
                && v.x >= FramePadding && v.x <= 1f - FramePadding
                && v.y >= FramePadding && v.y <= 1f - FramePadding;
            if (!inFrame) outside.Add(corner + " -> " + v);
        }

        Assert.IsEmpty(outside,
            "деталь не влезла в кадр: снимок обрезает её, и визуальная регрессия смотрит "
            + "на половину предмета, ничего об этом не сообщая. Ровно это даёт кадр по "
            + "корню у составных элементов — " + what + ". Углы вне кадра:\n"
            + string.Join("\n", outside));
    }

    private IEnumerator RenderElementIso(GameObject go, string png, float distanceScale)
    {
        // Дочерние коробки строятся в ApplyDimensions/Start — до кадра их нет.
        yield return null;

        var bounds = RendererBoundsOf(go);
        var (camGo, cam) = CreateIsoCamera(bounds.center, bounds.size, distanceScale);
        _spawned.Add(camGo);

        AssertFitsInFrame(cam, bounds, go.name);

        yield return RenderToPng(cam, png);

        Object.DestroyImmediate(camGo);
    }

    /// <summary>Положительный контроль к хелперу: у трёх составных типов на
    /// корне рендерера НЕТ, поэтому шаблонному GetComponent на корне находить
    /// нечего, а у духовки геометрия ещё и выходит за объявленный габарит. Без
    /// этого теста «рамка по AABB» выглядит перестраховкой, и следующий агент
    /// вернёт шаблон.</summary>
    [UnityTest]
    public IEnumerator IsoAppliances_KeepTheirGeometryInChildren_NotOnTheRoot()
    {
        var cooktop = ElementFactory.CreateCooktop("CtrlCooktop", Vector3.zero);
        var oven = ElementFactory.CreateOven("CtrlOven", new Vector3(2f, 0.3f, 0f));
        var dishwasher = ElementFactory.CreateDishwasher("CtrlDishwasher", new Vector3(4f, 0.4f, 0f));
        _spawned.Add(cooktop);
        _spawned.Add(oven);
        _spawned.Add(dishwasher);
        yield return null;

        foreach (var go in new[] { cooktop, oven, dishwasher })
        {
            Assert.IsNull(go.GetComponent<Renderer>(),
                "рендерера на корне нет — шаблонный кадр по GetComponent на корне "
                + "строить не из чего: " + go.name);
            Assert.Greater(RendererBoundsOf(go).size.magnitude, 0f,
                "а в детях геометрия есть: " + go.name);
        }

        var cooktopBounds = RendererBoundsOf(cooktop);
        Assert.Less(cooktopBounds.center.y, cooktop.transform.position.y - 0.01f,
            "варочная висит НИЖЕ своего pivot: плита толщиной RIM_HEIGHT_MM над нулём, "
            + "короб выреза под ним. Кадр, наведённый на позицию корня, смотрит выше "
            + "детали");

        float declaredHalfDepth = OvenElement.DEPTH_MM * 0.5f * AppConstants.MM_TO_UNITS;
        Assert.Greater(RendererBoundsOf(oven).max.z - oven.transform.position.z,
            declaredHalfDepth + 0.02f,
            "ручка духовки торчит за объявленный габарит DEPTH_MM: кадр по dims корня "
            + "срезает её");
    }

    // ─ Изометрия шести типов, у которых снимка не было ───────

    [UnityTest]
    public IEnumerator IsoPanel_500x716()
    {
        var dims = new Vector3Int(500, 716, 4);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreatePanel(dims, "IsoPanel", pos);
        _spawned.Add(go);
        Assert.IsNotNull(go.GetComponent<PanelElement>(),
            "ХДФ-задник обязан быть панелью, а не обычной доской");

        yield return RenderElementIso(go, "iso_panel_500x716.png", 2.5f);
    }

    [UnityTest]
    public IEnumerator IsoAssembledFacade_Blind()
    {
        yield return RenderAssembledFacade(AssembledFill.Blind,
            "IsoAssembledBlind", "iso_assembled_facade_blind.png");
    }

    [UnityTest]
    public IEnumerator IsoAssembledFacade_Glass()
    {
        yield return RenderAssembledFacade(AssembledFill.Glass,
            "IsoAssembledGlass", "iso_assembled_facade_glass.png");
    }

    private IEnumerator RenderAssembledFacade(AssembledFill fill, string name, string png)
    {
        var dims = new Vector3Int(450, 716, 22);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateAssembledFacade(dims, name, pos, fill);
        _spawned.Add(go);
        var facade = go.GetComponent<AssembledFacadeElement>();
        Assert.IsNotNull(facade, "сборный фасад обязан быть сборным фасадом, а не доской");
        Assert.AreEqual(fill, facade!.Fill,
            "снимок обязан показывать то заполнение, которое заказали: два снимка с "
            + "одинаковой картинкой не отличили бы глухую вставку от стекла");

        yield return RenderElementIso(go, png, 2.5f);
    }

    [UnityTest]
    public IEnumerator IsoCooktop_Bosch()
    {
        // Короб выреза уходит вниз от pivot: поднимаем корень так, чтобы его низ
        // лёг на пол.
        float lift = (CooktopElement.DEFAULT_HEIGHT_MM - CooktopElement.RIM_HEIGHT_MM)
            * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateCooktop("IsoCooktop", new Vector3(0f, lift, 0f));
        _spawned.Add(go);
        Assert.IsNotNull(go.GetComponent<CooktopElement>(),
            "варочная обязана быть варочной, а не доской");

        yield return RenderElementIso(go, "iso_cooktop.png", 3f);
    }

    [UnityTest]
    public IEnumerator IsoOven_Bosch()
    {
        float half = OvenElement.FACADE_HEIGHT_MM * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateOven("IsoOven", new Vector3(0f, half, 0f));
        _spawned.Add(go);
        Assert.IsNotNull(go.GetComponent<OvenElement>(),
            "духовка обязана быть духовкой, а не доской");

        yield return RenderElementIso(go, "iso_oven.png", 3f);
    }

    [UnityTest]
    public IEnumerator IsoDishwasher_Bosch()
    {
        float half = DishwasherElement.ModelDimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateDishwasher("IsoDishwasher", new Vector3(0f, half, 0f));
        _spawned.Add(go);
        Assert.IsNotNull(go.GetComponent<DishwasherElement>(),
            "посудомойка обязана быть посудомойкой, а не доской");

        yield return RenderElementIso(go, "iso_dishwasher.png", 3f);
    }

    [UnityTest]
    public IEnumerator IsoLightSource_Default()
    {
        var go = ElementFactory.CreateLightSource("IsoLamp", new Vector3(0f, 1.5f, 0f));
        _spawned.Add(go);
        Assert.IsNotNull(go.GetComponent<LightSourceElement>(),
            "светильник обязан быть светильником, а не доской");
        Assert.IsNotNull(go.GetComponentInChildren<Light>(),
            "светильник без источника света — просто шар: снимок обязан показывать "
            + "включённую лампу");

        yield return RenderElementIso(go, "iso_light_source.png", 3f);
    }
}
