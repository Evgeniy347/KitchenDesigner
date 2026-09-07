using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Golden-master snapshot tests: каждый тест генерирует JSON сцены/элемента
/// и сравнивает с эталонным файлом в Snapshots/.
///
/// Первый запуск: снапшот-файл создаётся, тест FAIL → разработчик проверяет
/// содержимое, коммитит файл, перезапускает → PASS.
/// При изменении кода сериализации тест ловит любую разницу в JSON.
///
/// Чтобы обновить ВСЕ снапшоты разом: запустить UpdateAllSnapshots (Explicit).
/// Чтобы сбросить один снапшот: удалить файл из Snapshots/ и перезапустить тест.
/// </summary>
public class SnapshotTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private KitchenSettingsData? _settingsBefore;
    private ResizeHandleManager.HandleMode _modeBefore;

    /// <summary>Снапшот содержит не только элементы, но и ГЛОБАЛЬНОЕ состояние
    /// приложения: блок настроек (KitchenSettings — синглтон-ассет из Resources)
    /// и режим ручек (ResizeHandleManager.Mode — статик, который переключает даже
    /// загрузка сейва). Оба правятся другими EditMode-тестами и не всегда
    /// восстанавливаются, поэтому результат зависел от порядка выполнения: в
    /// полном прогоне тесты проходили, а с -testFilter SnapshotTests падали 19 из
    /// 21. Полный сброс перед КАЖДЫМ тестом делает эталоны воспроизводимыми в
    /// любом окружении; TearDown возвращает застигнутое состояние, чтобы сами
    /// снапшоты не ломали соседние наборы.
    ///
    /// Третий кусок того же рода — реестр окон: снимок сериализует блок windows,
    /// а ProjectWindows это статический список. Окно, пережившее чужой TearDown,
    /// всплывает здесь как windows:[settings] в сценах, где UI вообще не строят.
    /// Само окно у SnapshotTests не строится ни одно, поэтому пустой список —
    /// единственный правильный эталон, и Clear() делает его независимым от
    /// соседей.</summary>
    [SetUp]
    public void Setup()
    {
        var s = KitchenSettings.Instance;
        _settingsBefore = s != null ? s.ToData() : null;
        _modeBefore = ResizeHandleManager.Mode;

        if (s != null) s.ResetToDefaults();
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        ProjectWindows.Clear();
        ResetScene();
    }

    [TearDown]
    public void Teardown()
    {
        ResetScene();

        var s = KitchenSettings.Instance;
        if (s != null && _settingsBefore != null) s.ApplyFrom(_settingsBefore);
        _settingsBefore = null;
        ResizeHandleManager.SetMode(_modeBefore);
    }

    private void ResetScene()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();

        // Project-level state is serialized INTO the snapshot, so anything a
        // neighbouring test loaded (SnapMutationTests restores docs/example.save.json,
        // whose contents change whenever the desktop autosaves) would otherwise
        // leak into every baseline here.
        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
        // Тонировка тоже project-level и тоже сериализуется в снапшот: её ставит
        // RestoreScene из файла проекта, а грузят example.save.json сразу
        // несколько наборов (SnapMutationTests, SaveValidationTests, SinkRealSceneTests).
        // Стоит десктопу сохранить проект с выключенной тонировкой — и эталоны
        // здесь краснеют по причине, к ним не относящейся.
        ElementHighlighter.TintEnabled = true;
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private KitchenElement? Add(GameObject go)
    {
        _spawned.Add(go);
        var el = go.GetComponent<KitchenElement>();
        if (el != null) PartRegistry.Register(el);
        return el;
    }

    private string CaptureJson(IEnumerable<KitchenElement> elements)
    {
        var data = SaveLoadManager.CaptureScene(elements);
        return SaveLoadManager.Serialize(data);
    }

    // ── Board (KitchenElement) snapshots ─────────────────────────────────

    [Test]
    public void Snapshot_Board_Default()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "DefaultBoard", Vector3.zero);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "board_default");
    }

    [Test]
    public void Snapshot_Board_CustomProperties()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 600, 18), "CustomBoard",
            new Vector3(1.5f, 2.5f, -3.0f));
        var el = Add(go);
        go.transform.rotation = ManagedRotation.Euler(0, 45, 0);
        el!.Movable = false;
        el!.Transparent = true;
        el!.MaterialId = "oak";

        var json = CaptureJson(new[] { el! });
        Snapshot.Match(json, "board_custom_properties");
    }

    [Test]
    public void Snapshot_Board_Multiple()
    {
        var elements = new List<KitchenElement>();
        for (int i = 0; i < 3; i++)
        {
            var go = ElementFactory.CreatePart(
                new Vector3Int(400 + i * 200, 400, 18),
                $"Board_{i}",
                new Vector3(i * 0.5f, 0.2f, 0));
            elements.Add(Add(go)!);
        }
        var json = CaptureJson(elements);
        Snapshot.Match(json, "board_multiple");
    }

    // ── FacadeElement snapshots ──────────────────────────────────────────

    [Test]
    public void Snapshot_Facade_Default()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "DefaultFacade", Vector3.zero,
            gapLeft: 2, gapRight: 2, gapTop: 2, gapBottom: 2);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "facade_default");
    }

    [Test]
    public void Snapshot_Facade_CustomGaps()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(600, 716, 18), "CustomFacade",
            new Vector3(1.0f, 0.4f, -2.0f),
            gapLeft: 3, gapRight: 5, gapTop: 2, gapBottom: 2);
        var facade = go.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.DrawerOut;
        facade.MaterialId = "wenge";
        Add(go);

        var json = CaptureJson(new[] { facade });
        Snapshot.Match(json, "facade_custom");
    }

    [Test]
    public void Snapshot_Facade_All18Modes()
    {
        var elements = new List<KitchenElement>();
        var allModes = new[]
        {
            DoorMode.HingeFrontLeft, DoorMode.HingeFrontRight, DoorMode.HingeFrontTop, DoorMode.HingeFrontBottom,
            DoorMode.HingeBackLeft, DoorMode.HingeBackRight, DoorMode.HingeBackTop, DoorMode.HingeBackBottom,
            DoorMode.HingeEdgeTopLeft, DoorMode.HingeEdgeTopRight, DoorMode.HingeEdgeBottomLeft, DoorMode.HingeEdgeBottomRight,
            DoorMode.DrawerOut, DoorMode.DrawerIn, DoorMode.DrawerRight, DoorMode.DrawerLeft, DoorMode.DrawerUp, DoorMode.DrawerDown,
        };

        for (int i = 0; i < allModes.Length; i++)
        {
            var go = ElementFactory.CreateFacade(
                new Vector3Int(450, 700, 18), $"Mode_{i}", new Vector3(i * 0.1f, 0, 0));
            var facade = go.GetComponent<FacadeElement>();
            facade.Mode = allModes[i];
            Add(go);
            elements.Add(facade);
        }

        var json = CaptureJson(elements);
        Snapshot.Match(json, "facade_all_18_modes");
    }

    // ── AssembledFacadeElement snapshots ─────────────────────────────────

    [Test]
    public void Snapshot_Assembled_Blind()
    {
        var go = ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 800, 18), "BlindAssembled",
            new Vector3(0.3f, 0.4f, -2.0f), AssembledFill.Blind);
        var assembled = go.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 3;
        assembled.Mode = DoorMode.HingeFrontRight;
        assembled.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { assembled });
        Snapshot.Match(json, "assembled_blind");
    }

    [Test]
    public void Snapshot_Assembled_Glass()
    {
        var go = ElementFactory.CreateAssembledFacade(
            new Vector3Int(450, 600, 18), "GlassAssembled",
            new Vector3(0.1f, 0.3f, -1.5f), AssembledFill.Glass);
        var assembled = go.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 0;
        assembled.Mode = DoorMode.HingeFrontLeft;
        Add(go);

        var json = CaptureJson(new[] { assembled });
        Snapshot.Match(json, "assembled_glass");
    }

    [Test]
    public void Snapshot_Assembled_Open()
    {
        var go = ElementFactory.CreateAssembledFacade(
            new Vector3Int(500, 800, 18), "OpenAssembled",
            Vector3.zero, AssembledFill.Open);
        var assembled = go.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 2;
        Add(go);

        var json = CaptureJson(new[] { assembled });
        Snapshot.Match(json, "assembled_open");
    }

    // ── RadialShelfElement snapshots ─────────────────────────────────────

    // ── Screw leg snapshots ──────────────────────────────────────────────

    [Test]
    public void Snapshot_ScrewLeg_Default()
    {
        var go = ElementFactory.CreateScrewLeg("DefaultScrewLeg", Vector3.zero);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "screw_leg_default");
    }

    [Test]
    public void Snapshot_ScrewLeg_Custom()
    {
        var go = ElementFactory.CreateScrewLeg("CustomScrewLeg", new Vector3(0.4f, 0.05f, -1.0f));
        var leg = go.GetComponent<ScrewLegElement>();
        leg.Thread = ScrewLegSpec.ThreadM10;
        leg.BaseDiameterMM = 40;
        leg.BaseHeightMM = 12;
        leg.ThreadLengthMM = 90;
        leg.InsertionDepthMM = 18;
        leg.AttachedToName = "SomeSidePanel";
        Add(go);

        var json = CaptureJson(new[] { leg });
        Snapshot.Match(json, "screw_leg_custom");
    }

    // ── Pipe snapshots ───────────────────────────────────────────────────

    [Test]
    public void Snapshot_Pipe_Default()
    {
        var go = ElementFactory.CreatePipe(KitchenDesigner.Core.Plumbing.PipeSpec.DEFAULT_SIZE,
            PipeElementSpec.DEFAULT_LENGTH_MM, "DefaultPipe", Vector3.zero);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "pipe_default");
    }

    [Test]
    public void Snapshot_Pipe_Custom()
    {
        var go = ElementFactory.CreatePipe(KitchenDesigner.Core.Plumbing.PipeSpec.Dn40, 1250,
            "CustomPipe", new Vector3(0.4f, 0.625f, -1.0f));
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "pipe_custom");
    }

    /// <summary>Все шесть фитингов в одном снимке — ровно потому, что писать в
    /// файл им почти нечего: вид и место. Отдельные снимки на каждый различались
    /// бы одной строкой, а одним общим сразу видно, что шесть записей РАЗНЫЕ, и
    /// подмена вида в ElementCapture красит эталон целиком.</summary>
    [Test]
    public void Snapshot_PipeFittings_AllSixKinds()
    {
        var made = new[]
        {
            ElementFactory.CreatePipeElbow("SnapElbow", new Vector3(0f, 0.5f, 0f)),
            ElementFactory.CreatePipeCoupling("SnapCoupling", new Vector3(1f, 0.5f, 0f)),
            ElementFactory.CreatePipeTee("SnapTee", new Vector3(2f, 0.5f, 0f)),
            ElementFactory.CreatePipeCap("SnapCap", new Vector3(3f, 0.5f, 0f)),
            ElementFactory.CreatePipeSupply("SnapSupply", new Vector3(4f, 0.5f, 0f)),
            ElementFactory.CreatePipeReturn("SnapReturn", new Vector3(5f, 0.5f, 0f)),
        };

        var elements = new KitchenElement[made.Length];
        for (int i = 0; i < made.Length; i++)
        {
            Add(made[i]);
            elements[i] = made[i].GetComponent<KitchenElement>();
        }

        Snapshot.Match(CaptureJson(elements), "pipe_fittings_all");
    }

    [Test]
    public void Snapshot_RadialShelf_Default()
    {
        var go = ElementFactory.CreateRadialShelf(600, 400, 18, 200, "DefaultRadial", Vector3.zero);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "radial_default");
    }

    [Test]
    public void Snapshot_RadialShelf_Custom()
    {
        var go = ElementFactory.CreateRadialShelf(450, 450, 18, 450, "BigRadial",
            new Vector3(0.5f, 0.01f, -1.0f));
        var shelf = go.GetComponent<RadialShelfElement>();
        go.transform.rotation = ManagedRotation.Euler(0, 90, 0);
        shelf.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { shelf });
        Snapshot.Match(json, "radial_custom");
    }

        // ── Window snapshots ──────────────────────────────────────────────────

        [Test]
        public void Snapshot_Window_Default()
        {
            var go = ElementFactory.CreateWindow(
                new Vector3Int(900, 1200, 100), "DefaultWindow", Vector3.zero);
            Add(go);
            var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
            Snapshot.Match(json, "window_default");
        }

        [Test]
        public void Snapshot_Window_Tinted()
        {
            var go = ElementFactory.CreateWindow(
                new Vector3Int(600, 800, 100), "TintedWindow",
                new Vector3(0.5f, 0.4f, -1.0f), GlassTint.Tinted, 70);
            var window = go.GetComponent<WindowElement>();
            go.transform.rotation = ManagedRotation.Euler(0, 90, 0);
            window.MaterialId = "oak";
            Add(go);

            var json = CaptureJson(new[] { window });
            Snapshot.Match(json, "window_tinted");
        }

        // ── Wall snapshots ───────────────────────────────────────────────────

        [Test]
        public void Snapshot_Wall_Default()
    {
        var go = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 3000), "DefaultWall",
            new Vector3(-1.6f, 1.35f, 0));
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "wall_default");
    }

    [Test]
    public void Snapshot_Wall_CustomMaterial()
    {
        var go = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 2000), "ConcreteWall",
            new Vector3(0, 1.35f, -3.5f));
        var el = Add(go);
        el!.MaterialId = "concrete";

        var json = CaptureJson(new[] { el! });
        Snapshot.Match(json, "wall_custom");
    }

    // ── ChairElement snapshot ────────────────────────────────────────────

    /// <summary>Стул несёт ДВЕ величины формы разом — радиус сиденья и высоту
    /// сиденья, — и обе живут в полях, у которых есть чужая история:
    /// <c>cornerRadius</c> общий с радиусной полкой и стартует с 200, а
    /// <c>seatHeightMM</c> просто новый. Снимок берётся с НЕумолчальными
    /// значениями обеих: на умолчаниях забытая запись поля неотличима от
    /// записанной.</summary>
    [Test]
    public void Snapshot_Chair_Custom()
    {
        var go = ElementFactory.CreateChair(new Vector3Int(600, 900, 400), 137, 512,
            "BigChair", new Vector3(0.5f, 0.45f, -1.0f));
        var chair = go.GetComponent<ChairElement>();
        chair.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "chair_custom");
    }

    // ── SofaElement snapshot ─────────────────────────────────────────────

    /// <summary>Диван несёт те же ДВЕ величины формы, что и стул, но в ЧУЖИХ
    /// полях: <c>cornerRadius</c> стартует с 200 (радиусная полка), а
    /// <c>seatHeightMM</c> — с 450 (стул). Умолчания дивана другие (120 и 400),
    /// поэтому снимок берётся с НЕумолчальными значениями обеих: иначе забытая
    /// запись поля неотличима от записанной.</summary>
    [Test]
    public void Snapshot_Sofa_Custom()
    {
        var go = ElementFactory.CreateSofa(new Vector3Int(1800, 820, 950), 143, 371,
            "BigSofa", new Vector3(0.5f, 0.41f, -1.0f));
        var sofa = go.GetComponent<SofaElement>();
        sofa.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "sofa_custom");
    }

    // ── BedElement snapshot ──────────────────────────────────────────────

    /// <summary>Кровать несёт два СВОИХ булевых поля, и оба стартуют с
    /// <c>true</c> — двуспальная со спинкой. Снимок берётся с обоими в
    /// <c>false</c>: поле, которое забыли записать в ElementCapture, при
    /// умолчательном значении неотличимо от записанного, и именно так однажды
    /// квадратная табуретка загрузилась скруглённой.</summary>
    [Test]
    public void Snapshot_Bed_Custom()
    {
        var go = ElementFactory.CreateBed(new Vector3Int(940, 640, 1930), false, false,
            "SmallBed", new Vector3(0.5f, 0.32f, -1.0f));
        var bed = go.GetComponent<BedElement>();
        bed.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "bed_custom");
    }

    // ── PouffeElement snapshot ───────────────────────────────────────────

    /// <summary>Снимок берётся на НЕумолчальных значениях обоих собственных
    /// полей пуфика: 90 мм вместо 120 и 70 мм вместо 50. Поле, которое забыли
    /// записать в ElementCapture, при умолчательном значении неотличимо от
    /// записанного — ровно так квадратная табуретка однажды загрузилась
    /// скруглённой. Габарит тоже несимметричный: на 450x450 перепутанные оси
    /// дали бы тот же JSON.</summary>
    [Test]
    public void Snapshot_Pouffe_Custom()
    {
        var go = ElementFactory.CreatePouffe(new Vector3Int(520, 380, 410), 90, 70,
            "BigPouffe", new Vector3(0.5f, 0.19f, -1.0f));
        var pouffe = go.GetComponent<PouffeElement>();
        pouffe.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "pouffe_custom");
    }

    // ── Toilet snapshots ─────────────────────────────────────────────────

    /// <summary>Габарит у обоих унитазов фиксированный, поэтому снимок
    /// проверяет ровно то, что габаритом не проверяется: собственные поля.
    /// Оба сняты на НЕумолчальных значениях (460 вместо 400; 520 и 730 вместо
    /// 400 и 600) — поле, забытое в ElementCapture, при умолчании неотличимо от
    /// записанного, и ровно так квадратная табуретка однажды загрузилась
    /// скруглённой. У подвесного оба числа ещё и различны между собой: равные
    /// дали бы одинаковый JSON при перепутанных местами полях.</summary>
    [Test]
    public void Snapshot_Toilet_Custom()
    {
        var go = ElementFactory.CreateToilet(460, "BigToilet", new Vector3(1.2f, 0.395f, -0.7f));
        var toilet = go.GetComponent<ToiletElement>();
        toilet.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "toilet_custom");
    }

    [Test]
    public void Snapshot_WallHungToilet_Custom()
    {
        var go = ElementFactory.CreateWallHungToilet(520, 730, "HungToilet",
            new Vector3(-0.8f, 0.5f, 1.1f));
        var toilet = go.GetComponent<WallHungToiletElement>();
        toilet.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "wall_hung_toilet_custom");
    }

    // ── Full scene snapshot ──────────────────────────────────────────────

    [Test]
    public void Snapshot_FullScene_AllTypes()
    {
        var elements = new List<KitchenElement>();

        // Board
        var boardGo = ElementFactory.CreatePart(
            new Vector3Int(800, 400, 18), "Board_A",
            new Vector3(0.5f, 0.2f, 1.0f));
        elements.Add(Add(boardGo)!);

        // Facade
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "Facade_A",
            new Vector3(0.5f, 0.35f, -1.0f),
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.HingeFrontRight;
        elements.Add(facade);

        // Assembled
        var assembledGo = ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 800, 18), "Assembled_A",
            new Vector3(-0.3f, 0.4f, -2.0f), AssembledFill.Glass);
        var assembled = assembledGo.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 2;
        assembled.MaterialId = "oak";
        elements.Add(assembled);

        // Radial shelf
        var radialGo = ElementFactory.CreateRadialShelf(350, 350, 18, 350, "Radial_A",
            new Vector3(0.2f, 0.01f, -1.5f));
        elements.Add(Add(radialGo)!);

        // Wall
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 3000), "Wall_A",
            new Vector3(-1.6f, 1.35f, 0));
        elements.Add(Add(wallGo)!);

        var json = CaptureJson(elements);
        Snapshot.Match(json, "fullscene_all_types");
    }

    // ── KitchenSettings snapshots ────────────────────────────────────────

    [Test]
    public void Snapshot_Settings_Default()
    {
        var gs = KitchenSettings.Instance;
        gs.GridStep = 18; gs.GridEnabled = true; gs.SnapEnabled = true;
        gs.SnapThreshold = 50f; gs.BlockOnViolation = true;
        gs.AutoSave = true; gs.AutoSaveInterval = 60;
        gs.SpatialGrid = false; gs.WindowedMode = true;
        gs.NormalView.edgeOutline = true; gs.NormalView.wallsEnabled = true; gs.NormalView.lowerNearWalls = true;
        gs.NormalView.wallOutline = true; gs.NormalView.hideOpeningsOnLoweredWalls = false;
        gs.NormalView.objectsVisible = true; gs.NormalView.hideLightSources = false;
        gs.MouseSensitivity = 1f; gs.WasdSpeed = 1f; gs.ArrowSpeed = 1f;
        SetPhotoDefaults(gs);

        var json = JsonUtility.ToJson(gs.ToData(), true);
        Snapshot.Match(json, "settings_default");
    }

    [Test]
    public void Snapshot_Settings_Custom()
    {
        var gs = KitchenSettings.Instance;
        gs.GridStep = 32; gs.GridEnabled = false; gs.SnapEnabled = false;
        gs.SnapThreshold = 80f; gs.BlockOnViolation = false;
        gs.AutoSave = true; gs.AutoSaveInterval = 120;
        gs.SpatialGrid = true; gs.WindowedMode = false;
        gs.NormalView.edgeOutline = true; gs.NormalView.wallsEnabled = false; gs.NormalView.lowerNearWalls = true;
        gs.NormalView.wallOutline = false; gs.NormalView.hideOpeningsOnLoweredWalls = true;
        gs.NormalView.objectsVisible = false; gs.NormalView.hideLightSources = true;
        // Второй пресет отличается от первого — в снапшоте видно, что они
        // сохраняются раздельно, а не дублируют друг друга.
        gs.RoomView.edgeOutline = false; gs.RoomView.wallOutline = true;
        gs.RoomView.objectsVisible = true; gs.RoomView.hideLightSources = false;
        gs.MouseSensitivity = 2f; gs.WasdSpeed = 0.5f; gs.ArrowSpeed = 1.5f;
        SetPhotoDefaults(gs);

        var json = JsonUtility.ToJson(gs.ToData(), true);
        Snapshot.Match(json, "settings_custom");
    }

    private static void SetPhotoDefaults(KitchenSettings gs)
    {
        gs.PhotoQuality = PhotoQualityPreset.High;
        gs.PhotoShadows = true;
        gs.PhotoSoftShadows = true;
        gs.PhotoAntiAliasing = true;
        gs.PhotoSupersampling = true;
        gs.PhotoAmbientOcclusion = true;
        gs.PhotoBloom = true;
        gs.PhotoVignette = true;
        gs.PhotoCeiling = true;
        gs.PhotoSSGI = true;
    }

    // ── Full ProjectData snapshot (elements + groups + camera + baseplate) ─

    [Test]
    public void Snapshot_FullProjectData()
    {
        var elements = new List<KitchenElement>();

        // Board + Facade
        var boardGo = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board_1",
            new Vector3(0.5f, 0.2f, 0.0f));
        elements.Add(boardGo.GetComponent<KitchenElement>());
        Add(boardGo);

        var facadeGo = ElementFactory.CreateFacade(new Vector3Int(450, 700, 18), "Facade_1",
            new Vector3(0.0f, 0.35f, -1.0f), 3, 3, 2, 2);
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.HingeFrontLeft;
        elements.Add(facade);
        Add(facadeGo);

        // Groups — link board and facade
        var group = GroupManager.Link(new List<KitchenElement> {
            boardGo.GetComponent<KitchenElement>(), facade });

        // BasePlate
        var bp = BasePlate.Create();
        bp.Element.DimensionsMM = new Vector3Int(3500, 18, 4000);
        bp.transform.position = new Vector3(0, -0.009f, 0);
        _spawned.Add(bp.gameObject);

        // Capture scene
        var data = SaveLoadManager.CaptureScene(elements);

        // Inject camera state (not available in EditMode)
        data.camera = new CameraState
        {
            valid = true,
            targetX = 0.5f, targetY = 0.3f, targetZ = -1.0f,
            angleX = 35f, angleY = 45f, distance = 6.0f
        };

        var json = SaveLoadManager.Serialize(data);
        Snapshot.Match(json, "full_project_data");
    }

    // ── Мета-тест: проверка что снапшоты для всего выше созданы ──────────

    [Test]
    public void AllSnapshots_Exist()
    {
        var names = new[]
        {
            "board_default", "board_custom_properties", "board_multiple",
            "facade_default", "facade_custom", "facade_all_18_modes",
            "assembled_blind", "assembled_glass", "assembled_open",
            "radial_default", "radial_custom",
            "chair_custom",
            "wall_default", "wall_custom",
            "window_default", "window_tinted",
            "fullscene_all_types",
            "settings_default", "settings_custom",
            "full_project_data",
            "undo_composite_two_moves", "undo_resize_command",
        };

        var missing = new System.Text.StringBuilder();
        foreach (var name in names)
        {
            if (!Snapshot.Exists(name))
                missing.AppendLine($"  - {name}.verified.json");
        }

        if (missing.Length > 0)
            Assert.Fail(
                $"Missing verified snapshots:\n{missing}\n" +
                "Run SnapshotTests (this fixture) once — it will create *.candidate.json files.\n" +
                "Then run AcceptAllCandidates (Explicit) to rename them to *.verified.json.");
    }

    // ── Accept candidates (Explicit — только по запросу) ─────────────────

    [Test]
    [Explicit]
    public void AcceptAllCandidates()
    {
        var dir = Snapshot.SnapshotDir;
        var candidates = System.IO.Directory.GetFiles(dir, "*.candidate.json");
        if (candidates.Length == 0)
            Assert.Ignore("No candidate files to accept.");

        foreach (var candidate in candidates)
        {
            var verified = candidate.Replace(".candidate.json", ".verified.json");
            System.IO.File.Copy(candidate, verified, overwrite: true);
            System.IO.File.Delete(candidate);
            TestContext.WriteLine($"  Accepted: {System.IO.Path.GetFileName(verified)}");
        }

        Assert.Pass($"Accepted {candidates.Length} snapshot(s). Commit the new *.verified.json files.");
    }
}
