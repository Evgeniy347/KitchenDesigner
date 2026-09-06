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
/// именно деталь осталась серой.</summary>
public class SelectionTintFrameTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;

    /// <summary>Выделение обязано накрыть элемент ЦЕЛИКОМ. Порог не подобран
    /// под текущую картинку: до правки у стула желтело только сиденье (около
    /// половины силуэта), а у двери — ничего, ноль. Девять десятых отделяет
    /// «покрашено всё» от любого из этих двух исходов с запасом на сглаженный
    /// край силуэта, который камера рисует наполовину фоном.</summary>
    private const float MinChangedShareOfSilhouette = 0.9f;

    private const int ColorDelta = 12;

    private static readonly Vector3 IsoDir = new Vector3(0.5f, 0.5f, -0.866f).normalized;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Color32[]? _lastPixels;
    private bool _tintWas;

    /// <summary>Валидационный тон выключен на время кадра. Он глобальный и
    /// красит КАЖДЫЙ элемент без своего декора — то есть и наши, ещё до того
    /// как их выделят: кадр «до» вышел бы уже перекрашенным, а рендереры,
    /// носившие фабричный декор, перестали бы его носить, и тест про декор
    /// проверял бы пустой список. Возвращаем как было в TearDown.</summary>
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        _tintWas = ElementHighlighter.TintEnabled;
        ElementHighlighter.TintEnabled = false;

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
        yield return Shoot(cam, "tint_chair_plain.png");
        var plain = _lastPixels!;

        Selection().Select(chair);
        yield return Shoot(cam, "tint_chair_selected.png");

        AssertSilhouetteChanged(plain, _lastPixels!, cam,
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
        yield return Shoot(cam, "tint_door_plain.png");
        var plain = _lastPixels!;

        Selection().Select(door);
        yield return Shoot(cam, "tint_door_selected.png");

        AssertSilhouetteChanged(plain, _lastPixels!, cam,
            "дверь: меша на корне у неё нет вовсе, поэтому она не желтела НИКАК — "
            + "полотно, коробка и оба наличника обязаны попасть под подсветку");
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
        yield return Shoot(cam, "tint_door_decor_before.png");

        var def = MaterialCatalog.Get("oak");
        MaterialManager.Apply(door, def);
        var newDecor = MaterialManager.GetSharedMaterial(def);
        yield return Shoot(cam, "tint_door_decor.png");

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

    private Camera AimAt(KitchenElement element)
    {
        var body = ElementRenderers.BodyOf(element);
        Assert.Greater(body.Count, 0, "элемент без единого рендерера в кадр не попадёт");

        var bounds = body[0].bounds;
        for (int i = 1; i < body.Count; i++) bounds.Encapsulate(body[i].bounds);

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

    private IEnumerator Shoot(Camera cam, string fileName)
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
        Camera cam, string what)
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

        float share = changed / (float)silhouette;
        Assert.GreaterOrEqual(share, MinChangedShareOfSilhouette,
            what + ". Изменилось " + changed + " из " + silhouette
            + " пикселей силуэта (" + share.ToString("0.00") + "), нужно "
            + MinChangedShareOfSilhouette + ". Серую часть видно на PNG рядом");
    }

    private static bool Differs(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) > ColorDelta
        || Mathf.Abs(a.g - b.g) > ColorDelta
        || Mathf.Abs(a.b - b.b) > ColorDelta;
}
