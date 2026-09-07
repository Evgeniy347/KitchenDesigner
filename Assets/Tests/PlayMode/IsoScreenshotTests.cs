using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Tests;

/// <summary>
/// PlayMode: изометрические снэпшоты 3D-объектов через Camera → RenderTexture.
/// Снимает фасады (все 18 DoorMode, дверь открыта), деталь и помещение
/// (стены подняты/опущены). Ракурс — 3/4 справа-сверху со стороны -Z.
///
/// «Со стороны -Z» — это НЕ «спереди», и раньше здесь было написано именно
/// «спереди». У мебели со спинкой спинка по соглашению проекта смотрит в -Z
/// (McpGuideTexts про стул: panel at the BACK (-Z)), то есть камера стоит с
/// ТОЙ ЖЕ стороны и снимает такой элемент СО СПИНЫ. На iso_chair_*.png мы всё
/// это время смотрели на щит спинки, а не на стул. Терпимо для стула, у
/// которого сиденье всё равно торчит сбоку, и неприемлемо для дивана, где
/// полка спинки во всю ширину закрывает все четыре подушки — то есть ровно
/// то, ради чего снимок и делается. Поэтому диван развёрнут в своём тесте, а
/// не правкой общего IsoDir: правка вектора пересняла бы каждый чужой эталон.
/// Связку держит IsoCamera_StandsOnTheSameSideAsTheBackOfFurniture.
/// </summary>
public class IsoScreenshotTests : ElementFrameTests
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

    /// <summary>Направление камеры: 3/4 справа-сверху со стороны -Z (~30°).
    /// Спинка мебели по соглашению смотрит туда же, поэтому такой элемент
    /// попадает в кадр СО СПИНЫ — см. сводку класса.</summary>
    private static readonly Vector3 IsoDir =
        new Vector3(0.5f, 0.5f, -0.866f).normalized;

    /// <summary>Пол дистанции: мелкий объект не подпускается к объективу
    /// ближе полуметра, иначе винтовая опора занимала бы весь кадр без
    /// единого ориентира вокруг. Для КРУПНЫХ ПЛАНОВ это ровно наоборот —
    /// там пол не даёт кадру стать меньше ~414 мм, и узел, ради которого
    /// кадр снимают, тонет в общем виде. См. CreateCloseUpCamera.</summary>
    private const float MinCameraDistance = 0.5f;

    /// <summary>Создать камеру для изометрического рендера объекта.
    /// Расстояние вычисляется из размера объекта так, чтобы он занимал
    /// ~70% высоты кадра. Ракурс 3/4 справа-сверху со стороны -Z.</summary>
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
        float distance = Mathf.Max(maxDim * distanceScale, MinCameraDistance);

        camGo.transform.position = center + IsoDir * distance;
        camGo.transform.LookAt(center);

        return (camGo, cam);
    }

    private static Vector3 MmToUnits(Vector3Int mm) =>
        new Vector3(mm.x, mm.y, mm.z) * AppConstants.MM_TO_UNITS;

    private IEnumerator RenderToPng(Camera cam, string fileName) =>
        RenderToPng(cam, fileName, Path.GetFileNameWithoutExtension(fileName) + ".json");

    /// <summary>panelSnapshotFile == null — снять только 3D-кадр, без эталона
    /// панели. Нужно там, где один тест рисует НЕСКОЛЬКО кадров одной и той же
    /// сцены: панель от кадра к кадру не меняется, и каждый лишний эталон —
    /// это ещё один файл, который придётся принимать вручную после любой
    /// правки сайдбара.</summary>
    private IEnumerator RenderToPng(Camera cam, string fileName, string? panelSnapshotFile)
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

        yield return CaptureFramePng(cam, fileName, RenderW, RenderH);

        var canvas = UIManager.Instance?.Canvas;
        if (canvas != null && panelSnapshotFile != null)
        {
            string dir = Path.Combine(Application.dataPath, "..", "test-results");
            UiSnapshotEngine.CaptureVerified(canvas.gameObject, Path.Combine(dir, panelSnapshotFile));
        }
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

    /// <summary>Поставить элемент на плиту ПО ТОЙ ЖЕ коробке, которую читает
    /// валидатор. У фасада и ХДФ-панели зазоры не сжимают коробку, а РАСШИРЯЮТ
    /// её (GappedBox.CornerUnits: minY = -h/2 - gapBottom), поэтому элемент,
    /// посаженный по половине ФИЗИЧЕСКОЙ высоты, тонет в опорной плите ровно на
    /// нижний зазор — 2 мм у фасада, 1 мм у панели. И то и другое больше
    /// Tolerance.ContactMm, так что ValidationCore видит Overlap с плитой, а
    /// ElementHighlighter красит элемент розовым _invalidMaterial. Восемнадцать
    /// кадров фасада и кадр панели месяцами показывали не декор, а тинт
    /// нарушения. Сдвиг считается по GetVertices() — тому же источнику, что и
    /// сама проверка: выписанное отдельно число разошлось бы с зазорами по
    /// умолчанию при первой же их правке.</summary>
    private static void StandOnFloor(KitchenElement element)
    {
        float minY = float.MaxValue;
        foreach (var v in element.GetVertices()) minY = Mathf.Min(minY, v.y);
        element.transform.position += new Vector3(0f, -minY, 0f);
    }

    [UnityTest]
    public IEnumerator IsoFacade_AllDoorModes()
    {
        var dims = new Vector3Int(350, 556, 18);
        // Половина ФИЗИЧЕСКОЙ высоты — только черновая посадка: коробка
        // валидации шире меша на зазоры, и на пол фасад ставит StandOnFloor.
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var facade = SpawnFacadeAt("IsoFacade", dims, pos);
        Assert.IsNotNull(facade);
        var fe = facade as FacadeElement;
        Assert.IsNotNull(fe);
        StandOnFloor(facade!);

        // Камера смотрит на центр фасада.
        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(facade!.transform.position, size, 2.5f);
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

            // Режим дверцы меняет 3D-модель, а не панель: все восемнадцать
            // эталонов UI были байт-в-байт одним файлом (один md5 на
            // ui_iso_facade_00..17). Снимаем панель ОДИН раз — иначе принятие
            // одной кнопки сайдбара стоит восемнадцати прогонов PlayMode,
            // потому что залогированная ошибка обрывает корутину и за прогон
            // рождается ровно один кандидат.
            yield return RenderToPng(cam, $"iso_facade_{i:D2}.png",
                i == 0 ? "iso_facade_panel.json" : (string?)null);
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
        yield return RenderStool(dims, FurnitureLayout.MaxCornerRadiusMM(dims),
            "IsoStoolRound", "iso_stool_360x450x360_round.png");
    }

    [UnityTest]
    public IEnumerator IsoStool_600x450x360_Capsule()
    {
        var dims = new Vector3Int(600, StoolElement.DefaultHeightMM, 360);
        yield return RenderStool(dims, FurnitureLayout.MaxCornerRadiusMM(dims),
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
        yield return RenderChair(dims, FurnitureLayout.MaxCornerRadiusMM(dims), 350,
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

    /// <summary>Камера проекта стоит на -Z (IsoDir), а спинка мебели по
    /// соглашению смотрит в -Z: у стула это дало снимок СЗАДИ, где вся
    /// геометрия сиденья спрятана за щитом спинки. У дивана то же соглашение
    /// прячет за полкой спинки все четыре подушки — то есть ровно то, что
    /// снимок обязан показывать. Поэтому диван развёрнут на 180 градусов:
    /// разворот не трогает габаритную коробку, поэтому проверка «влез в кадр»
    /// остаётся честной, а IsoDir общий для всех типов и его правка
    /// пересняла бы каждый чужой эталон.</summary>
    private const float FrontTowardsCameraDeg = 180f;

    /// <summary>Держит связку, из-за которой разворот вообще понадобился:
    /// камера стоит со стороны -Z, и спинка мебели смотрит туда же. Пока оба
    /// факта верны, элемент со спинкой попадает в кадр СО СПИНЫ, и тип, чья
    /// геометрия живёт перед спинкой, обязан развернуться.
    ///
    /// Тест сторожит ОБА конца связки, а не только один: перевернут IsoDir
    /// или переедет спинка на +Z — разворот дивана станет вредным, и красное
    /// покажет на константу, которую надо убрать. Прозой это уже один раз не
    /// удержалось: комментарий про «ракурс спереди» врал ровно столько, сколько
    /// существовал, и никто не заметил, потому что PNG никто не открывал.</summary>
    [Test]
    public void IsoCamera_StandsOnTheSameSideAsTheBackOfFurniture_SoASofaMustTurnAround()
    {
        Assert.Less(IsoDir.z, 0f,
            "камера смотрит со стороны -Z: это и есть та сторона, куда по соглашению "
            + "проекта обращена спинка мебели");

        var dims = new Vector3Int(SofaElement.DefaultWidthMM,
            SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM);
        Assert.Less(SofaLayout.BackRail(dims, SofaElement.DefaultSeatHeightMM).CentreMM.z, 0f,
            "спинка дивана стоит в -Z — как щит спинки стула; разворачивать соглашение "
            + "ради одного типа значило бы поставить диван и стул в одной комнате "
            + "спинками навстречу");

        Assert.AreEqual(180f, FrontTowardsCameraDeg,
            "раз обе стороны связки сошлись, диван обязан развернуться к камере лицом: "
            + "иначе снимок показывает полку спинки во всю ширину, а не четыре подушки");
    }

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

        go.transform.rotation = Quaternion.Euler(0f, FrontTowardsCameraDeg, 0f);
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

    // ─ Bed isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoBed_1800x900x2000_DoubleWithHeadboard()
    {
        yield return RenderBed(BedLayout.DefaultDimensions(true, true), true, true,
            "IsoBedDouble", "iso_bed_1800x900x2000_double.png");
    }

    [UnityTest]
    public IEnumerator IsoBed_900x600x2000_SingleWithoutHeadboard()
    {
        yield return RenderBed(BedLayout.DefaultDimensions(false, false), false, false,
            "IsoBedSingle", "iso_bed_900x600x2000_single_no_headboard.png", false);
    }

    /// <summary>capturePanel == false — второй кадр той же сцены: панель между
    /// двумя снимками кровати не меняется, а каждый лишний эталон панели пришлось
    /// бы принимать руками после любой правки сайдбара.</summary>
    private IEnumerator RenderBed(Vector3Int dims, bool isDouble, bool hasHeadboard,
        string name, string png, bool capturePanel = true)
    {
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateBed(dims, isDouble, hasHeadboard, name, pos);
        _spawned.Add(go);

        go.transform.rotation = Quaternion.Euler(0f, FrontTowardsCameraDeg, 0f);
        var bed = go.GetComponent<BedElement>();
        Assert.IsNotNull(bed, "фабрика обязана вернуть именно BedElement");
        Assert.AreEqual(dims, bed!.DimensionsMM,
            "снимок обязан показывать тот габарит, который заказали: переключатели типа "
            + "сбрасывают размер, и фабрика не должна сделать это ПОСЛЕ выставления размера");
        Assert.AreEqual(isDouble, bed.IsDouble, "и тот тип, который заказали");
        Assert.AreEqual(hasHeadboard, bed.HasHeadboard, "и то изголовье, которое заказали");

        Assert.AreEqual(hasHeadboard, go.transform.Find(BedLayout.HeadboardName) != null,
            "спинка — отдельный ребёнок с именем: кровать без спинки не имеет права её "
            + "показывать, а кровать со спинкой — прятать");
        Assert.IsNotNull(go.transform.Find(BedLayout.MattressName),
            "снимок кровати без матраса был бы снимком пустой рамы");
        for (int i = 0; i < BedLayout.DoublePillowCount; i++)
            Assert.AreEqual(i < BedLayout.PillowCount(isDouble),
                go.transform.Find(BedLayout.PillowName(i)) != null,
                "подушек ровно столько, сколько диктует тип: одна у односпальной, две у "
                + "двуспальной — и лишняя обязана исчезнуть, а не остаться от прошлой сборки");

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return capturePanel
            ? RenderToPng(cam, png)
            : RenderToPng(cam, png, null);

        Object.DestroyImmediate(camGo);
    }

    // ─ Pouffe isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoPouffe_450x400x450_Rounded()
    {
        yield return RenderPouffe(DefaultPouffeDims(), PouffeElement.DefaultCornerRadiusMM,
            PouffeElement.DefaultSeatThicknessMM, "IsoPouffeRounded",
            "iso_pouffe_450x400x450_rounded.png");
    }

    [UnityTest]
    public IEnumerator IsoPouffe_450x400x450_Square()
    {
        yield return RenderPouffe(DefaultPouffeDims(), 0,
            PouffeElement.DefaultSeatThicknessMM, "IsoPouffeSquare",
            "iso_pouffe_450x400x450_square.png", false);
    }

    /// <summary>Круглый пуфик — предельный случай формы: и тумба, и сидушка
    /// обязаны читаться ОДНОЙ окружностью, сидушка только уже на отступ.
    /// Раньше этот кадр показывал обратное — подушка CushionMesh не умеет
    /// круглый план (её угловой радиус подрезан собственной толщиной), и на
    /// круглой тумбе лежал квадрат, вписанный в окружность: «крышка от чужой
    /// кастрюли». Сидушка перешла на SoftSlabSurface, и кадр существует, чтобы
    /// это было видно глазами, а не только в арифметике PouffeLayoutTests.</summary>
    [UnityTest]
    public IEnumerator IsoPouffe_450x400x450_Round()
    {
        var dims = DefaultPouffeDims();
        yield return RenderPouffe(dims, PouffeElement.MaxCornerRadiusMM(dims),
            PouffeElement.DefaultSeatThicknessMM, "IsoPouffeRound",
            "iso_pouffe_450x400x450_round.png", false);
    }

    private static Vector3Int DefaultPouffeDims() => new Vector3Int(
        PouffeElement.DefaultWidthMM, PouffeElement.DefaultHeightMM,
        PouffeElement.DefaultDepthMM);

    private IEnumerator RenderPouffe(Vector3Int dims, int cornerRadiusMM,
        int seatThicknessMM, string name, string png, bool capturePanel = true)
    {
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreatePouffe(dims, cornerRadiusMM, seatThicknessMM, name, pos);
        _spawned.Add(go);

        var pouffe = go.GetComponent<PouffeElement>();
        Assert.IsNotNull(pouffe, "фабрика обязана вернуть именно PouffeElement");
        Assert.AreEqual(dims, pouffe!.DimensionsMM,
            "снимок обязан показывать заказанный габарит: свойства пуфика подрезаются "
            + "габаритом, поэтому фабрика обязана выставить его ПЕРВЫМ");
        Assert.AreEqual(cornerRadiusMM, pouffe.CornerRadiusMM,
            "и ту форму, которую заказали: радиус не должен молча схлопнуться");
        Assert.AreEqual(seatThicknessMM, pouffe.SeatThicknessMM,
            "и ту толщину сидушки, которую заказали");
        Assert.IsNotNull(go.transform.Find(PouffeElement.SeatChildName),
            "сидушка — отдельный именованный ребёнок: снимок пуфика без неё был бы "
            + "снимком обитой тумбы");
        Assert.IsNotNull(go.GetComponent<MeshRenderer>(),
            "обитая тумба живёт на КОРНЕ: без её рендерера кадр показал бы одну "
            + "сидушку, висящую в воздухе");

        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return capturePanel
            ? RenderToPng(cam, png)
            : RenderToPng(cam, png, null);

        Object.DestroyImmediate(camGo);
    }

    // ─ Toilet isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoToilet_360x790x660_Default()
    {
        yield return RenderToilet(ToiletElement.DefaultSeatHeightMM, "IsoToilet",
            "iso_toilet_360x790x660.png");
    }

    /// <summary>Кадр на ПРЕДЕЛЬНОЙ высоте сиденья: там бачку остаётся ровно
    /// минимум, и если формула предела разойдётся с раскладкой, на этом кадре
    /// бачок налезет на крышку или повиснет над ней. На умолчании такое
    /// расхождение не видно.</summary>
    [UnityTest]
    public IEnumerator IsoToilet_360x790x660_HighSeat()
    {
        yield return RenderToilet(ToiletElement.MaxSeatHeightMM, "IsoToiletHigh",
            "iso_toilet_360x790x660_high.png", false);
    }

    [UnityTest]
    public IEnumerator IsoWallHungToilet_360x1000x540_Default()
    {
        yield return RenderWallHungToilet(WallHungToiletElement.DefaultSeatHeightMM,
            WallHungToiletElement.DefaultFlushPlateHeightMM, "IsoWallHungToilet",
            "iso_wall_hung_toilet_360x1000x540.png");
    }

    /// <summary>Чаша на верхнем пределе. Панель смыва заказана умолчальной и
    /// обязана приехать ПОДНЯТОЙ — это единственный кадр, где одностороннюю
    /// связь двух параметров видно глазами.</summary>
    [UnityTest]
    public IEnumerator IsoWallHungToilet_360x1000x540_HighBowlPushesThePlate()
    {
        yield return RenderWallHungToilet(WallHungToiletElement.MaxSeatHeightMM,
            WallHungToiletElement.DefaultFlushPlateHeightMM, "IsoWallHungToiletHigh",
            "iso_wall_hung_toilet_360x1000x540_high.png", false);
    }

    private IEnumerator RenderToilet(int seatHeightMM, string name, string png,
        bool capturePanel = true)
    {
        var dims = ToiletElement.ModelDimensionsMM;
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateToilet(seatHeightMM, name, pos);
        _spawned.Add(go);

        var toilet = go.GetComponent<ToiletElement>();
        Assert.IsNotNull(toilet, "фабрика обязана вернуть именно ToiletElement");
        Assert.AreEqual(dims, toilet!.DimensionsMM,
            "габарит компакта фиксирован, и кадр обязан показывать именно его");
        Assert.AreEqual(seatHeightMM, toilet.SeatHeightMM,
            "и ту высоту чаши, которую заказали: подрезка на предельном значении не "
            + "имеет права его сдвинуть");
        Assert.IsNotNull(go.transform.Find(ToiletLayout.CisternName),
            "бачок — отдельный именованный ребёнок: кадр без него был бы кадром "
            + "приставного унитаза");
        Assert.IsNotNull(go.transform.Find(ToiletLayout.ButtonName),
            "хромированная кнопка живёт во ВТОРОМ наборе деталей: её пропажа означает, "
            + "что второй слот декора не строится вовсе");

        yield return RenderIsoFrame(pos, dims, png, capturePanel);
    }

    private IEnumerator RenderWallHungToilet(int seatHeightMM, int plateHeightMM, string name,
        string png, bool capturePanel = true)
    {
        var dims = WallHungToiletElement.ModelDimensionsMM;
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateWallHungToilet(seatHeightMM, plateHeightMM, name, pos);
        _spawned.Add(go);

        var toilet = go.GetComponent<WallHungToiletElement>();
        Assert.IsNotNull(toilet, "фабрика обязана вернуть именно WallHungToiletElement");
        Assert.AreEqual(dims, toilet!.DimensionsMM,
            "габарит инсталляции фиксирован и включает пустоту под чашей");
        Assert.AreEqual(seatHeightMM, toilet.SeatHeightMM,
            "и ту высоту чаши, которую заказали");
        Assert.AreEqual(WallHungToiletLayout.ClampPlateBottomMM(seatHeightMM, plateHeightMM),
            toilet.FlushPlateHeightMM,
            "панель либо на заказанной высоте, либо поднята чашей — третьего варианта "
            + "у односторонней связи нет");
        Assert.IsNotNull(go.transform.Find(WallHungToiletLayout.PlateName),
            "панель смыва — отдельный именованный ребёнок: без неё кадр показал бы "
            + "чашу, висящую в воздухе безо всякой инсталляции");

        yield return RenderIsoFrame(pos, dims, png, capturePanel);
    }

    // ─ Socket and light switch isometric screenshots ────────────────────────

    [UnityTest]
    public IEnumerator IsoSocket_80x80x10_Default()
    {
        yield return RenderSocket(WallDeviceSpec.Default, "IsoSocket",
            "iso_socket_80x80x10.png");
    }

    /// <summary>Тройной блок. Посты стоят ВПЛОТНУЮ и умножают ширину габарита —
    /// единственное поле, которое меняет размер элемента, а не только его
    /// начинку. Умолчательный кадр этого не показывает вовсе: там пост один, и
    /// раскладка, потерявшая шаг между постами, выглядела бы на нём правильно.</summary>
    [UnityTest]
    public IEnumerator IsoSocket_ThreePostsStandFlushAndTripleTheWidth()
    {
        var triple = WallDeviceSpec.Clamped(WallDeviceLayout.DefaultPlateWidthMM,
            WallDeviceLayout.DefaultPlateHeightMM, WallDeviceLayout.DefaultProtrusionMM,
            WallDeviceLayout.MaxPostCount);

        Assert.AreEqual(WallDeviceLayout.DefaultPlateWidthMM * WallDeviceLayout.MaxPostCount,
            triple.DimensionsMM.x,
            "кадр имеет смысл только если посты ДЕЙСТВИТЕЛЬНО умножили ширину: иначе он "
            + "показывает одиночную розетку под другим именем");

        yield return RenderSocket(triple, "IsoSocketTriple", "iso_socket_triple.png", false);
    }

    [UnityTest]
    public IEnumerator IsoLightSwitch_80x80x10_Default()
    {
        yield return RenderLightSwitch(WallDeviceSpec.Default, "IsoLightSwitch",
            "iso_light_switch_80x80x10.png");
    }

    /// <summary>Стена под прибор. Розетка и выключатель — НАСТЕННЫЕ приборы:
    /// без стены рядом <c>Start → SnapToWall</c> не находит, к чему сесть, и
    /// молча оставляет прибор там, где его создали.
    ///
    /// Именно это и было на трёх кадрах. Прибор стоял в начале координат, то
    /// есть на 40 мм ВНУТРИ плиты пола, и валидатор красил его целиком: COL-01
    /// («детали пересекаются в объёме») с BasePlate и COL-02 («деталь не имеет
    /// опоры»), потому что опереться было не на что. На PNG это читалось как
    /// декор, а не как ошибка: доля розового по кадру 2–3 %, но не оттого, что
    /// тинта мало, а оттого, что сам прибор в кадре занимал 2–3 % — камера
    /// упиралась в пол дистанции MinCameraDistance (см. CreateCloseUpCamera).
    ///
    /// Стена стоит ЗА прибором, лицевой плоскостью на z = 0. Посадка
    /// разворачивает прибор по наружной нормали стены, то есть лицом в −Z —
    /// туда, где стоит камера. До этого все три кадра снимались со спины.</summary>
    private void SpawnWallBehindDevices(string name)
    {
        SpawnWallAt(name + "_Wall", new Vector3Int(1200, 2500, WallBehindThicknessMM),
            new Vector3(0f, 1.25f, WallBehindThicknessMM * 0.5f * AppConstants.MM_TO_UNITS));
    }

    private const int WallBehindThicknessMM = 100;

    /// <summary>Во сколько раз кадр шире прибора. 80-миллиметровая розетка в
    /// кадре 512×512 обязана быть предметом, а не точкой.</summary>
    private const float WallDeviceFrameSpan = 2f;

    private IEnumerator RenderSocket(WallDeviceSpec spec, string name, string png,
        bool capturePanel = true)
    {
        SpawnWallBehindDevices(name);

        var go = ElementFactory.CreateSocket(spec, name,
            MountPosition(WallDeviceLayout.SocketCentreAboveFloorMM));
        _spawned.Add(go);

        var socket = go.GetComponent<SocketElement>();
        Assert.IsNotNull(socket, "фабрика обязана вернуть именно SocketElement");
        Assert.AreEqual(spec.PostCount, socket!.PostCount,
            "заказанное число постов обязано дойти неподрезанным, иначе кадр блока молча "
            + "вырождается в кадр одиночной розетки");
        Assert.AreEqual(spec.DimensionsMM, socket.DimensionsMM,
            "габарит ВЫЧИСЛЯЕТСЯ из формы: разойдись он с раскладкой, камера кадрировала "
            + "бы не то, что построено");

        yield return RenderWallDevice(go, socket, png, capturePanel,
            WallDeviceLayout.SocketCentreAboveFloorMM);
    }

    private IEnumerator RenderLightSwitch(WallDeviceSpec spec, string name, string png,
        bool capturePanel = true)
    {
        SpawnWallBehindDevices(name);

        var go = ElementFactory.CreateLightSwitch(spec, true, null, name,
            MountPosition(WallDeviceLayout.SwitchCentreAboveFloorMM));
        _spawned.Add(go);

        var source = go.GetComponent<LightSwitchElement>();
        Assert.IsNotNull(source, "фабрика обязана вернуть именно LightSwitchElement");
        Assert.AreEqual(spec.DimensionsMM, source!.DimensionsMM,
            "габарит ВЫЧИСЛЯЕТСЯ из формы, как и у розетки");

        yield return RenderWallDevice(go, source, png, capturePanel,
            WallDeviceLayout.SwitchCentreAboveFloorMM);
    }

    /// <summary>Прибор создаётся ПЕРЕД стеной (z &lt; 0) и на монтажной высоте:
    /// сторона решает, какую из двух граней стены посадка сочтёт наружной, а
    /// высота — та, что объявлена в раскладке, а не «где получилось».</summary>
    private static Vector3 MountPosition(int centreAboveFloorMM) =>
        new Vector3(0f, centreAboveFloorMM * AppConstants.MM_TO_UNITS,
            -0.01f);

    private IEnumerator RenderWallDevice(GameObject go, KitchenElement device, string png,
        bool capturePanel, int centreAboveFloorMM)
    {
        yield return null;

        float toU = AppConstants.MM_TO_UNITS;
        Assert.AreEqual(0f, (device.transform.position.z + device.DimensionsMM.z * 0.5f * toU) / toU,
            0.01f,
            "прибор обязан сесть задней гранью на плоскость стены z=0. Не севший прибор "
            + "висит там, где его создали, и кадр показывает тинт нарушения вместо "
            + "изделия — с этого и начался разбор");
        Assert.AreEqual(centreAboveFloorMM, device.transform.position.y / toU, 0.5f,
            "и остаться на монтажной высоте: посадка двигает прибор только поперёк стены");

        var bounds = RendererBoundsOf(go);
        var (camGo, cam) = CreateCloseUpCamera(bounds.center,
            Mathf.Max(device.DimensionsMM.x, device.DimensionsMM.y) * WallDeviceFrameSpan);
        _spawned.Add(camGo);
        AssertFitsInFrame(cam, bounds, go.name);

        yield return capturePanel
            ? RenderToPng(cam, png)
            : RenderToPng(cam, png, null);

        Object.DestroyImmediate(camGo);
    }

    private IEnumerator RenderIsoFrame(Vector3 pos, Vector3Int dims, string png,
        bool capturePanel)
    {
        Vector3 size = MmToUnits(dims);
        var (camGo, cam) = CreateIsoCamera(pos, size, 2.5f);
        _spawned.Add(camGo);

        yield return capturePanel
            ? RenderToPng(cam, png)
            : RenderToPng(cam, png, null);

        Object.DestroyImmediate(camGo);
    }

    // ─ Bathtub isometric screenshots ───────────────────

    [UnityTest]
    public IEnumerator IsoBathtub_1700x600x700_Default()
    {
        yield return RenderBathtub(BathtubElement.DefaultBowlRadiusMM, "IsoBathtub",
            "iso_bathtub_1700x600x700.png");
    }

    /// <summary>Радиус чаши НОЛЬ — строго прямоугольная ванна. Снаружи корпус
    /// обязан остаться скруглённым: его радиус выводится как радиус чаши плюс
    /// борт, поэтому на нуле он равен ширине борта, а не нулю. Острой кромки
    /// борта в акриле не бывает, и это единственный кадр, где видно, что
    /// производная не схлопнулась вместе со своим слагаемым.</summary>
    [UnityTest]
    public IEnumerator IsoBathtub_1700x600x700_SquareBowlKeepsARoundedShell()
    {
        yield return RenderBathtub(0, "IsoBathtubSquare",
            "iso_bathtub_1700x600x700_square.png", false);
    }

    /// <summary>Радиус чаши на ПРЕДЕЛЕ — торцы обязаны стать полукруглыми, а
    /// борт остаться равномерным по всему периметру. Это верхняя граница, за
    /// которой RoundedRectProfile начал бы ужимать радиус сам, и борт в углу
    /// поехал бы; на умолчании такое расхождение не видно.</summary>
    [UnityTest]
    public IEnumerator IsoBathtub_1700x600x700_WidestBowlGivesSemicircularEnds()
    {
        var dims = BathtubLayout.DefaultDimensionsMM;
        int widest = BathtubElement.MaxBowlRadiusMM(dims, BathtubElement.DefaultRimWidthMM);
        yield return RenderBathtub(widest, "IsoBathtubRound",
            "iso_bathtub_1700x600x700_round.png", false);
    }

    private IEnumerator RenderBathtub(int bowlRadiusMM, string name, string png,
        bool capturePanel = true)
    {
        var dims = BathtubLayout.DefaultDimensionsMM;
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateBathtub(dims, BathtubElement.DefaultRimWidthMM,
            BathtubElement.DefaultBowlDepthMM, bowlRadiusMM,
            BathtubElement.DefaultBowlFilletMM, name, pos);
        _spawned.Add(go);

        var tub = go.GetComponent<BathtubElement>();
        Assert.IsNotNull(tub, "фабрика обязана вернуть именно BathtubElement");
        Assert.AreEqual(dims, tub!.DimensionsMM,
            "габарит ванны задаёт пользователь, и кадр обязан показывать заказанный");
        Assert.AreEqual(bowlRadiusMM, tub.BowlRadiusMM,
            "и тот радиус чаши, который заказали: подрезка на предельном значении не "
            + "имеет права его сдвинуть, иначе кадр «полукруглые торцы» молча "
            + "выродится в кадр умолчания");
        Assert.AreEqual(bowlRadiusMM + tub.RimWidthMM,
            BathtubLayout.ShellCornerRadiusMM(dims, tub.RimWidthMM, tub.BowlRadiusMM),
            "наружный радиус — производная: радиус чаши плюс борт. Разойдись она с "
            + "кадром, борт в углу стал бы шире или уже, чем на прямом участке");

        yield return RenderIsoFrame(pos, dims, png, capturePanel);
    }

    // ─ Wall mixer and shower column isometric screenshots ───────────────────

    /// <summary>Имена этих кадров НЕ несут габарит, в отличие от доски или
    /// фасада. У смесителя и стойки габарит ВЫЧИСЛЯЕТСЯ из формы, и первая же
    /// правка носика или наклона лейки переименовала бы и PNG, и лежащий
    /// рядом эталон панели. Эталон при этом осиротел бы, а тест упал бы на
    /// «нет verified-файла» — хотя панель не менялась ни на пиксель.
    ///
    /// Крупные планы здесь не украшение. Стойка высотой полтора метра в кадре
    /// 512×512 даёт около четверти пикселя на миллиметр: штанга Ø 32 мм —
    /// восемь пикселей, хомут кронштейна — два. На общем виде НЕВОЗМОЖНО
    /// увидеть, разделились детали или слиплись, и ровно поэтому первая
    /// версия обеих моделей прошла все проверки, будучи комком.
    ///
    /// И главный урок первого круга. Смеситель и стойка снимались СО СТОРОНЫ
    /// СТЕНЫ. Камера проекта стоит на -Z, настенная арматура садится задней
    /// гранью на z=0 и растёт в +Z — значит в кадр она попадает затылком, и
    /// ближе всего к объективу оказываются отражатели, заслоняя собой корпус.
    /// Излива, рычага расхода и лицевой стороны лейки на таком снимке нет
    /// вовсе. Разбирая кадры, это легко списать на геометрию: «не видно
    /// излива» выглядит как «излив плохо сделан», и целый круг правок ушёл бы
    /// в стол. Лечится тем же разворотом на 180°, что уже придуман для
    /// дивана, и связку держит IsoCamera_FacesTheWallSideOfAFitting.</summary>
    private const float CloseUpDistanceScale = 1.21f;

    /// <summary>Держит связку, из-за которой разворот понадобился второй раз,
    /// теперь для настенной арматуры: камера стоит со стороны -Z, а лицо
    /// смесителя и стойки смотрит в +Z, потому что задней гранью они садятся
    /// на стену.
    ///
    /// Тест сторожит ОБА конца, а не один. Разверни IsoDir или перенеси
    /// плоскость посадки — и разворот станет вредным, а красное покажет на
    /// константу, которую надо убрать.</summary>
    [Test]
    public void IsoCamera_FacesTheWallSideOfAFitting_SoWallFittingsMustTurnAround()
    {
        var spec = BathMixerSpec.Default;

        Assert.Less(IsoDir.z, 0f, "камера смотрит со стороны -Z");
        Assert.AreEqual(0f, BathMixerLayout.BoundsMM(spec).min.z, 1e-3f,
            "а настенная арматура садится на стену задней гранью габарита, то есть "
            + "плоскостью z=0 — той самой, к которой обращён объектив");
        Assert.Greater(BathMixerOutlets.SpoutMouthMM(spec).z,
            BathMixerLayout.BodyAxisZMM(spec),
            "лицо же её смотрит в противоположную сторону: излив уходит в +Z");
        Assert.Greater(ShowerColumnSpec.Default.ArmReachMM,
            ShowerColumnSpec.Default.WallOffsetMM,
            "и гусак стойки — туда же");

        Assert.AreEqual(180f, FrontTowardsCameraDeg,
            "раз обе стороны связки сошлись, арматура обязана развернуться к камере "
            + "лицом: иначе снимок показывает затылки отражателей, а излива на нём нет");
    }

    [UnityTest]
    public IEnumerator IsoBathMixer_Default()
    {
        yield return RenderBathMixer(BathMixerSpec.Default, "IsoBathMixer",
            "iso_bath_mixer_default.png");
    }

    /// <summary>Межосевое на ПРЕДЕЛЕ — отражатели уезжают к самым торцам
    /// корпуса, а головки схлопываются до полусфер: их длина это
    /// (длина − межосевое)/2, и на потолке межосевого она равна радиусу
    /// корпуса. Это верхняя граница подрезки, и держит её один кадр: на
    /// умолчании головки длинные, и съехавшая на диаметр граница выглядела бы
    /// точно так же.</summary>
    [UnityTest]
    public IEnumerator IsoBathMixer_WidestCentresKeepTheEscutcheonsOnTheBody()
    {
        yield return RenderBathMixer(WidestMixer(), "IsoBathMixerWide",
            "iso_bath_mixer_widest_centres.png", false);
    }

    /// <summary>Крупный план правого торца: термоголовка, выступающее кольцо
    /// шкалы на ней и кнопка-ограничитель сверху. Три детали, которых на
    /// общем виде не разглядеть, и ровно те три, по которым термостатический
    /// смеситель отличается от обрубка трубы.
    ///
    /// Наводка — СЕРЕДИНА головки, а не начало кольца шкалы: кадр, названный
    /// по узлу, обязан держать узел в центре, а не у края.</summary>
    [UnityTest]
    public IEnumerator IsoBathMixer_ThermostatHeadCloseUp()
    {
        var spec = BathMixerSpec.Default;

        yield return RenderBathMixerCloseUp(spec, "IsoBathMixerThermostat",
            new Vector3(BathMixerControls.HandleMidXMM(spec, 1f), 0f,
                BathMixerLayout.BodyAxisZMM(spec)),
            ThermostatSpanMM(spec), "iso_bath_mixer_thermostat.png");
    }

    /// <summary>Крупный план низа: излив с изломом у носика, штуцер G 1/2 под
    /// шланг и кнопка дивертора ровно над ним. Здесь же видно талию корпуса
    /// между эксцентриками — без неё все эти детали тонули в одном
    /// цилиндре.</summary>
    [UnityTest]
    public IEnumerator IsoBathMixer_DiverterAndSpoutCloseUp()
    {
        var spec = BathMixerSpec.Default;
        var elbow = BathMixerOutlets.SpoutElbowMM(spec);
        var tip = BathMixerOutlets.SpoutTipMM(spec);

        yield return RenderBathMixerCloseUp(spec, "IsoBathMixerDiverter",
            new Vector3(BathMixerOutlets.StationXMM(spec) * 0.35f,
                (elbow.y + tip.y) * 0.5f, (elbow.z + tip.z) * 0.5f),
            SpoutSpanMM(spec), "iso_bath_mixer_diverter.png");
    }

    [UnityTest]
    public IEnumerator IsoShowerColumn_Default()
    {
        yield return RenderShowerColumn(ShowerColumnSpec.Default, "IsoShowerColumn",
            "iso_shower_column_default.png");
    }

    /// <summary>Шланг КОРОЧЕ расстояния между штуцером и рукояткой — петли не
    /// существует, и он обязан быть натянут по прямой, а не растянут до
    /// заказанной длины. Умолчательный кадр этого не показывает: там петля есть
    /// всегда, и подставная кривая, игнорирующая длину, выглядела бы на нём
    /// правильно.</summary>
    [UnityTest]
    public IEnumerator IsoShowerColumn_AHoseShorterThanTheGapRunsStraight()
    {
        var taut = TautHoseColumn();

        Assert.Less(taut.HoseLengthMM,
            (ShowerColumnLayout.HoseInletMM(taut) - ShowerColumnLayout.HoseOutletMM(taut))
                .magnitude,
            "кадр имеет смысл только если шланга ДЕЙСТВИТЕЛЬНО не хватает на прямую: "
            + "иначе он показывает обычную петлю под другим именем");

        yield return RenderShowerColumn(taut, "IsoShowerColumnTaut",
            "iso_shower_column_taut_hose.png", false);
    }

    /// <summary>Крупный план узла держателя: хомут на штанге, плечо вперёд,
    /// чашка вокруг рукоятки и наклонённая в ней ручная лейка. Именно этот
    /// узел на общем виде выглядел блямбой — четыре детали в двадцати
    /// пикселях.</summary>
    [UnityTest]
    public IEnumerator IsoShowerColumn_HandShowerInItsHolderCloseUp()
    {
        var spec = ShowerColumnSpec.Default;

        yield return RenderShowerColumnCloseUp(spec, "IsoShowerColumnHand",
            ShowerColumnLayout.HolderCentreMM(spec),
            ShowerColumnHandShower.GripLengthMM(spec) * 1.9f,
            "iso_shower_column_hand_shower.png");
    }

    /// <summary>Крупный план верха: гиб гусака и тропическая лейка под ним.
    /// Стойка стояла в габаритной коробке ПО ДИАГОНАЛИ, потому что дуга гиба
    /// рождалась в стороне от прямого участка, и протяжка тянула к ней
    /// наклонную перемычку. На общем виде полутораметровой стойки этот
    /// наклон читался как «модель просто кривая»; здесь он был бы виден
    /// сразу.</summary>
    [UnityTest]
    public IEnumerator IsoShowerColumn_GooseneckAndRainHeadCloseUp()
    {
        var spec = ShowerColumnSpec.Default;

        yield return RenderShowerColumnCloseUp(spec, "IsoShowerColumnGooseneck",
            new Vector3(0f, ShowerColumnLayout.ArmAxisYMM(spec) - spec.HeadDiameterMM * 0.4f,
                spec.ArmReachMM * 0.6f),
            spec.ArmReachMM * 1.6f, "iso_shower_column_gooseneck.png");
    }

    /// <summary>Крупный план низа: блок дивертора с рычагом и верх петли
    /// шланга, уходящей из него вниз. На общем виде стойки шланг Ø 15 мм —
    /// это четыре пикселя, и проверить по нему нечего; а дивертор без этого
    /// кадра остаётся комом, про который неизвестно даже, орган это
    /// управления или кусок трубы.</summary>
    [UnityTest]
    public IEnumerator IsoShowerColumn_DiverterAndHoseLoopCloseUp()
    {
        var spec = ShowerColumnSpec.Default;
        float loopBottom = PipePath.LowestPoint(ShowerColumnLayout.HosePath(spec)).y;

        yield return RenderShowerColumnCloseUp(spec, "IsoShowerColumnDiverter",
            new Vector3(0f, loopBottom * 0.5f, ShowerColumnLayout.DiverterDepthMM(spec) * 0.7f),
            (ShowerColumnLayout.DiverterHeightMM(spec) - loopBottom) * 1.5f,
            "iso_shower_column_diverter.png");
    }

    private const float ThermostatSpanRatio = 0.5f;
    private const float SpoutSpanRatio = 0.8f;

    private static float ThermostatSpanMM(BathMixerSpec spec) =>
        spec.BodyLengthMM * ThermostatSpanRatio;

    private static float SpoutSpanMM(BathMixerSpec spec) =>
        spec.BodyLengthMM * SpoutSpanRatio;

    /// <summary>Сторож против возврата крупных планов смесителя на общую
    /// камеру. У той стоит пол дистанции MinCameraDistance, и оба этих кадра
    /// в него упираются — то есть через неё они физически не могут быть
    /// крупными. Так и вышло в первый раз: заказанные 148 мм превратились в
    /// ~414 мм, головка уехала к краю кадра, и выглядело это как ошибка
    /// геометрии, а не камеры.
    ///
    /// Тест краснеет с ОБЕИХ сторон. Раздуй кадр до размеров, которые общая
    /// камера уже умеет — и он скажет, что крупный план перестал быть
    /// крупным.</summary>
    [Test]
    public void CloseUpFramesOfTheMixer_AreTighterThanTheSharedCameraCanEverBe()
    {
        var spec = BathMixerSpec.Default;
        float toU = AppConstants.MM_TO_UNITS * CloseUpDistanceScale;

        Assert.Less(ThermostatSpanMM(spec) * toU, MinCameraDistance,
            "кадр термоголовки обязан быть теснее пола общей камеры: иначе снимать его "
            + "отдельным кадром незачем — то же самое видно на общем виде");
        Assert.Less(SpoutSpanMM(spec) * toU, MinCameraDistance,
            "и кадр излива тоже");
        Assert.Greater(ThermostatSpanMM(spec),
            BathMixerControls.ScaleCollar(spec).FromRadiusMM
                + BathMixerControls.LimitButton(spec).ToMM.y,
            "но не теснее самого узла: кольцо шкалы вместе с кнопкой сверху обязано "
            + "влезть в кадр целиком, иначе крупный план режет ровно то, ради чего снят");
    }

    private static BathMixerSpec WidestMixer() =>
        BathMixerSpec.Clamped(
            BathMixerSpec.MaxCentresForMM(BathMixerSpec.DefaultBodyLengthMM,
                BathMixerSpec.DefaultBodyDiameterMM),
            BathMixerSpec.DefaultBodyLengthMM, BathMixerSpec.DefaultBodyDiameterMM,
            BathMixerSpec.DefaultEscutcheonReachMM, BathMixerSpec.DefaultSpoutLengthMM,
            BathMixerSpec.DefaultOutletDiameterMM);

    private static ShowerColumnSpec TautHoseColumn() =>
        ShowerColumnSpec.Clamped(ShowerColumnSpec.DefaultColumnHeightMM,
            ShowerColumnSpec.DefaultRiserDiameterMM, ShowerColumnSpec.DefaultHeadDiameterMM,
            ShowerColumnSpec.DefaultHeadThicknessMM, ShowerColumnSpec.DefaultArmReachMM,
            ShowerColumnSpec.DefaultWallOffsetMM,
            ShowerColumnSpec.DefaultHandShowerDiameterMM, ShowerColumnSpec.MinHoseLengthMM);

    /// <summary>Точка раскладки (миллиметры, начало — плоскость стены и низ
    /// узла крепления) в мировые координаты. Меш строится от ЦЕНТРА
    /// габарита, и элемент стоит центром в pos — поэтому смещение считается
    /// от центра габарита, а не от нуля раскладки.</summary>
    private static Vector3 WorldFromLayoutMM(Vector3 posUnits, Bounds boundsMM,
        Vector3 pointMM) =>
        posUnits + TurnedToCamera * ((pointMM - boundsMM.center) * AppConstants.MM_TO_UNITS);

    private static Quaternion TurnedToCamera =>
        Quaternion.Euler(0f, FrontTowardsCameraDeg, 0f);

    /// <summary>Крупный план, наведённый мимо модели, сохраняется на диск как
    /// обычный кадр — просто пустой — и никого не настораживает. Поэтому
    /// точка наводки проверяется ДО рендера: она обязана лежать внутри
    /// габарита элемента.</summary>
    private static void AssertDetailIsOnTheModel(Bounds boundsMM, Vector3 detailMM, string png)
    {
        Assert.IsTrue(boundsMM.Contains(detailMM),
            $"точка наводки крупного плана {png} — {detailMM} мм — вне габарита элемента "
            + $"{boundsMM.min}..{boundsMM.max} мм: камера смотрела бы в пустоту");
    }

    /// <summary>Крупный план НЕ может пользоваться CreateIsoCamera: там стоит
    /// пол дистанции MinCameraDistance, и любой кадр уже ~414 мм в него
    /// упирается. Первый заказанный крупный план узла термоголовки — 148 мм —
    /// молча превратился в кадр шириной с полторы длины смесителя, где сама
    /// головка уехала к краю, а середину занял излив. Ошибка при этом
    /// выглядит не как ошибка камеры, а как «деталь не там построена».</summary>
    private (GameObject camGo, Camera cam) CreateCloseUpCamera(Vector3 centre, float spanMM)
    {
        var camGo = new GameObject("IsoCloseUpCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = IsoFov;
        cam.aspect = (float)RenderW / RenderH;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        float distance = spanMM * AppConstants.MM_TO_UNITS * CloseUpDistanceScale;
        camGo.transform.position = centre + IsoDir * distance;
        camGo.transform.LookAt(centre);

        return (camGo, cam);
    }

    private IEnumerator RenderCloseUp(Vector3 centreUnits, float spanMM, string png)
    {
        var (camGo, cam) = CreateCloseUpCamera(centreUnits, spanMM);
        _spawned.Add(camGo);

        yield return RenderToPng(cam, png, null);

        Object.DestroyImmediate(camGo);
    }

    /// <summary>Стена, на которой висит арматура. Настенный прибор без стены —
    /// это НЕ «прибор в пустой сцене», а прибор, висящий в воздухе, и ядро
    /// говорит об этом прямо: ConstraintValidator.CheckConnectivity не находит
    /// у него ни одного контакта гранью с якорем и выдаёт COL-02 «Деталь не
    /// имеет опоры». ElementHighlighter красит нарушителя _invalidMaterial —
    /// розовым. Все кадры смесителя и стойки до этой правки сняты в тинте
    /// ошибки: на них не было ни хрома, ни собственного материала, один
    /// сигнал «красное».
    ///
    /// Стена лечит это по-настоящему, а не глушит проверку. Она разом
    /// закрывает три вещи: прибор получает опору (правило перестаёт
    /// срабатывать, потому что расстановка стала верной), Start → SnapToWall
    /// сажает его вплотную к плоскости стены, и он же разворачивает его лицом
    /// от стены — то есть к объективу, стоящему на -Z.</summary>
    private const int FittingWallWidthMM = 3000;
    private const int FittingWallHeightMM = 2500;
    private const int FittingWallThicknessMM = 100;

    /// <summary>Стена ставится так, чтобы посадка НЕ двигала прибор: её
    /// передняя плоскость приходится ровно на заднюю грань габарита в той
    /// позиции, куда прибор уже поставлен. Иначе Start сдвинул бы его ПОСЛЕ
    /// того, как камера наведена (камера создаётся до первого кадра), и кадр
    /// уехал бы мимо — сдвиг на полглубины выглядел бы как ошибка модели.
    ///
    /// Полное отображение стены включается здесь же. Это не косметика:
    /// опущенная стена ужимается по Y вместе с transform, а валидация читает
    /// ТОТ ЖЕ transform — под опущенной стеной прибор снова остаётся без
    /// опоры и снова краснеет.</summary>
    private void SpawnWallBehindFitting(string name, Vector3Int fittingDims, Vector3 fittingPos)
    {
        KitchenSettings.Instance.NormalView.wallsEnabled = true;
        KitchenSettings.Instance.NormalView.lowerNearWalls = false;
        KitchenSettings.Instance.NormalView.lowerAllWalls = false;

        float standoff = AppConstants.HalfHeightUnits(fittingDims.z)
            + AppConstants.HalfHeightUnits(FittingWallThicknessMM);

        var wall = SpawnWallAt(name,
            new Vector3Int(FittingWallWidthMM, FittingWallHeightMM, FittingWallThicknessMM),
            new Vector3(fittingPos.x,
                AppConstants.HalfHeightUnits(FittingWallHeightMM),
                fittingPos.z + standoff));

        AssertWallCoversTheBackOfTheFitting(wall, fittingDims, fittingPos, name);
    }

    /// <summary>Контакт гранью засчитывается только при перекрытии не меньше
    /// Tolerance.MinSupportOverlap. Стена шире и выше прибора, поэтому доля
    /// равна единице — но ровно до тех пор, пока задняя грань прибора целиком
    /// лежит внутри стены. Вылези она за верх (стойка тянется к 2,25 м) — и
    /// опора пропадёт, а кадр снова выйдет розовым, ничего об этом не
    /// сказав.</summary>
    private static void AssertWallCoversTheBackOfTheFitting(KitchenElement wall,
        Vector3Int fittingDims, Vector3 fittingPos, string name)
    {
        float halfW = AppConstants.HalfHeightUnits(fittingDims.x);
        float halfH = AppConstants.HalfHeightUnits(fittingDims.y);
        var wallPos = wall.transform.position;

        Assert.LessOrEqual(Mathf.Abs(fittingPos.x - wallPos.x) + halfW,
            AppConstants.HalfHeightUnits(FittingWallWidthMM),
            name + ": прибор шире стены — задняя грань выходит за её край, доля "
            + "перекрытия падает ниже Tolerance.MinSupportOverlap и опоры не будет");
        Assert.LessOrEqual(Mathf.Abs(fittingPos.y - wallPos.y) + halfH,
            AppConstants.HalfHeightUnits(FittingWallHeightMM),
            name + ": прибор выше стены — тот же обрыв опоры, только по вертикали");
    }

    /// <summary>Спрашивает ровно об одном: прибор остался там, куда его
    /// поставили. Вторая половина этого хелпера — «и он не в списке
    /// нарушителей» — переехала в общую съёмку
    /// (<c>ElementFrameTests.CaptureFramePng</c>): валидационный тон в кадре
    /// теперь выключен, поэтому вопрос валидатору задаётся ПЕРЕД каждым кадром
    /// без исключения, а не в тех тестах, где о нём вспомнили.</summary>
    private static void AssertSeatedOnTheWall(KitchenElement fitting,
        Vector3 spawnedAt, string png)
    {
        Assert.AreEqual(0f, (spawnedAt - fitting.transform.position).magnitude, 1e-4f,
            png + ": посадка на стену сдвинула прибор уже после того, как камера "
            + "наведена — стена стоит не на том расстоянии");
    }

    private IEnumerator RenderBathMixer(BathMixerSpec spec, string name, string png,
        bool capturePanel = true)
    {
        var dims = BathMixerLayout.DimensionsMM(spec);
        Vector3 pos = MixerPosition(spec);
        SpawnWallBehindFitting(name + "Wall", dims, pos);
        var mixer = SpawnBathMixer(spec, name, pos);

        yield return RenderIsoFrame(pos, dims, png, capturePanel);

        AssertSeatedOnTheWall(mixer, pos, png);
    }

    private IEnumerator RenderBathMixerCloseUp(BathMixerSpec spec, string name,
        Vector3 detailMM, float spanMM, string png)
    {
        var dims = BathMixerLayout.DimensionsMM(spec);
        Vector3 pos = MixerPosition(spec);
        SpawnWallBehindFitting(name + "Wall", dims, pos);
        var mixer = SpawnBathMixer(spec, name, pos);

        var bounds = BathMixerLayout.BoundsMM(spec);
        AssertDetailIsOnTheModel(bounds, detailMM, png);

        yield return RenderCloseUp(WorldFromLayoutMM(pos, bounds, detailMM), spanMM, png);

        AssertSeatedOnTheWall(mixer, pos, png);
    }

    private static Vector3 MixerPosition(BathMixerSpec spec) =>
        new Vector3(0f, BathMixerLayout.CentreAboveFloorMM(spec) * AppConstants.MM_TO_UNITS, 0f);

    private KitchenElement SpawnBathMixer(BathMixerSpec spec, string name, Vector3 pos)
    {
        var go = ElementFactory.CreateBathMixer(spec, name, pos);
        _spawned.Add(go);
        go.transform.rotation = TurnedToCamera;

        var mixer = go.GetComponent<BathMixerElement>();
        Assert.IsNotNull(mixer, "фабрика обязана вернуть именно BathMixerElement");
        Assert.AreEqual(spec.CentresMM, mixer!.CentresMM,
            "заказанное межосевое обязано дойти неподрезанным, иначе кадр предельного "
            + "значения молча вырождается в кадр умолчания");
        Assert.AreEqual(BathMixerLayout.DimensionsMM(spec), mixer.DimensionsMM,
            "габарит смесителя ВЫЧИСЛЯЕТСЯ из формы: разойдись он с раскладкой, камера "
            + "кадрировала бы не то, что построено");
        return mixer;
    }

    private IEnumerator RenderShowerColumn(ShowerColumnSpec spec, string name, string png,
        bool capturePanel = true)
    {
        var dims = ShowerColumnLayout.DimensionsMM(spec);
        Vector3 pos = ColumnPosition(spec);
        SpawnWallBehindFitting(name + "Wall", dims, pos);
        var column = SpawnShowerColumn(spec, name, pos);

        yield return RenderIsoFrame(pos, dims, png, capturePanel);

        AssertSeatedOnTheWall(column, pos, png);
    }

    private IEnumerator RenderShowerColumnCloseUp(ShowerColumnSpec spec, string name,
        Vector3 detailMM, float spanMM, string png)
    {
        var dims = ShowerColumnLayout.DimensionsMM(spec);
        Vector3 pos = ColumnPosition(spec);
        SpawnWallBehindFitting(name + "Wall", dims, pos);
        var column = SpawnShowerColumn(spec, name, pos);

        var bounds = ShowerColumnLayout.BoundsMM(spec);
        AssertDetailIsOnTheModel(bounds, detailMM, png);

        yield return RenderCloseUp(WorldFromLayoutMM(pos, bounds, detailMM), spanMM, png);

        AssertSeatedOnTheWall(column, pos, png);
    }

    private static Vector3 ColumnPosition(ShowerColumnSpec spec) =>
        new Vector3(0f,
            ShowerColumnLayout.CentreAboveFloorMM(spec) * AppConstants.MM_TO_UNITS, 0f);

    private KitchenElement SpawnShowerColumn(ShowerColumnSpec spec, string name, Vector3 pos)
    {
        var go = ElementFactory.CreateShowerColumn(spec, name, pos);
        _spawned.Add(go);
        go.transform.rotation = TurnedToCamera;

        var column = go.GetComponent<ShowerColumnElement>();
        Assert.IsNotNull(column, "фабрика обязана вернуть именно ShowerColumnElement");
        Assert.AreEqual(spec.HoseLengthMM, column!.HoseLengthMM,
            "заказанная длина шланга обязана дойти неподрезанной: подрезка превратила бы "
            + "кадр натянутого шланга в кадр умолчания");
        Assert.AreEqual(ShowerColumnLayout.DimensionsMM(spec), column.DimensionsMM,
            "габарит стойки ВЫЧИСЛЯЕТСЯ из формы вместе с петлёй шланга: разойдись он с "
            + "раскладкой, камера кадрировала бы не то, что построено");
        return column;
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
        KitchenSettings.Instance.NormalView.lowerAllWalls = false;

        yield return RenderRoom("iso_room_raised.png");
    }

    [UnityTest]
    public IEnumerator IsoRoom_Lowered()
    {
        KitchenSettings.Instance.NormalView.wallsEnabled = true;
        KitchenSettings.Instance.NormalView.lowerNearWalls = true;
        // Кадр называется «опущены БЛИЖНИЕ»: без этой строки он зависел бы от того,
        // стоит ли «опускать все стены» в демо-проекте, который поднял Bootstrap.
        KitchenSettings.Instance.NormalView.lowerAllWalls = false;

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

        // Кадр долго был единственным в наборе, снятым С нарушением: четыре стены
        // по периметру перекрываются на углах на полтолщины (50×2500×50 мм), и
        // ValidationCore выписывал на каждый угол COL-01 — два якоря, ни один из
        // которых не пол и не проём. Глазами этого не было видно никогда (стена
        // красится своим декором раньше, чем доходит до тона), и единственным
        // следом был красный бейдж в ui_iso_room_*.verified.json. Оговорка тут
        // и стояла — но нарушения не было: угол это ус, а не дефект, и теперь
        // ядро прощает пару стен, чьи осевые сходятся в общей точке
        // (WallCentreline.MeetAtSharedCorner). Оговорка снята, и кадр обязан
        // сниматься чистым, как все остальные.
        yield return RenderToPng(cam, fileName, Path.GetFileNameWithoutExtension(fileName) + ".json");

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
        var panel = go.GetComponent<PanelElement>();
        Assert.IsNotNull(panel,
            "ХДФ-задник обязан быть панелью, а не обычной доской");
        StandOnFloor(panel!);

        yield return RenderElementIso(go, "iso_panel_500x716.png", 2.5f);
    }

    [UnityTest]
    public IEnumerator IsoScrewLeg_M6x50()
    {
        int bodyH = ScrewLegSpec.BodyHeightMM(ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM,
            ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM);
        Vector3 pos = new Vector3(0f, bodyH * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateScrewLeg("IsoScrewLeg", pos);
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        Assert.IsNotNull(leg, "винтовая опора обязана быть опорой, а не доской");
        Assert.AreEqual(ScrewLegSpec.DEFAULT_THREAD, leg!.Thread,
            "снимок обязан показывать резьбу по умолчанию — M6");

        yield return RenderElementIso(go, "iso_screw_leg_m6x50.png", 2.5f);
    }

    /// <summary>Труба ДУ 20 длиной 600 мм — ровно то, что даёт сайдбар. Имя кадра
    /// само и есть планка качества: в нём стоят и условный проход, и длина, поэтому
    /// кадр, на котором труба другого диаметра или другой длины, читается как
    /// расхождение сразу, без сверки с кодом.</summary>
    [UnityTest]
    public IEnumerator IsoPipe_Dn20_600()
    {
        const int lengthMM = 600;
        Vector3 pos = new Vector3(0f, lengthMM * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, lengthMM, "IsoPipe", pos);
        _spawned.Add(go);
        var pipe = go.GetComponent<PipeElement>();
        Assert.IsNotNull(pipe, "труба обязана быть трубой, а не доской");
        Assert.AreEqual("3/4\"", pipe!.Designation,
            "снимок обязан показывать ряд по умолчанию — ДУ 20");
        Assert.AreEqual(lengthMM, pipe.LengthMM);
        Assert.AreEqual(27, pipe.DimensionsMM.x,
            "сечение габарита берётся из наружного диаметра 26,8, а не из введённой ширины");
        Assert.AreEqual(pipe.DimensionsMM.x, pipe.DimensionsMM.z,
            "труба круглая: ширина и глубина у неё равны всегда");

        yield return RenderElementIso(go, "iso_pipe_dn20_600.png", 2.5f);
    }

    /// <summary>Во сколько раз кадр шире фитинга. Фитинг ДУ 20 — это ящик от
    /// 34 до 80 мм, а у общей камеры стоит пол дистанции MinCameraDistance,
    /// то есть её кадр не бывает уже ~414 мм: тройник занял бы в нём пятую
    /// часть высоты, а муфта — двенадцатую часть ширины. Снимок, по которому
    /// тройник не узнаётся тройником, не делает того, ради чего снят, поэтому
    /// фитинги идут через CreateCloseUpCamera, как узлы смесителя.</summary>
    private const float PipeFittingFrameSpan = 2f;

    /// <summary>Высота кадра в миллиметрах: CreateCloseUpCamera строит
    /// дистанцию так, что spanMM и есть высота кадра. Берётся наибольшее из
    /// ТРЁХ измерений — у отвода и тройника ящик шире, чем выше, и рамка по
    /// одной высоте резала бы боковой раструб.</summary>
    private static float FittingFrameSpanMM(PipeNodeKind kind)
    {
        string size = PipeSpec.DEFAULT_SIZE;
        float widest = Mathf.Max(PipeFittingSpec.WidthMm(kind, size),
            Mathf.Max(PipeFittingSpec.HeightMm(kind, size),
                PipeFittingSpec.DepthMm(kind, size)));
        return widest * PipeFittingFrameSpan;
    }

    /// <summary>Шесть фитингов трассы. Кадр здесь — единственная проверка ФОРМЫ:
    /// арифметика ног и габаритного ящика проверена под dotnet в
    /// PipeFittingSpecTests, а вот сходится ли меш с этим ящиком, видно только
    /// на картинке — рамка строится по объединённым границам рендереров и
    /// падает, если хоть один угол ящика вылез из кадра.
    ///
    /// Число портов проверяется тут же, рядом с кадром: отвод и муфта на
    /// снимке легко перепутать (обе — две трубки), и порт, потерянный по
    /// дороге, картинкой не ловится вовсе.
    ///
    /// Все шесть создавались в начале координат — то есть НАПОЛОВИНУ ВНУТРИ
    /// опорной плиты (BasePlate занимает y от −18 до 0), и валидатор выписывал
    /// каждому COL-01 «детали пересекаются в объёме» с плитой. Ровно та же
    /// история, что уже была у розетки и выключателя, и лечится тем же:
    /// поставить на плиту по коробке ВАЛИДАЦИИ, а не по половине физической
    /// высоты. Тинт в кадровых наборах выключен, так что глазами этого не
    /// видно — красным становится только утверждение в CaptureFramePng.
    ///
    /// Эталон панели тут не снимается (третий аргумент null): шесть кадров
    /// одной и той же сцены дали бы шесть почти одинаковых ui_*-эталонов,
    /// каждый из которых пришлось бы принимать вручную после любой правки
    /// сайдбара. Свойства фитингов закреплены снапшотом pipe_fittings_all.</summary>
    private IEnumerator RenderFitting(GameObject go, PipeNodeKind kind, string file)
    {
        _spawned.Add(go);
        var fitting = go.GetComponent<PipeFittingElement>();
        Assert.IsNotNull(fitting, "фитинг обязан быть фитингом, а не доской");
        Assert.AreEqual(kind, fitting!.NodeKind, "фабрика собрала фитинг другого вида");
        Assert.AreEqual(PipeNodePorts.CountOf(kind), fitting.PortCount,
            "число портов у элемента разошлось с тем, по которому судят правила PIP-01/02");
        Assert.AreEqual(fitting.DerivedDimensionsMM, fitting.DimensionsMM,
            "габарит фитинга вычисляемый: он обязан совпадать с выведенным из ног");

        yield return null;

        StandOnFloor(fitting!);
        Assert.AreEqual(fitting!.DimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS,
            fitting!.transform.position.y, 1e-6f,
            "коробка фитинга центрирована на pivot и зазоров не имеет, поэтому подъём на "
            + "плиту обязан дать ровно половину высоты. Разъедься меш с коробкой или "
            + "заведись у фитинга зазор — и фитинг снова окажется в плите, то есть в COL-01");

        var bounds = RendererBoundsOf(go);
        var (camGo, cam) = CreateCloseUpCamera(bounds.center, FittingFrameSpanMM(kind));
        _spawned.Add(camGo);
        AssertFitsInFrame(cam, bounds, go.name);

        yield return RenderToPng(cam, file, null);

        Object.DestroyImmediate(camGo);
    }

    /// <summary>Почему фитинги нельзя снимать общей камерой. Тест краснеет с
    /// обеих сторон: раздуй кадр до размеров, которые общая камера и так
    /// умеет, — и он скажет, что крупный план перестал быть крупным; сожми
    /// его теснее самого фитинга — и он скажет, что кадр режет предмет.</summary>
    [Test]
    public void PipeFittingFrames_AreTighterThanTheSharedCameraCanEverBe()
    {
        int checkedKinds = 0;
        foreach (var kind in PipeNodePorts.Kinds)
        {
            if (!PipeFittingSpec.IsFitting(kind)) continue;
            checkedKinds++;

            float spanMM = FittingFrameSpanMM(kind);
            Assert.Less(spanMM * AppConstants.MM_TO_UNITS * CloseUpDistanceScale,
                MinCameraDistance,
                kind + ": кадр фитинга обязан быть теснее пола дистанции общей камеры — "
                + "иначе ДУ 20 занимает несколько процентов снимка и тройник от муфты "
                + "на нём не отличить");
            Assert.Greater(spanMM,
                PipeFittingSpec.HeightMm(kind, PipeSpec.DEFAULT_SIZE),
                kind + ": но не теснее самого фитинга — кадр обязан вмещать его целиком");
        }

        Assert.AreEqual(6, checkedKinds,
            "фитингов шесть; посчитай их меньше — и проверка выше прошла бы на пустоте");
    }

    [UnityTest]
    public IEnumerator IsoPipeElbow_Dn20()
    {
        yield return RenderFitting(ElementFactory.CreatePipeElbow("IsoElbow", Vector3.zero),
            PipeNodeKind.Elbow, "iso_pipe_elbow_dn20.png");
    }

    [UnityTest]
    public IEnumerator IsoPipeCoupling_Dn20()
    {
        yield return RenderFitting(ElementFactory.CreatePipeCoupling("IsoCoupling", Vector3.zero),
            PipeNodeKind.Coupling, "iso_pipe_coupling_dn20.png");
    }

    [UnityTest]
    public IEnumerator IsoPipeTee_Dn20()
    {
        yield return RenderFitting(ElementFactory.CreatePipeTee("IsoTee", Vector3.zero),
            PipeNodeKind.Tee, "iso_pipe_tee_dn20.png");
    }

    [UnityTest]
    public IEnumerator IsoPipeCap_Dn20()
    {
        yield return RenderFitting(ElementFactory.CreatePipeCap("IsoCap", Vector3.zero),
            PipeNodeKind.Cap, "iso_pipe_cap_dn20.png");
    }

    [UnityTest]
    public IEnumerator IsoPipeSupply_Dn20()
    {
        yield return RenderFitting(ElementFactory.CreatePipeSupply("IsoSupply", Vector3.zero),
            PipeNodeKind.Supply, "iso_pipe_supply_dn20.png");
    }

    [UnityTest]
    public IEnumerator IsoPipeReturn_Dn20()
    {
        yield return RenderFitting(ElementFactory.CreatePipeReturn("IsoReturn", Vector3.zero),
            PipeNodeKind.Return, "iso_pipe_return_dn20.png");
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

        float before = facade!.transform.position.y;
        StandOnFloor(facade!);
        Assert.AreEqual(FacadeElement.DEFAULT_GAP_MM * AppConstants.MM_TO_UNITS,
            facade!.transform.position.y - before, 1e-6f,
            "постановка идёт по коробке ВАЛИДАЦИИ: она ниже физического низа ровно на "
            + "нижний притвор, поэтому подъём обязан совпасть с ним до миллиметра");

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
        yield return null;

        // Кадр был розовым целиком. Опору прибора судит DWH-05, а не COL-02:
        // объём валидации начинается на 90 мм выше подошвы, и под ним пусто
        // при ЛЮБОЙ правильной установке — см. DishwasherElementTests,
        // «Dishwasher_OnTheFloor_IsNotReportedUnsupported…». Что нарушений нет,
        // спрашивает сама съёмка (ElementFrameTests.CaptureFramePng).
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
        yield return null;

        // Замер доли «розового» по кадру объявил этот снимок нарушением на 33 %.
        // Это не тинт: светильник ЖЁЛТЫЙ, а тинта на нём не бывает вовсе —
        // ElementHighlighter.KeepsItsOwnMaterialAlways выводит LightSourceElement
        // из покраски, и ElementKind.Decor выводит его из парных проверок и из
        // опоры. Правило под мебель до светильника не дотягивается, и кадр это
        // фиксирует, чтобы следующий замер цвета не отправил чинить исправное.
        yield return RenderElementIso(go, "iso_light_source.png", 3f);
    }
}
