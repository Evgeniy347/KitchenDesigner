using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode: опущенная стена, снятая изометрической камерой — крышки
/// стены против проёмов.
///
/// Зачем кадр, если арифметику выреза уже стерегут EditMode-тесты
/// (<c>WallCapSpansTests</c>, <c>DoorThresholdTests</c>). Те спрашивают про
/// пролёты и нормали — числа, которые сходятся и тогда, когда пользователь
/// видит поперёк дверного проёма горизонтальную плитку. Именно так дефект
/// 1a1619aa и прожил: 6613e108 стерёг пол (y = −0,5), 77fa83fd — переднюю
/// грань (z = +0,5), а ВЕРХНЯЯ крышка на y = +0,5 не попадала ни в один
/// сенсор. Здесь вопрос задаётся глазами камеры: сколько пикселей стены
/// оказалось там, где стены быть не должно.
///
/// ЧТО СЧИТАЕТСЯ. Не «силуэт» и не весь кадр: доля считается по ЯВНО
/// очерченному четырёхугольнику — куску реальной поверхности стены, заданному
/// мировыми координатами (полоса верхней крышки над проёмом, панель передней
/// грани). Знаменатель — пиксели внутри этого четырёхугольника, и он известен
/// заранее. Урок про знаменатель оплачен на соседнем наборе кадров: «всё, что
/// отличается от заливки камеры» затянуло в себя пол, и целиком жёлтая дверь
/// печаталась как 0,23 (CONVENTIONS.md → «A guard that measures a SHARE needs
/// a test for its denominator»).
///
/// В КАЖДОМ кадре есть и патч, обязанный быть ПУСТЫМ, и патч, обязанный быть
/// ПОЛНЫМ. Без второго тест «в проёме нет стены» проходил бы на кадре, где нет
/// вообще ничего — не туда смотрящая камера, не отрисованная стена, не тот
/// слой. Полный патч снят той же камерой в том же кадре, поэтому он отвечает
/// ровно за это.
///
/// ЧЕМ стена отделена от сцены. Тело стены уезжает на отдельный слой
/// <c>FrameLayer</c>, а камера кадра получает <c>cullingMask</c> ровно из
/// этого слоя. Решение принимается в момент ОТРИСОВКИ и по слою объекта:
/// <c>renderer.enabled = false</c> на чужих объектах не работает, его
/// возвращает чужой Update (CONVENTIONS.md → «Isolation for a measurement must
/// be decided at DRAW time»). Дыры маски две, обе закрыты настройками:
/// <c>SpatialGridRenderer</c> и <c>EdgeOutlineRenderer</c> рисуют через
/// <c>GL</c> из <c>OnRenderObject</c> и слой не спрашивают — а чёрный контур
/// габаритов стены прошёл бы прямо поперёк проёма и сам стал бы «плиткой».
///
/// РАКУРС. Камера ортографическая (истинная изометрия: параллакс не зависит от
/// того, куда попал пиксель), поднята на 38° над горизонтом и повёрнута на 25°
/// от нормали стены. Оба угла не косметические. Сверху видно верхнюю крышку —
/// ту самую поверхность, о которой весь тест; при этом на 38° луч, вошедший в
/// проём, опускается за 100 мм толщины стены на 78 мм, то есть проходит
/// огрызок высотой 100 мм НАСКВОЗЬ и упирается в фон, а не в собственную
/// изнанку стены (на 45° и выше он бы уже не прошёл). Боковой снос за ту же
/// толщину — 47 мм, поэтому патчи сжаты к своему центру до 0,7: запас в 15% от
/// 900-мм проёма это 135 мм, втрое больше сноса.
///
/// ФОН выбран пурпурным намеренно. Он не встречается ни в одном материале
/// проекта, поэтому «пиксель стены» отличается от фона с запасом даже там, где
/// стена в собственной тени, — и на PNG человеку видно дыру, а не догадку.
///
/// Эталонных PNG нет и не будет: сравниваются не кадры, а доля внутри области,
/// заданной геометрией стенда. Принимать снапшоты после каждой правки света
/// поэтому не придётся.</summary>
public class LoweredWallOpeningFrameTests
{
    private const int RenderW = 1536;
    private const int RenderH = 1024;

    /// <summary>Слой, на котором в кадре стоит только тело стены. Тридцать
    /// первый свободен: в TagManager названы Default, TransparentFX,
    /// Ignore Raycast, Water и UI, а продуктовый код слои не назначает.</summary>
    private const int FrameLayer = 31;

    /// <summary>Доля пикселей стены внутри патча, который обязан быть пустым.
    /// До 1a1619aa полоса верхней крышки над дверным проёмом была сплошной —
    /// там стояла 1,0. После — 0: сквозь проём опущенной стены видно фон.
    /// Пять сотых отделяет эти два исхода с запасом на краевой пиксель: патч
    /// сжат до 0,7 и стоит от настоящей стены дальше, чем боковой снос луча,
    /// так что заполнить его нечему; даже периметр самой узкой из областей
    /// (полоса крышки, около 40×340 px в кадре 1536×1024) — это 6% её площади,
    /// то есть до порога не дотянул бы и сплошь ошибочный край.</summary>
    private const float MaxWallShareWhereItMustBeOpen = 0.05f;

    /// <summary>Доля пикселей стены внутри патча, который обязан быть сплошным.
    /// Зеркало предыдущего порога: 0,95 отделяет «поверхность цела» от любого
    /// выреза размером больше краевого пикселя. Ловит противоположную ошибку —
    /// вырезать крышку под КАЖДЫМ проёмом: подоконная полоска и верх обычной
    /// стены обязаны остаться целыми.</summary>
    private const float MinWallShareWhereItMustBeSolid = 0.95f;

    /// <summary>Патч сжимается к своему центру: измеряется середина
    /// поверхности, а не её край. 0,7 выбрано по боковому сносу луча (47 мм
    /// против 135 мм запаса на 900-мм проёме), см. «РАКУРС» выше.</summary>
    private const float PatchShrink = 0.7f;

    /// <summary>Меньше этого патч в кадр не помещается настолько, чтобы доля
    /// что-то значила: 2000 пикселей — это примерно 45×45. Проверка ловит
    /// «элемент за кадром» и «камера смотрит мимо» ДО того, как тест начнёт
    /// рассуждать о доле.</summary>
    private const int MinPatchPixels = 2000;

    private const int ColorDelta = 12;

    /// <summary>Пурпур: ни один материал проекта в него не красится.</summary>
    private static readonly Color Background = new Color(0.85f, 0.10f, 0.60f, 1f);

    /// <summary>Подъём 38°, поворот 25° от нормали стены — обоснование в
    /// «РАКУРС» выше. Направление ОТ цели К камере.</summary>
    private static readonly Vector3 IsoDir = new Vector3(0.333f, 0.616f, -0.714f).normalized;

    private static readonly Vector3Int WallDims = new Vector3Int(3000, 2500, 100);
    private static readonly Vector3Int DoorDims = new Vector3Int(900, 2100, 100);
    private static readonly Vector3Int WindowDims = new Vector3Int(900, 1200, 100);

    private KitchenSettingsData? _settingsBackup;
    private bool _violationTintWas;
    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private Wall? _wall;
    private KitchenElement? _door;
    private KitchenElement? _window;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera.tag = "MainCamera";
        _mainCamera.AddComponent<Camera>();
        _mainCamera.transform.position = new Vector3(0f, 3f, -5f);
        _mainCamera.transform.LookAt(Vector3.zero);

        SaveLoadManager.LastPath = "";
        var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        if (File.Exists(autoPath)) File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;

        // Сцену Bootstrap мог набить демо-проектом. Чужая стена рядом — не
        // косметика: WallOpeningElement привязывается к БЛИЖАЙШЕЙ стене, так
        // что дверь стенда уехала бы вырезать чужой меш, а чужое окно —
        // прорезать наш. Стенд должен быть единственным.
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        yield return null;

        // Режим мог остаться от соседнего теста, а в Room и Photo опускание
        // стен разрешает не NormalView, а другой пресет.
        EditModeManager.SetMode(EditMode.Normal);

        var settings = KitchenSettings.Instance;
        _settingsBackup = settings != null ? settings.ToData() : null;
        if (settings != null)
        {
            settings.SpatialGrid = false;
            settings.NormalView.wallsEnabled = true;
            settings.NormalView.wallOutline = false;
            settings.NormalView.edgeOutline = false;
            settings.NormalView.hideOpeningsOnLoweredWalls = false;
        }

        _violationTintWas = ElementHighlighter.ViolationTintVisible;
        ElementHighlighter.ViolationTintVisible = false;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        ElementHighlighter.ViolationTintVisible = _violationTintWas;

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

    /// <summary>Дверной проём опущенной стены обязан быть пуст на всю высоту
    /// огрызка. Красный патч этого теста — полоса ВЕРХНЕЙ крышки над проёмом:
    /// до 1a1619aa она была сплошной и садилась в 100 мм над полом поперёк
    /// прохода. Второй пустой патч (передняя грань) стерёг ту же дыру с
    /// 77fa83fd и обязан остаться зелёным — если покраснеет ОН, сломалось
    /// что-то другое, чем эта правка.</summary>
    [UnityTest]
    public IEnumerator IsoLoweredWall_DoorOpening_HasNoCapStripAcrossIt()
    {
        yield return BuildStand(lowered: true);

        var wall = _wall!;
        Bench(wall, lowered: true);

        float top = TopOf(wall);
        float baseY = BaseOf(wall);
        float frontZ = FrontOf(wall);
        float backZ = BackOf(wall);
        var door = SpanOf(_door!);

        var capOverDoor = CapPatch("полоса верхней крышки над дверным проёмом",
            top, door, frontZ, backZ);
        var faceOverDoor = FacePatch("передняя грань в дверном проёме",
            frontZ, door, baseY, top);
        // Контроль в ТОМ ЖЕ кадре: кусок стены слева от проёма, где огрызок
        // сплошной. Без него «в проёме пусто» прошло бы на пустом кадре.
        var solidSpan = new Span(door.Min - 0.60f, door.Min - 0.15f);
        var capBesideDoor = CapPatch("полоса верхней крышки рядом с проёмом",
            top, solidSpan, frontZ, backZ);
        var faceBesideDoor = FacePatch("передняя грань рядом с проёмом",
            frontZ, solidSpan, baseY, top);

        var cam = AimAt(capOverDoor, faceOverDoor, capBesideDoor, faceBesideDoor);
        yield return Shoot(cam, wall, "wall_lowered_doorway_no_cap_strip.png",
            capOverDoor, faceOverDoor, capBesideDoor, faceBesideDoor);

        AssertSolid(capBesideDoor,
            "рядом с проёмом огрызок стены обязан быть виден — иначе кадр пуст, "
            + "и «в проёме нет стены» ничего не доказывает");
        AssertSolid(faceBesideDoor,
            "передняя грань огрызка рядом с проёмом обязана быть видна");

        AssertOpen(capOverDoor,
            "верхняя крышка опущенной стены обязана быть вырезана дверным "
            + "проёмом: иначе пользователь видит горизонтальную плитку в 100 мм "
            + "над полом поперёк прохода (дефект, закрытый 1a1619aa)");
        AssertOpen(faceOverDoor,
            "передняя грань в дверном проёме вырезана с 77fa83fd; её краснота "
            + "означает поломку НЕ верхней крышки, а грани");
    }

    /// <summary>Обратная сторона той же правки. Окно не достаёт ни до низа, ни
    /// до верха огрызка, поэтому подоконная полоска в 100 мм — сплошной короб
    /// вместе со своей крышкой. Тест краснеет, если вырез крышки сделать
    /// безусловным: «починить» дверь, разрезав стену под каждым проёмом.</summary>
    [UnityTest]
    public IEnumerator IsoLoweredWall_WindowSill_StaysWholeIncludingItsTop()
    {
        yield return BuildStand(lowered: true);

        var wall = _wall!;
        Bench(wall, lowered: true);

        float top = TopOf(wall);
        float baseY = BaseOf(wall);
        float frontZ = FrontOf(wall);
        float backZ = BackOf(wall);
        var win = SpanOf(_window!);

        var capUnderWindow = CapPatch("верхняя крышка подоконной полоски",
            top, win, frontZ, backZ);
        var faceUnderWindow = FacePatch("передняя грань подоконной полоски",
            frontZ, win, baseY, top);
        // Пустой контроль: воздух над огрызком. Начинается заметно выше края —
        // крышка при взгляде сверху проецируется на экран ВЫШЕ верхнего ребра
        // передней грани, и патч, начатый вплотную, поймал бы её саму.
        var airAboveStub = FacePatch("воздух над опущенной стеной",
            frontZ, win, top + 0.25f, top + 0.55f);

        var cam = AimAt(capUnderWindow, faceUnderWindow, airAboveStub);
        yield return Shoot(cam, wall, "wall_lowered_window_sill_strip_solid.png",
            capUnderWindow, faceUnderWindow, airAboveStub);

        AssertOpen(airAboveStub,
            "над опущенной стеной стены нет — если и здесь «стена», то кадр "
            + "меряет не стену, и доля ниже ничего не значит");

        AssertSolid(capUnderWindow,
            "окно не достаёт до верха огрызка, поэтому верхняя крышка "
            + "подоконной полоски обязана остаться целой: дыра здесь — это "
            + "вырез крышки, сделанный безусловно, вместо вырезa под теми "
            + "проёмами, которые до края действительно доходят");
        AssertSolid(faceUnderWindow,
            "подоконная полоска в 100 мм — сплошная стена: вырезов окна у "
            + "опущенной стены нет вовсе");
    }

    /// <summary>Обычная стена в полный рост. Дверь 2100 мм не достаёт до верха
    /// стены 2500 мм, значит крышка над ней цела, а грань между притолокой и
    /// верхом — сплошная. Пустой патч того же кадра — сам проём: он
    /// доказывает, что вырез вообще есть и что сенсор умеет отвечать нулём
    /// здесь же, а не только на другой сцене.</summary>
    [UnityTest]
    public IEnumerator IsoFullHeightWall_AboveTheDoor_StaysWhole()
    {
        yield return BuildStand(lowered: false);

        var wall = _wall!;
        Bench(wall, lowered: false);

        float top = TopOf(wall);
        float frontZ = FrontOf(wall);
        float backZ = BackOf(wall);
        var door = SpanOf(_door!);
        float lintel = _door!.transform.position.y + _door!.DimensionsMM.y * 0.0005f;

        var capOverDoor = CapPatch("верхняя крышка стены над дверью",
            top, door, frontZ, backZ);
        var faceAboveLintel = FacePatch("грань между притолокой и верхом стены",
            frontZ, door, lintel + 0.05f, top - 0.05f);
        var openDoorway = FacePatch("сам дверной проём",
            frontZ, door, lintel - 0.50f, lintel - 0.10f);

        var cam = AimAt(capOverDoor, faceAboveLintel, openDoorway);
        yield return Shoot(cam, wall, "wall_fullheight_door_head_intact.png",
            capOverDoor, faceAboveLintel, openDoorway);

        AssertOpen(openDoorway,
            "под притолокой обязан быть проём: если стена и здесь сплошная, "
            + "дверь не прилипла к стене и весь кадр про пустой короб");

        AssertSolid(faceAboveLintel,
            "у стены в полный рост участок над дверью обязан остаться стеной");
        AssertSolid(capOverDoor,
            "дверь 2100 мм не доходит до верха стены 2500 мм, поэтому верхняя "
            + "крышка над ней целая: дыра здесь означает, что вырез крышки "
            + "перестал спрашивать, достаёт ли проём до края");
    }

    /// <summary>Стенд собирается В ПОЛНЫЙ РОСТ и только потом опускается.
    /// Порядок не косметический: <c>Wall.SetLowered</c> запоминает «полную»
    /// высоту в тот момент, когда его позвали впервые, — опусти стену в кадре
    /// её рождения, и запомнилась бы высота, которую <c>DimensionsMM</c> ещё не
    /// успел применить.</summary>
    private IEnumerator BuildStand(bool lowered)
    {
        var settings = KitchenSettings.Instance;
        if (settings != null)
        {
            settings.NormalView.lowerNearWalls = false;
            settings.NormalView.lowerAllWalls = false;
        }

        var wallGo = ElementFactory.CreateWall(WallDims, "СтендСтена",
            new Vector3(0f, WallDims.y * 0.0005f, 0f));
        _spawned.Add(wallGo);
        _wall = wallGo.GetComponent<Wall>();

        var doorGo = ElementFactory.CreateDoor(DoorDims, "СтендДверь",
            new Vector3(0f, DoorDims.y * 0.0005f, 0f), DoorSashType.Blind);
        _spawned.Add(doorGo);
        _door = doorGo.GetComponent<KitchenElement>();

        var windowGo = ElementFactory.CreateWindow(WindowDims, "СтендОкно",
            new Vector3(1f, 1.2f, 0f));
        _spawned.Add(windowGo);
        _window = windowGo.GetComponent<KitchenElement>();

        // Привязка ЯВНАЯ, как в WindowStripBugTests: полагаться на «ближайшую
        // стену» значит зависеть от того, что осталось в сцене.
        doorGo.GetComponent<DoorElement>()!.AttachToWall(_wall!);
        windowGo.GetComponent<WindowElement>()!.AttachToWall(_wall!);

        yield return null;
        yield return null;

        if (settings != null)
        {
            settings.NormalView.lowerNearWalls = lowered;
            settings.NormalView.lowerAllWalls = lowered;
        }

        yield return null;
        yield return null;
    }

    /// <summary>Стенд до измерения: доказывает, что снимается именно тот
    /// случай, о котором тест. Иначе доля пикселей была бы правдой о чём
    /// угодно (CONVENTIONS.md → «Prove the harness before you trust what it
    /// measures»).</summary>
    private void Bench(Wall wall, bool lowered)
    {
        Assert.AreEqual(1, wall.AttachedDoors.Count,
            "на стенде обязана висеть ровно одна дверь — она и режет проём");
        Assert.AreEqual(1, wall.AttachedWindows.Count,
            "на стенде обязано висеть ровно одно окно");
        Assert.Less(Quaternion.Angle(wall.transform.rotation, Quaternion.identity), 0.01f,
            "стена стенда не повёрнута: патчи заданы мировыми координатами");

        int plainVerts = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout>()).vertexCount;
        var mesh = wall.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у стены нет меша");
        Assert.Greater(mesh!.vertexCount, plainVerts,
            "в меше стены нет ни одного выреза — дверь не прилипла, и «в проёме "
            + "пусто» проверяло бы простой короб");

        Assert.AreEqual(lowered, wall.IsLowered,
            lowered
                ? "стена обязана быть опущена: без опускания верхняя крышка "
                  + "сидит на 2500 мм и дефекта не видно вовсе"
                : "стена обязана стоять в полный рост");

        float height = wall.transform.localScale.y;
        float expected = lowered
            ? WallManager.LoweredHeightMM * AppConstants.MM_TO_UNITS
            : WallDims.y * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(expected, height, 0.001f,
            "высота стены в кадре: " + (height * 1000f).ToString("0") + " мм вместо "
            + (expected * 1000f).ToString("0") + " мм");

        Assert.AreEqual(0f, BaseOf(wall), 0.001f, "низ стены обязан стоять на полу");
        Assert.AreEqual(0f, _door!.transform.position.y - _door!.DimensionsMM.y * 0.0005f, 0.001f,
            "низ двери обязан стоять на полу: приподнятая дверь дала бы под собой "
            + "полосу настоящей стены, и патч проёма покраснел бы законно");
    }

    private static float TopOf(Wall wall) =>
        wall.transform.position.y + wall.transform.localScale.y * 0.5f;

    private static float BaseOf(Wall wall) =>
        wall.transform.position.y - wall.transform.localScale.y * 0.5f;

    private static float FrontOf(Wall wall) =>
        wall.transform.position.z + wall.transform.localScale.z * 0.5f;

    private static float BackOf(Wall wall) =>
        wall.transform.position.z - wall.transform.localScale.z * 0.5f;

    private static Span SpanOf(KitchenElement opening)
    {
        float half = opening.DimensionsMM.x * 0.0005f;
        return new Span(opening.transform.position.x - half,
            opening.transform.position.x + half);
    }

    /// <summary>Горизонтальный патч на плоскости крышки: во всю толщину стены,
    /// по X — заданный пролёт.</summary>
    private static Patch CapPatch(string name, float y, Span x, float frontZ, float backZ) =>
        new Patch(name,
            new Vector3(x.Min, y, frontZ), new Vector3(x.Max, y, frontZ),
            new Vector3(x.Max, y, backZ), new Vector3(x.Min, y, backZ));

    /// <summary>Вертикальный патч на плоскости передней грани.</summary>
    private static Patch FacePatch(string name, float z, Span x, float y0, float y1) =>
        new Patch(name,
            new Vector3(x.Min, y0, z), new Vector3(x.Max, y0, z),
            new Vector3(x.Max, y1, z), new Vector3(x.Min, y1, z));

    /// <summary>Ортографическая изометрия, подогнанная по углам патчей: то, что
    /// тест меряет, обязано быть в кадре целиком и с запасом. Своя камера, а не
    /// Camera.main: на главной висит CameraController, который каждый Update
    /// заново ставит её на орбиту (CONVENTIONS.md → «A PlayMode pixel test
    /// measures the camera you actually have»).</summary>
    private Camera AimAt(params Patch[] patches)
    {
        var center = Vector3.zero;
        int corners = 0;
        foreach (var p in patches)
            foreach (var c in p.Corners) { center += c; corners++; }
        center /= corners;

        var camGo = new GameObject("WallFrameCam");
        _spawned.Add(camGo);
        camGo.transform.rotation = Quaternion.LookRotation(-IsoDir, Vector3.up);
        camGo.transform.position = center + IsoDir * 20f;

        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.orthographic = true;
        cam.aspect = (float)RenderW / RenderH;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        float halfX = 0f, halfY = 0f;
        foreach (var p in patches)
            foreach (var c in p.Corners)
            {
                var local = camGo.transform.InverseTransformPoint(c);
                halfX = Mathf.Max(halfX, Mathf.Abs(local.x));
                halfY = Mathf.Max(halfY, Mathf.Abs(local.y));
            }
        cam.orthographicSize = Mathf.Max(halfY, halfX / cam.aspect) * 1.25f;
        return cam;
    }

    /// <summary>Снимок стены и только стены: тело уезжает на кадровый слой,
    /// маска камеры — ровно этот слой. Доли считаются, пока targetTexture ещё
    /// назначена: снять её раньше — значит проецировать в pixelRect батч-экрана,
    /// читая пиксели из 1024×1024.</summary>
    private IEnumerator Shoot(Camera cam, Wall wall, string fileName, params Patch[] patches)
    {
        var body = wall.GetComponent<MeshRenderer>();
        Assert.IsNotNull(body, "у стены нет MeshRenderer — снимать нечего");
        Assert.IsTrue(body!.enabled,
            "рендерер стены выключен: настройки вида погасили стены, и пустой "
            + "кадр прошёл бы как «в проёме нет стены»");

        int wasLayer = body.gameObject.layer;
        body.gameObject.layer = FrameLayer;
        cam.cullingMask = 1 << FrameLayer;

        var placed = cam.transform.position;

        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();
        var pixels = tex.GetPixels32();

        Assert.AreEqual(RenderW, cam.pixelWidth,
            "камера кадра проецирует не в кадровую текстуру");
        Assert.Less((cam.transform.position - placed).magnitude, 1e-4f,
            "камеру кадра кто-то сдвинул за время съёмки — все патчи мимо");

        foreach (var patch in patches) Fill(patch, cam, pixels);

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("[WALLCAP] Saved: " + Path.GetFullPath(path));

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        body.gameObject.layer = wasLayer;
        cam.cullingMask = ~0;
    }

    private static void Fill(Patch patch, Camera cam, Color32[] frame)
    {
        var poly = new Vector2[4];
        var centre = Vector3.zero;
        foreach (var c in patch.Corners) centre += c;
        centre /= 4f;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            var world = Vector3.Lerp(centre, patch.Corners[i], PatchShrink);
            var v = cam.WorldToViewportPoint(world);
            Assert.Greater(v.z, 0f, patch.Name + ": угол патча позади камеры");
            Assert.IsTrue(v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f,
                patch.Name + ": угол патча вне кадра (" + v.x.ToString("0.00") + ", "
                + v.y.ToString("0.00") + ") — камера снимает не то место");
            poly[i] = new Vector2(v.x * RenderW, v.y * RenderH);
            minX = Mathf.Min(minX, poly[i].x); maxX = Mathf.Max(maxX, poly[i].x);
            minY = Mathf.Min(minY, poly[i].y); maxY = Mathf.Max(maxY, poly[i].y);
        }

        int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
        int x1 = Mathf.Min(RenderW - 1, Mathf.CeilToInt(maxX));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
        int y1 = Mathf.Min(RenderH - 1, Mathf.CeilToInt(maxY));

        int total = 0, filled = 0;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!Inside(poly, x + 0.5f, y + 0.5f)) continue;
                total++;
                if (Differs(frame[y * RenderW + x], Background)) filled++;
            }

        patch.Total = total;
        patch.Filled = filled;
    }

    /// <summary>Точка внутри выпуклого четырёхугольника: знак векторного
    /// произведения одинаков на всех четырёх рёбрах. Порядок обхода после
    /// проекции может смениться на обратный, поэтому знак не задан заранее, а
    /// берётся с первого ненулевого ребра.</summary>
    private static bool Inside(Vector2[] q, float x, float y)
    {
        int sign = 0;
        for (int i = 0; i < 4; i++)
        {
            var a = q[i];
            var b = q[(i + 1) % 4];
            float cross = (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
            int s = cross > 0f ? 1 : (cross < 0f ? -1 : 0);
            if (s == 0) continue;
            if (sign == 0) sign = s;
            else if (s != sign) return false;
        }
        return true;
    }

    private static void AssertOpen(Patch patch, string why)
    {
        AssertMeasurable(patch);
        Assert.LessOrEqual(patch.Share, MaxWallShareWhereItMustBeOpen,
            why + ". Пикселей стены в области «" + patch.Name + "»: "
            + patch.Filled + " из " + patch.Total + " ("
            + patch.Share.ToString("0.00") + "), допустимо не больше "
            + MaxWallShareWhereItMustBeOpen + ". Кадр рядом");
    }

    private static void AssertSolid(Patch patch, string why)
    {
        AssertMeasurable(patch);
        Assert.GreaterOrEqual(patch.Share, MinWallShareWhereItMustBeSolid,
            why + ". Пикселей стены в области «" + patch.Name + "»: "
            + patch.Filled + " из " + patch.Total + " ("
            + patch.Share.ToString("0.00") + "), нужно не меньше "
            + MinWallShareWhereItMustBeSolid + ". Кадр рядом");
    }

    private static void AssertMeasurable(Patch patch) =>
        Assert.Greater(patch.Total, MinPatchPixels,
            "область «" + patch.Name + "» заняла в кадре " + patch.Total
            + " пикселей — меньше " + MinPatchPixels + ": доля от такого "
            + "знаменателя ничего не значит, камера стоит слишком далеко или "
            + "смотрит вдоль поверхности");

    private static bool Differs(Color32 a, Color b)
    {
        var c = (Color32)b;
        return Mathf.Abs(a.r - c.r) > ColorDelta
            || Mathf.Abs(a.g - c.g) > ColorDelta
            || Mathf.Abs(a.b - c.b) > ColorDelta;
    }

    /// <summary>Прямоугольный кусок поверхности стены, заданный мировыми
    /// координатами, и результат его замера. Углы идут по контуру.</summary>
    private sealed class Patch
    {
        public readonly string Name;
        public readonly Vector3[] Corners;
        public int Total;
        public int Filled;

        public float Share => Total > 0 ? Filled / (float)Total : 0f;

        public Patch(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Name = name;
            Corners = new[] { a, b, c, d };
        }
    }
}
