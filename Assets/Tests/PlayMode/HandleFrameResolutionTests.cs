using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Handles;

/// <summary>
/// Ручка обязана выйти в КАДРЕ той длины, которую для этого кадра назначили,
/// сколько бы пикселей ни было у главной камеры. Раскладка считала масштаб от
/// высоты Camera.main, а замер шёл по кадру 512x512 — два разных числа для одной
/// величины, и набор был зелёным только по совпадению разрешения прогона.
///
/// Здесь высота Camera.main меняется в 2,667 раза (384 -> 1024, через её
/// targetTexture: в батче экран подменить нечем), а ручка меряется в ОДНОЙ и той
/// же снимающей камере 512x512. На коде, который берёт Camera.main внутри
/// раскладки, две длины отличаются ровно в это же число раз.
///
/// Мерит тот же датчик, что и сторожа §12 — HandleScreenExtent поверх PinholeView
/// снимающей камеры: длина силуэта нарисованной стрелки в пикселях. Пиксельная
/// проба кадра сказала бы то же самое грубее — цвет ручки лежит на кадре
/// HandleOverlayRenderTests, а длина здесь.
/// </summary>
public class HandleFrameResolutionTests
{
    private const int RenderW = 512;
    private const int RenderH = 512;

    private const int MainHeightSmall = 384;
    private const int MainHeightLarge = 1024;

    private const float SamePixelLength = 0.5f;
    private const float ChosenLengthTolerance = 2f;

    private static readonly Vector3 CameraPos = new Vector3(0f, 0.6f, -2.5f);
    private static readonly Vector3 TargetPos = new Vector3(0f, 0.6f, 0.5f);
    private static readonly Vector3Int TargetDims = new Vector3Int(300, 300, 18);

    private static readonly int[] AcrossTheScreenFaces = { 0, 1, 2, 3 };

    private GameObject? _bootstrap;
    private GameObject? _mainCameraObject;
    private Camera? _mainCamera;
    private GameObject? _renderCameraObject;
    private Camera? _renderCamera;
    private RenderTexture? _renderTexture;
    private RenderTexture? _mainTexture;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCameraObject = new GameObject("Main Camera");
        _mainCameraObject!.tag = "MainCamera";
        _mainCamera = _mainCameraObject!.AddComponent<Camera>();
        _mainCameraObject!.transform.position = new Vector3(0f, 3f, -5f);
        _mainCameraObject!.transform.LookAt(Vector3.zero);

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
        if (_mainCamera != null) _mainCamera.targetTexture = null;
        if (_renderCamera != null) _renderCamera.targetTexture = null;
        if (_mainTexture != null) Object.DestroyImmediate(_mainTexture);
        if (_renderTexture != null) Object.DestroyImmediate(_renderTexture);
        _mainTexture = null;
        _renderTexture = null;
        _renderCamera = null;
        _mainCamera = null;

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        SelectionManager.Instance?.Deselect();
        yield return null;

        foreach (var go in _spawned) if (go != null) Object.Destroy(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_renderCameraObject != null) Object.Destroy(_renderCameraObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCameraObject != null) Object.Destroy(_mainCameraObject);
        yield return null;
    }

    private static ResizeHandle[] Handles() =>
        Object.FindObjectsByType<ResizeHandle>(FindObjectsSortMode.None);

    private IEnumerator BuildSceneAndCamera()
    {
        var go = ElementFactory.CreatePart(TargetDims, "Полка", TargetPos);
        _spawned.Add(go);
        var target = go.GetComponent<KitchenElement>();
        yield return null;

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        SelectionManager.Instance!.Select(target);
        yield return null;
        yield return null;

        _renderCameraObject = new GameObject("HandleFrameCam");
        _renderCamera = _renderCameraObject.AddComponent<Camera>();
        _renderCamera.clearFlags = CameraClearFlags.SolidColor;
        _renderCamera.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        _renderCamera.fieldOfView = 45f;
        _renderCamera.aspect = (float)RenderW / RenderH;
        _renderCamera.nearClipPlane = 0.01f;
        _renderCamera.farClipPlane = 100f;
        _renderCameraObject.transform.position = CameraPos;
        _renderCameraObject.transform.LookAt(TargetPos);

        _renderTexture = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        _renderCamera.targetTexture = _renderTexture;
        yield return null;

        Assert.AreEqual(RenderH, _renderCamera.pixelHeight,
            "снимающая камера обязана мерить в кадре 512x512, иначе замер идёт "
            + "в экране батч-прогона — ровно та подмена, ради которой набор написан");
    }

    /// <summary>Ставит главной камере высоту mainHeight, даёт кадр на раскладку по
    /// ней, затем раскладывает ручки для СНИМАЮЩЕЙ камеры и меряет их в ней.
    /// Порядок важен: LateUpdate следующего кадра снова разложит по Camera.main,
    /// поэтому замер идёт в том же кадре, что и раскладка.</summary>
    private IEnumerator MeasureWithMainHeight(int mainHeight,
        Dictionary<int, float> lengths, List<float> mainScale)
    {
        if (_mainTexture != null) { _mainCamera!.targetTexture = null; Object.DestroyImmediate(_mainTexture); }
        _mainTexture = new RenderTexture(mainHeight, mainHeight, 24, RenderTextureFormat.ARGB32);
        _mainCamera!.targetTexture = _mainTexture;
        yield return null;

        Assert.AreEqual(mainHeight, _mainCamera!.pixelHeight,
            "стенд обязан реально менять высоту главной камеры: без этого обе "
            + "пробы взяты при одном разрешении и тест не проверяет ничего");

        var manager = ResizeHandleManager.Instance;
        Assert.IsNotNull(manager, "менеджер ручек создаётся Bootstrap");
        manager!.PositionHandles(_renderCamera);

        var view = HandleView.Of(_renderCamera!);
        var handles = Handles();
        Assert.AreEqual(6, handles.Length,
            "в режиме перемещения ручки строятся на все шесть граней");

        mainScale.Add(HandleScale.ForScreen(HandleView.Of(_mainCamera!),
            TargetPos, HandleMetrics.Resize));

        foreach (var h in handles)
        {
            var extent = HandleScreenExtent.Measure(view, h.transform.position,
                h.transform.forward, HandleMetrics.Resize, h.transform.localScale.x);
            Assert.IsTrue(extent.InFront, $"грань {h.faceIndex}: ручка обязана быть перед камерой");
            lengths[h.faceIndex] = extent.LengthPixels;
        }
    }

    [UnityTest]
    public IEnumerator ArrowInTheFrame_KeepsItsPixelLength_WhateverTheMainCameraResolution()
    {
        yield return BuildSceneAndCamera();

        var small = new Dictionary<int, float>();
        var large = new Dictionary<int, float>();
        var mainScale = new List<float>();

        yield return MeasureWithMainHeight(MainHeightSmall, small, mainScale);
        yield return MeasureWithMainHeight(MainHeightLarge, large, mainScale);

        float ratioOfHeights = (float)MainHeightLarge / MainHeightSmall;
        Assert.AreEqual(ratioOfHeights, mainScale[0] / mainScale[1], 0.05f,
            "контроль стенда: масштаб, посчитанный ПО ГЛАВНОЙ камере, обязан "
            + $"измениться ровно в {ratioOfHeights:0.###} раза — иначе targetTexture "
            + "не поменял её pixelHeight, и равенство длин ниже ничего не доказывает");

        var moved = new List<string>();
        foreach (var pair in small)
        {
            float after = large[pair.Key];
            if (Mathf.Abs(after - pair.Value) > SamePixelLength)
                moved.Add($"грань {pair.Key}: {pair.Value:0.0} -> {after:0.0} px "
                    + $"(в {after / Mathf.Max(pair.Value, 0.001f):0.###} раза)");
        }

        Assert.IsEmpty(moved,
            "длина ручки в кадре 512x512 обязана зависеть ТОЛЬКО от камеры, для "
            + "которой её раскладывают. Изменилась вместе с высотой главной камеры: "
            + string.Join("; ", moved)
            + ". Значит масштаб снова берут у Camera.main внутри раскладки, и любой "
            + "пиксельный замер на другом разрешении окна поедет не из-за поломки.");
    }

    [UnityTest]
    public IEnumerator ArrowAcrossTheFrame_IsDrawnAtTheChosenPixelLength()
    {
        yield return BuildSceneAndCamera();

        var lengths = new Dictionary<int, float>();
        var mainScale = new List<float>();
        yield return MeasureWithMainHeight(MainHeightSmall, lengths, mainScale);

        foreach (int face in AcrossTheScreenFaces)
            Assert.AreEqual(HandleScale.DrawnArrowPixels, lengths[face], ChosenLengthTolerance,
                $"грань {face}: стрелка поперёк взгляда обязана выйти выбранными "
                + $"{HandleScale.DrawnArrowPixels:0} px именно в том кадре, для которого "
                + "её разложили. Равенство двух проб без этого числа было бы зелёным и "
                + "на стабильно НЕВЕРНОЙ длине");
    }
}
