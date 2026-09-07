using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Пять ракурсов смесителя — отдельный набор, а не ещё один кадр в
/// <c>IsoScreenshotTests</c>. Тот снимает ОДИН ракурс (3/4 справа-сверху) и
/// два крупных плана, и общего у него с этим набором только направление
/// разворота: настенная арматура садится задней гранью на z = 0 и растёт в
/// +Z, а объектив проекта стоит на −Z, поэтому лицом к камере она
/// поворачивается на 180°.
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
public class BathMixerViewpointTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;
    private const float Fov = 45f;
    private const float DistanceScale = 2.2f;
    private const float FramePadding = 0.02f;

    /// <summary>Разворот арматуры лицом к объективу — та же константа, что в
    /// <c>IsoScreenshotTests</c>, и по той же причине. Здесь она пересчитана
    /// заново, а не позаимствована: два набора не имеют права молча
    /// разъехаться, поэтому её держит собственный тест ниже.</summary>
    private const float FrontTowardsCameraDeg = 180f;

    private static Quaternion TurnedToCamera =>
        Quaternion.Euler(0f, FrontTowardsCameraDeg, 0f);

    /// <summary>Направления заданы в СОБСТВЕННОЙ системе смесителя: +X —
    /// торец термостата, −X — торец вентиля, +Z — лицо, +Y — верх. В мир они
    /// переводятся разворотом, поэтому смена FrontTowardsCameraDeg не
    /// требует переписывать пять векторов.</summary>
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
    /// стоять со стороны ЛИЦА смесителя, а лицо после разворота смотрит в −Z
    /// мира — туда же, куда смотрит объектив у всей проектной изометрии.
    ///
    /// Тест краснеет с обеих сторон. Убери разворот — и каждый мировой
    /// вектор сменит знак z, то есть все пять кадров станут снимками
    /// затылка. Разверни один локальный вектор лицом внутрь стены — красное
    /// покажет именно на него, по имени PNG.</summary>
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
            Assert.Less((TurnedToCamera * localDir).z, 0f,
                png + ": после разворота на " + FrontTowardsCameraDeg + "° камера обязана "
                + "оказаться на −Z мира — там, где стоит вся изометрия проекта");
        }
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

    private IEnumerator RenderViewpoint(int index)
    {
        var (png, localDir, shows) = Viewpoints[index];
        var spec = BathMixerSpec.Default;
        var pos = new Vector3(0f,
            BathMixerLayout.CentreAboveFloorMM(spec) * AppConstants.MM_TO_UNITS, 0f);

        var go = ElementFactory.CreateBathMixer(spec, "Viewpoint" + index, pos);
        _spawned.Add(go);
        go.transform.rotation = TurnedToCamera;

        // Меш строится в ApplyDimensions, а Start ещё и сажает элемент на
        // ближайшую стену — до кадра ни того, ни другого на сцене нет.
        yield return null;

        Assert.AreEqual(0f, Quaternion.Angle(TurnedToCamera, go.transform.rotation), 0.05f,
            "элемент развернулся сам: посадка на стену перебила разворот к камере, и "
            + "кадр " + png + " снимает затылок");

        var bounds = RendererBoundsOf(go);
        var worldDir = (TurnedToCamera * localDir).normalized;
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

        yield return RenderToPng(cam, png);

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

    private static IEnumerator RenderToPng(Camera cam, string png)
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
        string path = Path.Combine(dir, png);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(path), "PNG не создан: " + path);
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG пустой: " + path);
        Debug.Log("[ISO] Saved: " + path);

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
