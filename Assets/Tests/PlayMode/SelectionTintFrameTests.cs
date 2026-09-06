using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Tests;

/// <summary>PlayMode: выделение и декор составного элемента, снятые камерой.
///
/// Зачем кадр, если тонировку уже стережёт EditMode. Тот сторож спрашивает
/// «поменялся ли материал на каждом рендерере» — вопрос про ссылки, и он
/// зелен даже если элемент покрашен в цвет, которого никто не увидит, или
/// если жёлтой стала одна деталь из десяти, а остальные просто получили
/// ДРУГОЙ материал. Здесь вопрос задаётся глазами камеры: сколько пикселей
/// силуэта элемента изменилось от выделения.
///
/// Силуэт не выписан числом и не взят из эталона: это те пиксели кадра «до»,
/// которые отличаются от фона камеры. Эталонный PNG тоже не нужен — сравнение
/// идёт между двумя кадрами ОДНОЙ сцены с одной камеры, поэтому тест не
/// придётся принимать заново после каждой правки освещения. PNG всё равно
/// пишется: по нему видно то, чего не увидит ни одна доля пикселей — какая
/// именно деталь осталась серой.
///
/// В кадре ОДИН элемент и больше ничего. Первая версия снимала сцену как
/// есть и звала «силуэтом» всё, что отличается от фона камеры, — а Bootstrap
/// строит пол, и пол занимал три четверти кадра. Дверь желтела ЦЕЛИКОМ
/// (42029 изменившихся пикселей из ~42400, которые она занимает), а тест
/// печатал «0,23 из 0,9» и обвинял подсветку: 140 тысяч пикселей пола честно
/// не менялись и честно считались частью двери. Порог был ни при чём —
/// сторож мерил не ту вещь.
///
/// ЧЕМ элемент отделяется от сцены. Вторая версия перед каждым снимком гасила
/// каждый посторонний <c>Renderer</c> — и не гасила НИЧЕГО: кадр «со сценой»
/// и кадр «после гашения» вышли побайтно одинаковыми, с полом и с гизмо.
/// Список рендереров тут ни при чём, промахнулся сам способ.
/// <c>CameraController.Update</c> каждый кадр зовёт
/// <c>UpdateFloorVisibility</c>, а та в <c>ApplyFloorCameraHide</c> пишет
/// полу, найденному по тегу Floor, <c>renderer.enabled = rendererVisible</c>
/// (CameraController.cs:317): наш <c>false</c> жил доли кадра и возвращался в
/// <c>true</c> раньше, чем камера снимала. Гизмо утекали по второй причине:
/// список посторонних снимался ОДИН раз, а ручки строятся в Update следующего
/// кадра — то есть уже после того, как их «погасили».
///
/// Поэтому изоляция теперь не гасит чужое, а показывает только своё: тело
/// подопытного уезжает на отдельный слой <c>FrameLayer</c>, а камера кадра
/// получает <c>cullingMask</c> ровно из этого слоя. Решение принимается в
/// момент ОТРИСОВКИ и по слою объекта, поэтому его нельзя ни отменить чужим
/// Update, ни обойти, родившись позже снимка списка. Отобрать слой некому:
/// во всём Assets/Scripts нет ни одного присваивания <c>.layer</c> или
/// <c>cullingMask</c>. Единственная дыра маски — рисование через <c>GL</c>
/// из <c>OnRenderObject</c>: так рисует <c>SpatialGridRenderer</c>, и слой
/// ему не указ, поэтому пространственная сетка на время кадров выключается
/// настройкой; влезь в кадр всё-таки чужая линия — её поймает рамка
/// габаритов.
///
/// Само это правило сторожа стережёт
/// <c>SilhouetteIsTheElementAlone_OrTheShareWouldCountTheFloor</c>: он держит
/// обе половины — в сцене ЕСТЬ что отсекать, и после отсечения силуэт
/// помещается в экранную проекцию габаритов элемента. Вернись в кадр пол — он
/// вылезет за эту рамку, и красным станет измерение, а не подсветка
/// (CONVENTIONS.md → «Prove the harness before you trust what it measures»).</summary>
public class SelectionTintFrameTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;

    /// <summary>Слой, на котором в кадре стоит подопытный, и только он.
    /// Тридцать первый: в TagManager названы Default, TransparentFX,
    /// Ignore Raycast, Water и UI — всё, что старше пятого, свободно, а
    /// продуктовый код слои не назначает вообще.</summary>
    private const int FrameLayer = 31;

    /// <summary>Выделение обязано накрыть элемент ЦЕЛИКОМ. Порог не подобран
    /// под текущую картинку: до правки у стула желтело только сиденье (около
    /// половины силуэта), а у двери — ничего, ноль. Девять десятых отделяет
    /// «покрашено всё» от любого из этих двух исходов с запасом на сглаженный
    /// край силуэта, который камера рисует наполовину фоном.</summary>
    private const float MinChangedShareOfSilhouette = 0.9f;

    /// <summary>Доля силуэта, которой позволено оказаться вне экранной рамки
    /// габаритов: проекция выпуклой коробки ограничивает проекцию элемента
    /// точно, но мягкая тень и краевой пиксель дают единицы промахов. Пол,
    /// вернувшийся в кадр, дал бы здесь больше половины.</summary>
    private const float MaxShareOutsideBounds = 0.01f;

    private const int ColorDelta = 12;

    private static readonly Vector3 IsoDir = new Vector3(0.5f, 0.5f, -0.866f).normalized;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Color32[]? _lastPixels;
    private bool _tintWas;
    private bool _gridWas;

    /// <summary>Валидационный тон выключен на время кадра. Он глобальный и
    /// красит КАЖДЫЙ элемент без своего декора — то есть и наши, ещё до того
    /// как их выделят: кадр «до» вышел бы уже перекрашенным, а рендереры,
    /// носившие фабричный декор, перестали бы его носить, и тест про декор
    /// проверял бы пустой список. Пространственная сетка выключена по другой
    /// причине: она рисуется через GL из OnRenderObject, а такое рисование
    /// маску камеры не спрашивает и в изолированный кадр пролезло бы линиями.
    /// Обе настройки возвращаются как были в TearDown.</summary>
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        _tintWas = ElementHighlighter.TintEnabled;
        ElementHighlighter.TintEnabled = false;
        _gridWas = KitchenSettings.Instance.SpatialGrid;
        KitchenSettings.Instance.SpatialGrid = false;

        _mainCamera = new GameObject("Main Camera");
        _mainCamera.tag = "MainCamera";
        _mainCamera.AddComponent<Camera>();
        _mainCamera.transform.position = new Vector3(0f, 3f, -5f);
        _mainCamera.transform.LookAt(Vector3.zero);

        SaveLoadManager.LastPath = "";

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (SelectionManager.Instance != null) SelectionManager.Instance.DeselectAll();
        ElementHighlighter.TintEnabled = _tintWas;
        KitchenSettings.Instance.SpatialGrid = _gridWas;

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

    [UnityTest]
    public IEnumerator SelectedChair_GoesYellowOverItsWholeSilhouette_LegsAndBackIncluded()
    {
        var go = ElementFactory.CreateChair(new Vector3Int(450, 900, 450), 25, 460,
            "Кадр-стул", Vector3.zero);
        _spawned.Add(go);
        var chair = go.GetComponent<KitchenElement>();
        yield return null;

        var cam = AimAt(chair);
        yield return Shoot(cam, chair, "tint_chair_plain.png");
        var plain = _lastPixels!;

        Selection().Select(chair);
        yield return Shoot(cam, chair, "tint_chair_selected.png");

        AssertSilhouetteChanged(plain, _lastPixels!, cam, chair,
            "стул: жёлтыми обязаны стать сиденье, спинка и все четыре ножки — "
            + "они отдельные объекты со своими MeshRenderer");
    }

    [UnityTest]
    public IEnumerator SelectedDoor_GoesYellowOverItsWholeSilhouette_ThoughItHasNoRootMesh()
    {
        var go = ElementFactory.CreateDoor(new Vector3Int(900, 2000, 100),
            "Кадр-дверь", Vector3.zero, DoorSashType.Blind);
        _spawned.Add(go);
        var door = go.GetComponent<KitchenElement>();
        yield return null;

        var cam = AimAt(door);
        yield return Shoot(cam, door, "tint_door_plain.png");
        var plain = _lastPixels!;

        Selection().Select(door);
        yield return Shoot(cam, door, "tint_door_selected.png");

        AssertSilhouetteChanged(plain, _lastPixels!, cam, door,
            "дверь: меша на корне у неё нет вовсе, поэтому она не желтела НИКАК — "
            + "полотно, коробка и оба наличника обязаны попасть под подсветку");
    }

    /// <summary>Сторож измерения, а не подсветки. Доля «изменившихся пикселей
    /// силуэта» осмысленна ровно настолько, насколько силуэт — это элемент.
    ///
    /// Тест снимает один и тот же кадр дважды — со сценой и без неё — и держит
    /// обе половины правила. Первая: в сцене ЕСТЬ что отсекать, иначе изоляция
    /// ничего не делает и рамка ниже проходит сама собой (в кадре по-прежнему
    /// стоит пол Bootstrap, и он занимает больше самого элемента). Вторая:
    /// после изоляции силуэт помещается в экранную проекцию габаритов элемента.
    /// Ровно этот тест поймал версию, где посторонние рендереры гасились по
    /// одному: оба кадра вышли побайтно равными, потому что CameraController
    /// возвращал полу enabled быстрее, чем камера снимала. Пропусти изоляцию —
    /// и первый кадр показывает, сколько чужого попадало в знаменатель: у
    /// двери 183410 пикселей «силуэта» против ~42000 её собственных, отчего
    /// целиком жёлтая дверь и печаталась как 0,23.</summary>
    [UnityTest]
    public IEnumerator SilhouetteIsTheElementAlone_OrTheShareWouldCountTheFloor()
    {
        var go = ElementFactory.CreateDoor(new Vector3Int(900, 2000, 100),
            "Кадр-дверь-рамка", Vector3.zero, DoorSashType.Blind);
        _spawned.Add(go);
        var door = go.GetComponent<KitchenElement>();
        yield return null;

        var cam = AimAt(door);

        yield return ShootWholeScene(cam, "tint_door_scene.png");
        int withScenery = CountSilhouette(_lastPixels!, cam);

        yield return Shoot(cam, door, "tint_door_isolated.png");
        int elementOnly = CountSilhouette(_lastPixels!, cam);

        Assert.Greater(withScenery, elementOnly * 2,
            "в кадре нет посторонней геометрии (" + withScenery + " против "
            + elementOnly + " пикселей): отсекать нечего, и рамка ниже пройдёт "
            + "на любом коде — сторож проверяет пустоту");

        AssertSilhouetteInsideBounds(_lastPixels!, cam, door,
            "после изоляции в силуэте обязан остаться только элемент");
    }

    /// <summary>Кадр про декор, а не про подсветку: тот же обход тела решает,
    /// докуда доходит выбранная текстура. Судим по рендерерам, носившим СТАРЫЙ
    /// декор: стекло и хром в декоре не участвуют и в счёт не идут.</summary>
    [UnityTest]
    public IEnumerator DecorOnADoor_ReachesEveryCasing_NotJustTheFirstOne()
    {
        var go = ElementFactory.CreateDoor(new Vector3Int(900, 2000, 100),
            "Кадр-дверь-декор", Vector3.zero, DoorSashType.Blind);
        _spawned.Add(go);
        var door = go.GetComponent<KitchenElement>();
        yield return null;

        var oldDecor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(door.MaterialId));
        var wearing = new List<MeshRenderer>();
        foreach (var renderer in ElementRenderers.BodyOf(door))
            if (oldDecor != null && ReferenceEquals(renderer.sharedMaterial, oldDecor))
                wearing.Add(renderer);

        Assert.Greater(wearing.Count, 1,
            "у двери декор носит не один меш; если их стало меньше двух — "
            + "проверять нечего, и кадр ничего не покажет");

        var cam = AimAt(door);
        yield return Shoot(cam, door, "tint_door_decor_before.png");

        var def = MaterialCatalog.Get("oak");
        MaterialManager.Apply(door, def);
        var newDecor = MaterialManager.GetSharedMaterial(def);
        yield return Shoot(cam, door, "tint_door_decor.png");

        var missed = new List<string>();
        foreach (var renderer in wearing)
            if (!ReferenceEquals(renderer.sharedMaterial, newDecor))
                missed.Add(ElementRenderers.PathOf(door, renderer));

        Assert.IsEmpty(missed,
            "декор не дошёл до " + missed.Count + " из " + wearing.Count
            + " носивших его мешей: " + string.Join(", ", missed));
    }

    private static SelectionManager Selection()
    {
        var sm = SelectionManager.Instance;
        if (sm != null) return sm;
        return new GameObject("SelectionManager кадра").AddComponent<SelectionManager>();
    }

    private static Bounds BodyBounds(KitchenElement element)
    {
        var body = ElementRenderers.BodyOf(element);
        Assert.Greater(body.Count, 0, "элемент без единого рендерера в кадр не попадёт");

        var bounds = body[0].bounds;
        for (int i = 1; i < body.Count; i++) bounds.Encapsulate(body[i].bounds);
        return bounds;
    }

    private Camera AimAt(KitchenElement element)
    {
        var bounds = BodyBounds(element);

        var camGo = new GameObject("FrameCam");
        _spawned.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        cam.fieldOfView = 45f;
        cam.aspect = (float)RenderW / RenderH;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        camGo.transform.position = bounds.center + IsoDir * Mathf.Max(maxDim * 1.6f, 0.5f);
        camGo.transform.LookAt(bounds.center);
        return cam;
    }

    /// <summary>Уводит тело элемента на кадровый слой и возвращает прежние
    /// слои, чтобы поставить обратно. Обход тела берётся из боевого
    /// <c>ElementRenderers.BodyOf</c>: своё второе описание «что такое элемент»
    /// сошлось бы само с собой. Ручки гизмо — объекты КОРНЯ сцены, а не дети
    /// элемента, поэтому в тело не попадают и на кадровый слой не едут.</summary>
    private static List<KeyValuePair<Transform, int>> PutOnFrameLayer(KitchenElement subject)
    {
        var moved = new List<KeyValuePair<Transform, int>>();
        foreach (var mr in ElementRenderers.BodyOf(subject))
        {
            if (mr == null) continue;
            var node = mr.transform;
            moved.Add(new KeyValuePair<Transform, int>(node, node.gameObject.layer));
            node.gameObject.layer = FrameLayer;
        }

        Assert.IsNotEmpty(moved,
            "на кадровый слой не уехало ни одного меша: снимать нечего, "
            + "и любая доля силуэта считалась бы от пустого кадра");
        return moved;
    }

    /// <summary>Снимок ОДНОГО элемента: тело на кадровом слое, маска камеры —
    /// ровно этот слой. Маска решает на каждой отрисовке, поэтому её не
    /// отменит чужой Update и не обойдёт объект, родившийся между снимком
    /// списка и кадром, — на этих двух вещах сломалось поштучное гашение
    /// рендереров.</summary>
    private IEnumerator Shoot(Camera cam, KitchenElement subject, string fileName)
    {
        var moved = PutOnFrameLayer(subject);
        cam.cullingMask = 1 << FrameLayer;

        yield return Capture(cam, fileName);

        cam.cullingMask = ~0;
        foreach (var was in moved)
            if (was.Key != null) was.Key.gameObject.layer = was.Value;
    }

    private IEnumerator ShootWholeScene(Camera cam, string fileName)
    {
        cam.cullingMask = ~0;
        yield return Capture(cam, fileName);
    }

    private static int CountSilhouette(Color32[] frame, Camera cam)
    {
        Color32 background = cam.backgroundColor;
        int silhouette = 0;
        for (int i = 0; i < frame.Length; i++)
            if (Differs(frame[i], background)) silhouette++;
        return silhouette;
    }

    private IEnumerator Capture(Camera cam, string fileName)
    {
        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();
        _lastPixels = tex.GetPixels32();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("[TINT] Saved: " + Path.GetFullPath(path));

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    private static void AssertSilhouetteChanged(Color32[] before, Color32[] after,
        Camera cam, KitchenElement subject, string what)
    {
        Color32 background = cam.backgroundColor;
        int silhouette = 0;
        int changed = 0;

        for (int i = 0; i < before.Length; i++)
        {
            if (!Differs(before[i], background)) continue;
            silhouette++;
            if (Differs(before[i], after[i])) changed++;
        }

        Assert.Greater(silhouette, RenderW * RenderH / 20,
            "элемент занял меньше 5% кадра — камера смотрит мимо, и доля "
            + "перекрашенного ничего не значит (" + what + ")");

        AssertSilhouetteInsideBounds(before, cam, subject,
            "знаменатель доли обязан быть элементом и только им (" + what + ")");

        float share = changed / (float)silhouette;
        Assert.GreaterOrEqual(share, MinChangedShareOfSilhouette,
            what + ". Изменилось " + changed + " из " + silhouette
            + " пикселей силуэта (" + share.ToString("0.00") + "), нужно "
            + MinChangedShareOfSilhouette + ". Серую часть видно на PNG рядом");
    }

    private static void AssertSilhouetteInsideBounds(Color32[] frame, Camera cam,
        KitchenElement subject, string why)
    {
        var bounds = BodyBounds(subject);
        ScreenRectOf(cam, bounds, out int x0, out int y0, out int x1, out int y1);

        Color32 background = cam.backgroundColor;
        int silhouette = 0;
        int outside = 0;

        for (int i = 0; i < frame.Length; i++)
        {
            if (!Differs(frame[i], background)) continue;
            silhouette++;
            int x = i % RenderW;
            int y = i / RenderW;
            if (x < x0 || x > x1 || y < y0 || y > y1) outside++;
        }

        Assert.Greater(silhouette, 0, "в кадре не оказалось ни одного пикселя элемента");

        float share = outside / (float)silhouette;
        Assert.LessOrEqual(share, MaxShareOutsideBounds,
            why + ". Вне экранной рамки габаритов элемента (" + x0 + ".." + x1
            + " x " + y0 + ".." + y1 + ") оказалось " + outside + " из " + silhouette
            + " пикселей силуэта (" + share.ToString("0.00")
            + "): в силуэт попало то, что элементом не является");
    }

    /// <summary>Экранная рамка габаритов. Проекция выпуклой коробки — это
    /// выпуклая оболочка проекций её восьми углов, поэтому рамка по углам
    /// ограничивает элемент точно; запас в два пикселя оставлен на округление
    /// вьюпорта в целые.</summary>
    private static void ScreenRectOf(Camera cam, Bounds bounds,
        out int x0, out int y0, out int x1, out int y1)
    {
        const int Slack = 2;
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int corner = 0; corner < 8; corner++)
        {
            var p = new Vector3(
                (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
            var v = cam.WorldToViewportPoint(p);
            Assert.Greater(v.z, 0f,
                "угол габаритов оказался позади камеры — рамка бессмысленна");
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
        }

        x0 = Mathf.FloorToInt(minX * RenderW) - Slack;
        x1 = Mathf.CeilToInt(maxX * RenderW) + Slack;
        y0 = Mathf.FloorToInt(minY * RenderH) - Slack;
        y1 = Mathf.CeilToInt(maxY * RenderH) + Slack;
    }

    private static bool Differs(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) > ColorDelta
        || Mathf.Abs(a.g - b.g) > ColorDelta
        || Mathf.Abs(a.b - b.b) > ColorDelta;
}
