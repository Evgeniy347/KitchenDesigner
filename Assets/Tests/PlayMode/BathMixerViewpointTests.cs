using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Пять ракурсов смесителя — отдельный набор, а не ещё один кадр в
/// <c>IsoScreenshotTests</c>. Тот снимает ОДИН ракурс (3/4 справа-сверху) и
/// два крупных плана, и общего у него с этим набором только СТОРОНА съёмки:
/// настенная арматура садится задней гранью на z = 0 и растёт в +Z, туда же
/// смотрит её лицо, и туда же — на сторону лица — ставит объектив общий
/// <c>IsoCameraRig.ViewDir</c>. Сам прибор не разворачивается ни здесь, ни
/// там.
///
/// Зачем ракурсов пять. Дефект «деталь вывернута наизнанку» — это обход
/// треугольников против нормалей: отсечение задних граней выбрасывает
/// ближнюю к объективу стенку и рисует ДАЛЬНЮЮ, изнутри. На одном ракурсе
/// это неотличимо от «модель просто кривая»: силуэт остаётся тем же,
/// пропадают только перекрытия. Отличает их СМЕНА ракурса — деталь,
/// закрывающая соседку спереди, обязана уходить за неё сзади, а бочонок
/// корпуса обязан закрывать собой всё, что за ним. Один кадр этого вопроса
/// не задаёт; пять задают его пять раз с разных сторон.
///
/// Имена файлов названы по тому, что на кадре ОБЯЗАНО быть видно, а не по
/// номеру ракурса: «не вижу красной кнопки» на кадре с именем
/// …_red_button.png отделяет «кнопка плохо построена» от «кнопки нет в
/// кадре». Габарит в имя не идёт: он у смесителя ВЫЧИСЛЯЕТСЯ из формы, и
/// первая же правка носика переименовала бы все пять файлов.</summary>
public class BathMixerViewpointTests : ElementFrameTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;
    private const float Fov = 45f;
    private const float DistanceScale = 2.2f;
    private const float FramePadding = 0.02f;

    /// <summary>Направления заданы в СОБСТВЕННОЙ системе смесителя: +X —
    /// торец термостата, −X — торец вентиля, +Z — лицо, +Y — верх. Прибор
    /// больше не разворачивают лицом к объективу — объектив сам стоит со
    /// стороны лица (<c>IsoCameraRig.ViewDir</c>), — поэтому собственная
    /// система совпадает с мировой и пять векторов идут в мир как есть.</summary>
    private static readonly (string Png, Vector3 LocalDir, string Shows)[] Viewpoints =
    {
        ("iso_bath_mixer_front_spout_lever_and_scale_ring.png",
            new Vector3(0f, 0.25f, 1f),
            "лицо: излив вниз-вперёд, рычаг вентиля слева, кольцо шкалы справа"),
        ("iso_bath_mixer_end_thermostat_scale_ring_and_red_button.png",
            new Vector3(1f, 0.35f, 0.55f),
            "правый торец: термоголовка, кольцо шкалы на ней и красная кнопка сверху"),
        ("iso_bath_mixer_end_flow_lever_and_escutcheon_cup.png",
            new Vector3(-1f, 0.35f, 0.55f),
            "левый торец: рычаг расхода и чашка эксцентрика за ним"),
        ("iso_bath_mixer_top_centres_150_and_spout_reach.png",
            new Vector3(0f, 1f, 0.35f),
            "сверху: два эксцентрика на межосевом 150 и вылет излива вперёд"),
        ("iso_bath_mixer_below_hose_nipple_and_spout_underside.png",
            new Vector3(0f, -0.7f, 1f),
            "снизу: штуцер под душевой шланг Ø13 и низ излива"),
    };

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

    /// <summary>Сторож против случайного разворота: все пять ракурсов обязаны
    /// стоять со стороны ЛИЦА смесителя — то есть в +Z, куда смотрит
    /// <c>ElementFacing.LocalFront</c> и куда переехал объектив всей проектной
    /// изометрии.
    ///
    /// Тест краснеет с обеих сторон. Разверни один локальный вектор лицом
    /// внутрь стены — красное покажет именно на него, по имени PNG. Верни
    /// съёмщика на сырой <c>IsoDir</c> — красной станет последняя проверка:
    /// общая сторона съёмки разойдётся с лицом, и пять ракурсов этого набора
    /// окажутся с другой стороны, чем весь остальной проект.</summary>
    [Test]
    public void EveryViewpoint_StandsOnTheFaceSideOfTheMixer_NotBehindTheWall()
    {
        var spec = BathMixerSpec.Default;

        Assert.AreEqual(0f, BathMixerLayout.BoundsMM(spec).min.z, 1e-3f,
            "смеситель садится на стену задней гранью габарита, плоскостью z = 0: "
            + "именно из этого следует, что без разворота объектив смотрит ему в затылок");
        Assert.Greater(BathMixerOutlets.SpoutMouthMM(spec).z,
            BathMixerLayout.BodyAxisZMM(spec),
            "а лицо у него в +Z — туда уходит излив");

        foreach (var (png, localDir, shows) in Viewpoints)
        {
            Assert.Greater(localDir.z, 0f,
                png + " обязан стоять со стороны лица (+Z в собственной системе), "
                + "иначе кадр показывает " + shows + " сквозь корпус");
        }

        Assert.Greater(Vector3.Dot(ElementFacing.LocalFront, IsoCameraRig.ViewDir), 0f,
            "и общий съёмщик проекта стоит с ТОЙ ЖЕ стороны: пять ракурсов этого набора "
            + "и один ракурс IsoScreenshotTests обязаны смотреть на прибор с одной "
            + "стороны, иначе «не вижу излива» на одном кадре ничего не говорит о другом");
    }

    /// <summary>Пять ракурсов обязаны быть пятью РАЗНЫМИ ракурсами. Опечатка
    /// в одном векторе даёт два одинаковых кадра под разными именами, и
    /// набор молча теряет ту самую смену ракурса, ради которой он написан.</summary>
    [Test]
    public void TheViewpoints_LookFromGenuinelyDifferentDirections()
    {
        for (int i = 0; i < Viewpoints.Length; i++)
            for (int j = i + 1; j < Viewpoints.Length; j++)
                Assert.Less(
                    Vector3.Dot(Viewpoints[i].LocalDir.normalized,
                        Viewpoints[j].LocalDir.normalized), 0.9f,
                    Viewpoints[i].Png + " и " + Viewpoints[j].Png
                    + " смотрят почти из одной точки: перекрытия на них совпадут, и "
                    + "смены ракурса, которая только и отличает выворот от кривой "
                    + "геометрии, не будет");
    }

    [UnityTest]
    public IEnumerator IsoBathMixer_Front_SpoutLeverAndScaleRing() =>
        RenderViewpoint(0);

    [UnityTest]
    public IEnumerator IsoBathMixer_ThermostatEnd_ScaleRingAndRedButton() =>
        RenderViewpoint(1);

    [UnityTest]
    public IEnumerator IsoBathMixer_FlowEnd_LeverAndEscutcheonCup() =>
        RenderViewpoint(2);

    [UnityTest]
    public IEnumerator IsoBathMixer_FromAbove_CentresAndSpoutReach() =>
        RenderViewpoint(3);

    [UnityTest]
    public IEnumerator IsoBathMixer_FromBelow_HoseNippleAndSpoutUnderside() =>
        RenderViewpoint(4);

    /// <summary>Стена, на которой висит смеситель. Без неё прибор висит в
    /// воздухе — и это не фигура речи, а ровно то, что говорит ядро:
    /// ConstraintValidator не находит у него ни одного контакта гранью с
    /// якорем сцены и выдаёт COL-02 «Деталь не имеет опоры», после чего
    /// ElementHighlighter подмешивает ему тон нарушения. Все пять кадров этого
    /// набора до правки сняты розовыми — то есть показывали не хром, а тинт
    /// ошибки.
    ///
    /// Стена ставится так, чтобы посадка (Start → SnapToWall) НЕ двигала
    /// прибор: её передняя плоскость приходится ровно на заднюю грань его
    /// габарита. Тогда позиция остаётся заявленной, а разворот, который
    /// посадка задаёт сама, оставляет лицо в +Z — и проверка
    /// ниже впервые становится проверкой, а не тавтологией про пустую
    /// сцену.</summary>
    private const int WallWidthMM = 3000;
    private const int WallHeightMM = 2500;
    private const int WallThicknessMM = 100;

    private void SpawnWallBehind(string name, Vector3Int fittingDims, Vector3 fittingPos)
    {
        KitchenSettings.Instance.NormalView.wallsEnabled = true;
        KitchenSettings.Instance.NormalView.lowerNearWalls = false;
        KitchenSettings.Instance.NormalView.lowerAllWalls = false;

        float standoff = AppConstants.HalfHeightUnits(fittingDims.z)
            + AppConstants.HalfHeightUnits(WallThicknessMM);

        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(WallWidthMM, WallHeightMM, WallThicknessMM), name,
            new Vector3(fittingPos.x, AppConstants.HalfHeightUnits(WallHeightMM),
                fittingPos.z - standoff));
        _spawned.Add(wallGo);
    }

    private IEnumerator RenderViewpoint(int index)
    {
        var (png, localDir, shows) = Viewpoints[index];
        var spec = BathMixerSpec.Default;
        var dims = BathMixerLayout.DimensionsMM(spec);
        var pos = new Vector3(0f,
            BathMixerLayout.CentreAboveFloorMM(spec) * AppConstants.MM_TO_UNITS, 0f);

        SpawnWallBehind("ViewpointWall" + index, dims, pos);

        var go = ElementFactory.CreateBathMixer(spec, "Viewpoint" + index, pos);
        _spawned.Add(go);

        // Меш строится в ApplyDimensions, а Start сажает прибор на ближайшую
        // стену — до этого кадра ни того, ни другого на сцене нет.
        yield return null;

        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, go.transform.rotation), 0.05f,
            "посадка на стену развернула прибор: стена стоит ПОЗАДИ его лица, в −Z, и "
            + "SnapToWall обязан оставить лицо в +Z. Развернулся — значит кадр " + png
            + " снимает затылок");
        Assert.AreEqual(0f, (pos - go.transform.position).magnitude, 1e-4f,
            "посадка сдвинула смеситель: стена стоит не на том расстоянии, и кадр "
            + png + " снят не там, где заявлено");

        // «Прибор не в списке нарушителей» здесь больше не спрашивается: тот же
        // вопрос задаёт съёмка перед каждым кадром без исключения — см.
        // ElementFrameTests.CaptureFramePng. Двух формулировок одного вопроса в
        // проекте было две, и это ровно та пара, которую свели в один хелпер.
        var bounds = RendererBoundsOf(go);
        var worldDir = localDir.normalized;
        float distance = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
            * DistanceScale;

        var camGo = new GameObject("ViewpointCam");
        _spawned.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = Fov;
        cam.aspect = (float)RenderW / RenderH;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        var station = bounds.center + worldDir * distance;
        camGo.transform.position = station;
        camGo.transform.LookAt(bounds.center);

        AssertFitsInFrame(cam, bounds, png, shows);

        yield return CaptureFramePng(cam, png, RenderW, RenderH);

        Assert.AreEqual(0f, (station - camGo.transform.position).magnitude, 1e-4f,
            "камера уехала за время съёмки: у Camera.main в этой сцене висит "
            + "CameraController, и он раскладывает по орбите ЛЮБУЮ камеру, до которой "
            + "дотянулся — тогда кадр " + png + " снят не оттуда, откуда заявлено");

        Object.DestroyImmediate(camGo);
    }

    private static Bounds RendererBoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers,
            "у смесителя нет ни одного рендерера: снимок вышел бы пустым кадром, а тест "
            + "зелёным — " + go.name);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void AssertFitsInFrame(Camera cam, Bounds bounds, string png, string shows)
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
            "смеситель не влез в кадр " + png + ", а на нём обязано быть видно: " + shows
            + ". Обрезанный кадр читается как дефект модели — ровно то, ради чего "
            + "проверка углов и стоит ДО съёмки. Углы вне кадра:\n"
            + string.Join("\n", outside));
    }

}
