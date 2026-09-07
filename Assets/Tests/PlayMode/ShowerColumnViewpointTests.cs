using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>
/// PlayMode: душевая стойка с ЧЕТЫРЁХ ракурсов плюс крупный план ручной лейки.
///
/// Зачем отдельный набор. Общий изометрический кадр у стойки один
/// (IsoScreenshotTests.IsoShowerColumn_Default), и он снят единственным
/// направлением IsoDir — 3/4 сверху-справа. Одного направления мало для
/// суждения о ФОРМЕ: угол наклона детали, лежащий в плоскости взгляда,
/// на таком кадре виден в проекции и читается то как 20°, то как 70°
/// в зависимости от того, что за ним. Кривизну, про которую спрашивает
/// пользователь, невозможно ни подтвердить, ни опровергнуть по одному
/// снимку — поэтому здесь их четыре, и каждый отвечает за свой класс
/// дефектов (см. сводки самих тестов).
///
/// Главное отличие от IsoScreenshotTests — здесь двигается КАМЕРА, а не
/// элемент. Там настенная арматура разворачивается на FrontTowardsCameraDeg
/// = 180°, потому что общая камера проекта стоит на -Z, а стойка садится
/// задней гранью на плоскость стены z = 0 и растёт в +Z: без разворота
/// объектив смотрит ей в затылок, и «не видно лица лейки» означает не
/// дефект модели, а дефект ракурса — целый круг правок уже ушёл в стол
/// именно так (agents/FLEET.md → «Judging a MODEL»). Разворачивать элемент
/// здесь нельзя: тогда «вид сбоку» показывал бы не бок, а имя теста врало
/// бы. Вместо этого все четыре камеры ставятся со стороны КОМНАТЫ, и связку
/// сторожит ShowerColumnViewpoints_StandOnTheRoomSide_AndDifferFromEachOther.
///
/// Кадры снимаются с cullingMask по приватному слою: стойка висит на высоте
/// 1,1 м, и вид сверху иначе снимался бы на фоне пола и плинтуса, где
/// силуэт диска тропического душа теряется. Изоляция решается на слое
/// объекта, а не выключением чужих рендереров: чужой Update возвращает
/// enabled обратно до кадра (agents/TEST-DESIGN.md).
/// </summary>
public class ShowerColumnViewpointTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;
    private const float Fov = 45f;

    /// <summary>Доля кадра по краям, внутрь которой обязан попасть габарит:
    /// снимок, обрезающий деталь, показывает половину предмета и молчит об
    /// этом.</summary>
    private const float FramePadding = 0.02f;

    /// <summary>Запас поверх точной посадки описанной сферы в конус обзора.
    /// Без запаса угол габарита садится ровно на границу кадра и проверка
    /// «влез» падает на ошибке округления, а не на дефекте.</summary>
    private const float FitMargin = 1.08f;

    /// <summary>Крупный план НЕ пользуется общей камерой IsoScreenshotTests:
    /// у той пол дистанции 0,5 м, и заказанные полторы длины рукоятки молча
    /// стали бы кадром в полметра шириной. Тот же множитель, что у крупных
    /// планов стойки в IsoScreenshotTests — кадры обязаны быть сравнимы.</summary>
    private const float CloseUpDistanceScale = 1.21f;

    /// <summary>Приватный слой съёмки: в проекте не занят ни один слой выше
    /// UI (ProjectSettings/TagManager.asset), и никакой код не фильтрует по
    /// слоям — брать 31 безопасно.</summary>
    private const int IsolationLayer = 31;

    /// <summary>Элемент в этих кадрах НЕ разворачивается: разворот — приём
    /// общей камеры, стоящей не с той стороны стены. Здесь с той.</summary>
    private const float ColumnYawDeg = 0f;

    /// <summary>Строго спереди, от комнаты к стене. Это единственный ракурс,
    /// на котором силуэт обязан быть симметричным относительно оси штанги, и
    /// единственный, на котором лицо ручной лейки видно как лицо, а не как
    /// эллипс.</summary>
    public static readonly Vector3 FrontDir = new Vector3(0f, 0f, 1f);

    /// <summary>Чистый профиль справа. Вся стойка построена в плоскости YZ
    /// (x = 0 у каждой детали), поэтому ИМЕННО здесь углы меряются без
    /// проекционного искажения: наклон рукоятки, касательная шланга, вылет
    /// гусака. Кадр, по которому спор о «повернуть на 90°» решается
    /// однозначно.</summary>
    public static readonly Vector3 SideDir = new Vector3(1f, 0f, 0f);

    /// <summary>Те же 3/4 сверху-справа, что у общей камеры проекта, но со
    /// стороны комнаты. Держит связь с существующими iso-кадрами: одна и та
    /// же модель на двух снимках должна читаться как одна.</summary>
    public static readonly Vector3 ThreeQuarterDir =
        new Vector3(0.5f, 0.5f, 0.866f).normalized;

    /// <summary>Сверху, с наклоном 75°, а не отвесно: у отвесного взгляда
    /// LookAt вырождается — up совпадает с направлением, и разворот кадра
    /// становится случайным.</summary>
    public static readonly Vector3 TopDir = new Vector3(0f, 0.966f, 0.259f).normalized;

    public static IEnumerable<Vector3> AllViewpoints =>
        new[] { FrontDir, SideDir, ThreeQuarterDir, TopDir };

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

    /// <summary>Сторож ракурсов. Держит ровно те факты, из-за которых
    /// прошлый круг кадров оказался бесполезен, и ещё один, без которого
    /// «набор ракурсов» перестаёт быть набором.
    ///
    /// Первое: стойка садится задней гранью на плоскость стены z = 0 и
    /// растёт в +Z, значит камера обязана стоять в +Z. Перенеси плоскость
    /// посадки или переверни любой из векторов — и снимки снова станут
    /// съёмкой затылка, на которой лица лейки нет вовсе.
    ///
    /// Второе: четыре ракурса обязаны РАЗЛИЧАТЬСЯ. Сдвинь их случайной
    /// правкой к одному направлению — и набор молча выродится в четыре
    /// копии одного кадра, каждая со своим именем.</summary>
    [Test]
    public void ShowerColumnViewpoints_StandOnTheRoomSide_AndDifferFromEachOther()
    {
        var bounds = ShowerColumnLayout.BoundsMM(ShowerColumnSpec.Default);

        Assert.AreEqual(0f, bounds.min.z, 1e-3f,
            "стойка — настенная арматура: задней гранью габарита она садится на плоскость "
            + "стены z = 0 и растёт в комнату, в +Z");
        Assert.Greater(ShowerColumnSpec.Default.ArmReachMM,
            ShowerColumnSpec.Default.WallOffsetMM,
            "и лицо её смотрит туда же: гусак уносит тропический душ дальше от стены, чем "
            + "стоит сама штанга");

        foreach (var dir in AllViewpoints)
        {
            Assert.AreEqual(1f, dir.magnitude, 1e-3f,
                "направление ракурса обязано быть единичным: дистанцию считает камера, и "
                + "длина вектора умножала бы её втихую — " + dir);
            Assert.GreaterOrEqual(dir.z, 0f,
                "камера обязана стоять со стороны КОМНАТЫ. Общая камера проекта стоит на "
                + "-Z, и оттуда настенная арматура снимается со спины: излива, рычага и "
                + "лица лейки на таком кадре нет вовсе, а выглядит это как дефект модели "
                + "(agents/FLEET.md). Здесь разворачивается камера, а не элемент — " + dir);
        }

        Assert.AreEqual(0f, ColumnYawDeg, 1e-3f,
            "и элемент при этом НЕ разворачивается: разворот на 180° — приём общей камеры, "
            + "стоящей не с той стороны стены. Развернуть здесь и элемент значило бы "
            + "снимать «вид сбоку», глядя стойке в бок с другой стороны, а «вид спереди» — "
            + "в спину");

        Assert.Greater(Vector3.Dot(FrontDir, Vector3.forward), 0.99f,
            "фронт обязан быть фронтом: только строго спереди силуэт стойки симметричен, и "
            + "асимметрия на этом кадре — дефект формы, а не ракурса");
        Assert.Less(Mathf.Abs(SideDir.z), 1e-3f,
            "профиль обязан быть чистым: вся стойка построена в плоскости YZ, и лишь при "
            + "нулевом z углы наклона видны без проекционного искажения");
        Assert.Less(Mathf.Abs(SideDir.y), 1e-3f, "и без подъёма камеры — иначе это 3/4");
        Assert.Greater(ThreeQuarterDir.x * ThreeQuarterDir.y * ThreeQuarterDir.z, 0f,
            "3/4 обязан смотреть сверху-сбоку-спереди: обнулись любая из трёх составляющих, "
            + "и он совпал бы с одним из ортогональных видов");
        Assert.Greater(Mathf.Asin(TopDir.y) * Mathf.Rad2Deg, 70f,
            "вид сверху обязан быть видом сверху, а не высоким 3/4");
        Assert.Greater(new Vector2(TopDir.x, TopDir.z).magnitude, 0.05f,
            "но не отвесным: у отвесного взгляда LookAt вырождается — up совпадает с "
            + "направлением, и поворот кадра вокруг оси становится случайным");

        var all = new List<Vector3>(AllViewpoints);
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                Assert.Greater(Vector3.Angle(all[i], all[j]), 30f,
                    "два ракурса из набора смотрят почти одинаково — набор выродился в "
                    + "копии одного кадра под разными именами: " + all[i] + " и " + all[j]);
    }

    /// <summary>Строго спереди. Отвечает на два вопроса, на которые не
    /// отвечает ни один другой кадр: симметричен ли силуэт относительно оси
    /// штанги (всё построено при x = 0, и любой перекос здесь — дефект
    /// формы, а не ракурса) и куда обращено ЛИЦО ручной лейки. Лейка,
    /// смотрящая на пользователя, даёт здесь круг; лейка, задранная вверх,
    /// даёт полоску торца.</summary>
    [UnityTest]
    public IEnumerator ShowerColumn_FromTheFront_ShowsTheFaceOfTheHandShower()
    {
        yield return RenderColumnFrom(FrontDir, "ColumnFront",
            "shower_column_front_face_of_hand_shower.png");
    }

    /// <summary>Чистый профиль. Кадр про УГЛЫ: наклон рукоятки лейки,
    /// касательная шланга там, где он входит в рукоятку, наклон гусака и
    /// вылет тропического душа. Вся стойка лежит в плоскости YZ, поэтому
    /// здесь эти углы видны в натуральную величину — на 3/4 любой из них
    /// читается искажённым, и спор «повернуть на 90 или нет» по такому кадру
    /// не решается.</summary>
    [UnityTest]
    public IEnumerator ShowerColumn_FromTheSide_ShowsTheAngleBetweenGripAndHose()
    {
        yield return RenderColumnFrom(SideDir, "ColumnSide",
            "shower_column_side_grip_vs_hose_angle.png");
    }

    /// <summary>Три четверти — тот же наклон, что у общей камеры проекта, но
    /// со стороны комнаты. Единственный кадр, на котором предмет читается как
    /// объём: держатель с чашкой, петля шланга и блок дивертора видны на
    /// своих местах друг относительно друга, а не наложенными.</summary>
    [UnityTest]
    public IEnumerator ShowerColumn_FromThreeQuarters_ShowsHolderAndHoseLoop()
    {
        yield return RenderColumnFrom(ThreeQuarterDir, "ColumnThreeQuarter",
            "shower_column_three_quarter_holder_and_hose_loop.png");
    }

    /// <summary>Сверху. Диск тропического душа Ø 250 мм здесь виден плашмя и
    /// обязан быть КРУГЛЫМ: гранёный многоугольник на этом кадре — дефект
    /// тесселяции трубы, а не формы, и вылечить его утолщением обода нельзя
    /// (agents/FLEET.md: 16 сегментов на Ø 250 — это 48-миллиметровые грани).
    /// Второй вопрос кадра — план: насколько гусак уносит диск от стены и
    /// стоит ли он на одной оси со штангой.</summary>
    [UnityTest]
    public IEnumerator ShowerColumn_FromAbove_ShowsTheRainHeadDisc()
    {
        yield return RenderColumnFrom(TopDir, "ColumnTop",
            "shower_column_top_rain_head_disc_is_round.png");
    }

    /// <summary>Крупный план узла держателя в чистом профиле. На общем виде
    /// полутораметровой стойки рукоятка лейки — это десяток пикселей, и угол
    /// между нею и шлангом там не измерить ничем. Кадр назван по детали,
    /// поэтому деталь обязана в него попасть целиком: низ рукоятки и лицо
    /// лейки проверяются на попадание в кадр ДО съёмки — иначе «не вижу
    /// лейку» опять означало бы что угодно.</summary>
    [UnityTest]
    public IEnumerator ShowerColumn_HandShowerFromTheSide_ShowsGripNeckAndHead()
    {
        var spec = ShowerColumnSpec.Default;
        var go = SpawnColumn(spec, "ColumnHandShowerCloseUp");
        yield return null;
        AssertColumnStayedWhereItWasPut(go, spec);

        float spanMM = ShowerColumnHandShower.GripLengthMM(spec) * 1.9f;
        var bounds = ShowerColumnLayout.BoundsMM(spec);
        var holderMM = ShowerColumnLayout.HolderCentreMM(spec);

        Assert.IsTrue(bounds.Contains(holderMM),
            $"точка наводки крупного плана — {holderMM} мм — вне габарита стойки "
            + $"{bounds.min}..{bounds.max} мм: камера смотрела бы в пустоту, а пустой кадр "
            + "сохраняется на диск как обычный и никого не настораживает");

        var centre = WorldOf(go, bounds, holderMM);
        float distance = spanMM * AppConstants.MM_TO_UNITS * CloseUpDistanceScale;
        var (camGo, cam) = CreateCamera(centre, SideDir, distance);

        AssertPointsAreInFrame(cam, new[]
        {
            ("низ рукоятки", WorldOf(go, bounds, ShowerColumnHandShower.GripBottomMM(spec))),
            ("лицо лейки", WorldOf(go, bounds, ShowerColumnHandShower.HeadFaceMM(spec))),
            ("чашка держателя", WorldOf(go, bounds, holderMM)),
        });

        yield return RenderToPng(cam, "shower_column_side_closeup_hand_shower_in_holder.png");

        Object.DestroyImmediate(camGo);
    }

    private IEnumerator RenderColumnFrom(Vector3 viewDir, string name, string png)
    {
        var spec = ShowerColumnSpec.Default;
        var go = SpawnColumn(spec, name);
        yield return null;
        AssertColumnStayedWhereItWasPut(go, spec);

        var bounds = RendererBoundsOf(go);
        Assert.Greater(bounds.size.magnitude, 0f,
            "у стойки нет ни одного рендерера: кадрировать нечего, а PNG всё равно "
            + "сохранился бы — пустым");

        float distance = bounds.extents.magnitude * FitMargin
            / Mathf.Sin(Fov * 0.5f * Mathf.Deg2Rad);
        var (camGo, cam) = CreateCamera(bounds.center, viewDir, distance);

        AssertBoundsFitInFrame(cam, bounds);

        yield return RenderToPng(cam, png);

        Object.DestroyImmediate(camGo);
    }

    /// <summary>Стена, на которой висит стойка. Её в кадре НЕТ — съёмка идёт
    /// по cullingMask приватного слоя, а стена остаётся на слое по умолчанию.
    /// Стоит она не ради вида, а ради валидации: без стены стойка висит в
    /// воздухе, ConstraintValidator не находит у неё ни одного контакта
    /// гранью с якорем сцены, выдаёт COL-02 «Деталь не имеет опоры», и
    /// ElementHighlighter красит её _invalidMaterial. Все пять кадров этого
    /// набора до правки сняты розовыми — то есть показывали не материал
    /// стойки, а тинт ошибки.
    ///
    /// Здесь стойка НЕ разворачивается (ColumnYawDeg = 0), поэтому задняя
    /// грань габарита смотрит в −Z, и стена уходит туда же — за спину камерам,
    /// которые все стоят со стороны комнаты.</summary>
    private const int WallWidthMM = 3000;
    private const int WallHeightMM = 2500;
    private const int WallThicknessMM = 100;

    private void SpawnWallBehindColumn(string name, Vector3Int dims, Vector3 pos)
    {
        KitchenSettings.Instance.NormalView.wallsEnabled = true;
        KitchenSettings.Instance.NormalView.lowerNearWalls = false;
        KitchenSettings.Instance.NormalView.lowerAllWalls = false;

        float standoff = AppConstants.HalfHeightUnits(dims.z)
            + AppConstants.HalfHeightUnits(WallThicknessMM);

        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(WallWidthMM, WallHeightMM, WallThicknessMM), name,
            new Vector3(pos.x, AppConstants.HalfHeightUnits(WallHeightMM), pos.z - standoff));
        _spawned.Add(wallGo);
    }

    private GameObject SpawnColumn(ShowerColumnSpec spec, string name)
    {
        var pos = new Vector3(0f,
            ShowerColumnLayout.CentreAboveFloorMM(spec) * AppConstants.MM_TO_UNITS, 0f);
        SpawnWallBehindColumn(name + "Wall", ShowerColumnLayout.DimensionsMM(spec), pos);
        var go = ElementFactory.CreateShowerColumn(spec, name, pos);
        _spawned.Add(go);
        go.transform.rotation = Quaternion.Euler(0f, ColumnYawDeg, 0f);
        SetLayerRecursively(go, IsolationLayer);

        var column = go.GetComponent<ShowerColumnElement>();
        Assert.IsNotNull(column, "фабрика обязана вернуть именно ShowerColumnElement");
        Assert.AreEqual(ShowerColumnLayout.DimensionsMM(spec), column!.DimensionsMM,
            "габарит стойки ВЫЧИСЛЯЕТСЯ из формы вместе с петлёй шланга: разойдись он с "
            + "раскладкой, камера кадрировала бы не то, что построено");
        return go;
    }

    /// <summary>Стойка — IWallMounted, и её Start зовёт SnapToWall. Стена в
    /// сцене теперь ЕСТЬ, и поставлена она ровно так, чтобы посадка ничего не
    /// сдвинула: передняя плоскость стены приходится на заднюю грань габарита,
    /// а разворот, который посадка задаёт сама, равен ColumnYawDeg. Проверка
    /// от этого не ослабла, а усилилась — раньше она сторожила пустоту, теперь
    /// сторожит арифметику расстояния до стены.
    ///
    /// Заодно спрашивается сама валидация, и спрашивается ТОТ ЖЕ источник, из
    /// которого ElementHighlighter берёт цвет: элемент в violations — значит на
    /// кадре не материал стойки, а розовый тинт ошибки.</summary>
    private static void AssertColumnStayedWhereItWasPut(GameObject go, ShowerColumnSpec spec)
    {
        var pos = new Vector3(0f,
            ShowerColumnLayout.CentreAboveFloorMM(spec) * AppConstants.MM_TO_UNITS, 0f);

        Assert.AreEqual(0f, (pos - go.transform.position).magnitude, 1e-3f,
            "стойка уехала между созданием и кадром: стена стоит не на том расстоянии, "
            + "и посадка сдвинула её после того, как камера наведена");
        Assert.AreEqual(ColumnYawDeg, go.transform.eulerAngles.y, 1e-2f,
            "стойку кто-то развернул между созданием и кадром — имена ракурсов после "
            + "этого врут: «вид сбоку» показывает не бок");
        Assert.AreEqual(Vector3.one, go.transform.localScale,
            "меш стойки строится в миллиметрах и живёт при единичном масштабе: любой "
            + "другой означал бы двойное масштабирование и кадр не по габариту");

        var element = go.GetComponent<KitchenElement>();
        var validation = ConstraintValidator.Validate(PartRegistry.GetAll());
        Assert.IsFalse(validation.violations.Contains(element),
            "стойка помечена нарушителем (COL-02 «Деталь не имеет опоры»): контакта "
            + "гранью со стеной нет, и кадр выйдет розовым — тинтом ошибки вместо "
            + "собственного материала");
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>Точка раскладки (миллиметры от плоскости стены и низа
    /// дивертора) в мир. Меш строится от ЦЕНТРА габарита, поэтому смещение
    /// считается от центра, а не от нуля раскладки.</summary>
    private static Vector3 WorldOf(GameObject go, Bounds boundsMM, Vector3 pointMM) =>
        go.transform.TransformPoint((pointMM - boundsMM.center) * AppConstants.MM_TO_UNITS);

    private (GameObject camGo, Camera cam) CreateCamera(Vector3 centre, Vector3 viewDir,
        float distance)
    {
        var camGo = new GameObject("ShowerColumnCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = Fov;
        // Кадр всегда 512×512, а не размер экрана батча: без явного aspect
        // WorldToViewportPoint считает по экрану, и проверка «влез в кадр»
        // мерила бы не тот кадр, который потом рисуется.
        cam.aspect = (float)RenderW / RenderH;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        cam.cullingMask = 1 << IsolationLayer;

        camGo.transform.position = centre + viewDir * distance;
        camGo.transform.LookAt(centre);
        _spawned.Add(camGo);

        return (camGo, cam);
    }

    private static Bounds RendererBoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void AssertBoundsFitInFrame(Camera cam, Bounds bounds)
    {
        var corners = new List<(string what, Vector3 world)>();
        for (int i = 0; i < 8; i++)
            corners.Add(("угол габарита " + i, new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                (i & 4) == 0 ? bounds.min.z : bounds.max.z)));

        AssertPointsAreInFrame(cam, corners);
    }

    private static void AssertPointsAreInFrame(Camera cam,
        IEnumerable<(string what, Vector3 world)> points)
    {
        var outside = new List<string>();
        foreach (var (what, world) in points)
        {
            var v = cam.WorldToViewportPoint(world);
            bool inFrame = v.z > 0f
                && v.x >= FramePadding && v.x <= 1f - FramePadding
                && v.y >= FramePadding && v.y <= 1f - FramePadding;
            if (!inFrame) outside.Add(what + " " + world + " -> " + v);
        }

        Assert.IsEmpty(outside,
            "кадр назван по тому, что обязан показывать, а показывает не всё: снимок "
            + "обрезает деталь и молчит об этом, а читающий кадр агент принимает «не вижу» "
            + "за дефект модели. Вне кадра:\n" + string.Join("\n", outside));
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

        Assert.IsTrue(File.Exists(path), $"PNG не создан: {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG пустой");
        Debug.Log($"[ISO] Saved: {path}");

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
