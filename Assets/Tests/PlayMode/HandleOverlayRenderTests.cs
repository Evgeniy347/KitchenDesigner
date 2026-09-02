using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Handles;

/// <summary>
/// Воспроизведение жалобы «ручки заходят внутрь объектов»: деталь стоит ЗА
/// сплошной заслонкой, и все шесть стрелок геометрически перекрыты. Ручка
/// обязана остаться видимой — иначе её нечем ухватить.
///
/// Проверка идёт по ПИКСЕЛЯМ отрисованного кадра, а не по свойствам материала:
/// отказ здесь бесшумный (цвет, размер и положение стрелки остаются
/// правильными, меняется только проверка глубины), и единственный честный
/// свидетель — сам кадр.
///
/// Кадр снимает СВОЯ камера, а не Camera.main. Первая версия набора светила
/// главной камерой и промахнулась мимо всех стрелок: Bootstrap вешает на
/// Camera.main компонент CameraController, тот каждый кадр ставит камеру на
/// орбиту вокруг НУЛЯ на расстоянии 5 м, и позиция из [UnitySetUp] жила ровно
/// до первого Update. Отсюда же второе правило набора: пробовать пиксели
/// можно только пока RenderTexture ЕЩЁ назначена. WorldToScreenPoint считает
/// в текущем pixelRect камеры, и стоит снять targetTexture — как рект
/// становится экраном батч-прогона, а не кадром 512x512, который мы только
/// что прочитали. Оба факта закреплены ассертами, а не комментарием:
/// Camera_StaysWhereTheTestPutIt и проверка pixelWidth в Sample.
///
/// Чтобы набор не выродился в «зелено всегда», у пиксельной проверки есть
/// встречная: заслонка обязана прятать саму ДЕТАЛЬ. Точка на её лице по
/// диагонали (мимо стрелок, которые лежат на осях) обязана быть цветом
/// заслонки. Видны стрелки и не видно детали — накладка работает; не видно ни
/// того, ни другого — накладка сломана; видна деталь — сцена разъехалась, и
/// весь замер недействителен.
/// </summary>
public class HandleOverlayRenderTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;

    private const float ChannelMargin = 0.10f;
    private const int SampleHalfWindow = 4;
    private const float SameSurfaceTolerance = 0.10f;

    private static readonly Vector3 CameraPos = new Vector3(0f, 0.6f, -2.5f);
    private static readonly Vector3 OccluderPos = new Vector3(0f, 0.6f, 0f);
    private static readonly Vector3Int OccluderDims = new Vector3Int(1400, 1400, 18);
    private static readonly Vector3 TargetPos = new Vector3(0f, 0.6f, 0.5f);
    private static readonly Vector3Int TargetDims = new Vector3Int(300, 300, 18);

    private static readonly Vector3 OnTargetFaceOffAxis = new Vector3(0.10f, -0.10f, 0f);
    private static readonly Vector3 OnOccluderBesideTarget = new Vector3(0.42f, -0.10f, 0f);

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private GameObject? _renderCamera;
    private Camera? _cam;
    private RenderTexture? _rt;
    private Texture2D? _frame;
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
        if (_cam != null) _cam.targetTexture = null;
        if (_frame != null) Object.DestroyImmediate(_frame);
        if (_rt != null) Object.DestroyImmediate(_rt);
        _frame = null;
        _rt = null;
        _cam = null;

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        SelectionManager.Instance?.Deselect();
        yield return null;

        foreach (var go in _spawned) if (go != null) Object.Destroy(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_renderCamera != null) Object.Destroy(_renderCamera);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    private KitchenElement Spawn(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static ResizeHandle[] Handles() =>
        Object.FindObjectsByType<ResizeHandle>(FindObjectsSortMode.None);

    /// <summary>Сцена, ручки и снятый кадр. Камера СВОЯ и остаётся с
    /// назначенной RenderTexture до самого TearDown — см. сводку класса.</summary>
    private IEnumerator BuildSceneAndGrabFrame()
    {
        Spawn("Заслон", OccluderDims, OccluderPos);
        var target = Spawn("Полка", TargetDims, TargetPos);
        yield return null;

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        SelectionManager.Instance!.Select(target);
        yield return null;
        yield return null;

        _renderCamera = new GameObject("HandleOverlayCam");
        _cam = _renderCamera.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        _cam.fieldOfView = 45f;
        _cam.aspect = (float)RenderW / RenderH;
        _cam.nearClipPlane = 0.01f;
        _cam.farClipPlane = 100f;
        _renderCamera.transform.position = CameraPos;
        _renderCamera.transform.LookAt(TargetPos);

        _rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        _cam.targetTexture = _rt;
        yield return null;
        yield return null;

        _frame = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = _rt;
        _frame.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        _frame.Apply();
        RenderTexture.active = null;

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "handles_inside_board.png"), _frame.EncodeToPNG());
        Debug.Log($"[HANDLES] Saved: {Path.Combine(dir, "handles_inside_board.png")}");
    }

    private static float ChannelOf(Color c, int axis) =>
        axis == 0 ? c.r : (axis == 1 ? c.g : c.b);

    private Vector2Int Project(Vector3 world)
    {
        Assert.AreEqual(RenderW, _cam!.pixelWidth,
            "проба берётся в pixelRect камеры: сняв targetTexture, мы стали бы "
            + "проецировать в экран батч-прогона, а читать пиксели из кадра 512x512");
        var p = _cam!.WorldToScreenPoint(world);
        Assert.Greater(p.z, 0f, "точка обязана быть перед камерой, иначе проекция бессмысленна");
        return new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
    }

    /// <summary>Самый выразительный по оси axis пиксель окна вокруг точки:
    /// возвращает и цвет, и признак «ось доминирует». Цвет нужен в сообщении
    /// о падении — иначе «стрелка не найдена» не отличить от «стрелка есть,
    /// но оттенок не тот».</summary>
    private (bool dominates, Color best) Sample(Vector2Int at, int axis)
    {
        Color best = Color.black;
        float bestScore = float.NegativeInfinity;
        bool dominates = false;
        for (int dx = -SampleHalfWindow; dx <= SampleHalfWindow; dx++)
            for (int dy = -SampleHalfWindow; dy <= SampleHalfWindow; dy++)
            {
                int x = at.x + dx, y = at.y + dy;
                if (x < 0 || y < 0 || x >= RenderW || y >= RenderH) continue;
                var c = _frame!.GetPixel(x, y);
                float mine = ChannelOf(c, axis);
                float rival = Mathf.Max(
                    ChannelOf(c, (axis + 1) % 3), ChannelOf(c, (axis + 2) % 3));
                float score = mine - rival;
                if (score > bestScore) { bestScore = score; best = c; }
                if (score >= ChannelMargin) dominates = true;
            }
        return (dominates, best);
    }

    private Color AverageAt(Vector2Int at)
    {
        Color sum = Color.black;
        int n = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = at.x + dx, y = at.y + dy;
                if (x < 0 || y < 0 || x >= RenderW || y >= RenderH) continue;
                sum += _frame!.GetPixel(x, y);
                n++;
            }
        return sum / Mathf.Max(n, 1);
    }

    [UnityTest]
    public IEnumerator Camera_StaysWhereTheTestPutIt_AndTheOccluderHidesTheElement()
    {
        yield return BuildSceneAndGrabFrame();

        Assert.AreEqual(0f, Vector3.Distance(CameraPos, _renderCamera!.transform.position), 1e-3f,
            "снимающая камера обязана остаться там, куда её поставил тест: на Camera.main "
            + "Bootstrap вешает CameraController, и тот каждый кадр уводит её на орбиту "
            + "вокруг нуля — замер по такой камере меряет не ту сцену");

        var onTarget = AverageAt(Project(TargetPos + OnTargetFaceOffAxis));
        var onOccluder = AverageAt(Project(OccluderPos + OnOccluderBesideTarget));

        Assert.AreEqual(onOccluder.r, onTarget.r, SameSurfaceTolerance,
            $"по диагонали от центра детали (мимо стрелок, они лежат на осях) обязан "
            + $"быть цвет ЗАСЛОНКИ: деталь целиком за ней. Видно деталь — сцена "
            + $"разъехалась, и пиксельная проверка ручек недействительна. "
            + $"деталь {onTarget}, заслонка {onOccluder}");
        Assert.AreEqual(onOccluder.g, onTarget.g, SameSurfaceTolerance,
            "то же по зелёному каналу");
        Assert.AreEqual(onOccluder.b, onTarget.b, SameSurfaceTolerance,
            "то же по синему каналу");
    }

    [UnityTest]
    public IEnumerator Occluder_ReallyStandsBetweenTheCameraAndEveryHandle()
    {
        yield return BuildSceneAndGrabFrame();
        var handles = Handles();
        Assert.AreEqual(6, handles.Length,
            "в режиме перемещения ручки строятся на все шесть граней; "
            + "иначе замерять нечего и остальные проверки бессмысленны");

        Vector3 eye = _renderCamera!.transform.position;
        float halfW = OccluderDims.x * AppConstants.MM_TO_UNITS * 0.5f;
        float halfH = OccluderDims.y * AppConstants.MM_TO_UNITS * 0.5f;
        foreach (var h in handles)
        {
            float dz = h.grabPoint.z - eye.z;
            Assert.Greater(dz, 0f, $"грань {h.faceIndex}: ручка обязана быть перед камерой");
            float t = (OccluderPos.z - eye.z) / dz;
            Assert.That(t, Is.InRange(0f, 1f),
                $"грань {h.faceIndex}: заслонка обязана лежать МЕЖДУ камерой и ручкой");
            var hit = Vector3.Lerp(eye, h.grabPoint, t);
            Assert.Less(Mathf.Abs(hit.x - OccluderPos.x), halfW,
                $"грань {h.faceIndex}: луч уходит мимо заслонки по X — стрелка видна "
                + "не благодаря накладке, а потому что её ничто не закрывает");
            Assert.Less(Mathf.Abs(hit.y - OccluderPos.y), halfH,
                $"грань {h.faceIndex}: луч уходит мимо заслонки по Y");
        }
    }

    [UnityTest]
    public IEnumerator HandleInsideAnotherBoard_IsStillDrawnOnTop_OnEveryAxis()
    {
        yield return BuildSceneAndGrabFrame();
        var handles = Handles();
        Assume.That(handles.Length, Is.EqualTo(6), "шесть ручек — предпосылка замера");

        var missing = new List<string>();
        foreach (var h in handles)
        {
            int axis = h.faceIndex / 2;
            var (dominates, best) = Sample(Project(h.grabPoint), axis);
            if (!dominates)
                missing.Add($"грань {h.faceIndex} (ось {axis}), самый выразительный пиксель {best}");
        }

        Assert.IsEmpty(missing,
            "стрелки утонули в заслонке: " + string.Join("; ", missing)
            + ". Ручка внутри чужой детали не видна, а значит её нечем ухватить — "
            + "ровно та жалоба, ради которой ручкам дан ZTest Always");
    }

    [UnityTest]
    public IEnumerator EveryHandleRenderer_UsesTheOverlayShader_NotASilentSubstitute()
    {
        yield return BuildSceneAndGrabFrame();
        var handles = Handles();
        Assume.That(handles.Length, Is.EqualTo(6), "шесть ручек — предпосылка замера");

        int checkedRenderers = 0;
        foreach (var h in handles)
            foreach (var r in h.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.IsNotNull(r.sharedMaterial,
                    $"{h.name}/{r.gameObject.name}: рендерер без материала");
                Assert.AreEqual(HandleMaterials.ShaderName, r.sharedMaterial.shader.name,
                    $"{h.name}/{r.gameObject.name}: шейдер подменён — обычная проверка "
                    + "глубины топит ручку в соседней детали, и в логе об этом ни строчки");
                checkedRenderers++;
            }

        Assert.AreEqual(12, checkedRenderers,
            "шесть стрелок по два рендерера: ствол и наконечник — накладку получают оба");
    }
}
