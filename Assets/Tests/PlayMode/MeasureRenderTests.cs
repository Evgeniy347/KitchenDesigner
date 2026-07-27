using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;
using KitchenDesigner.Tests;

/// <summary>
/// PlayMode: разметка рулетки реально попадает в кадр. Ловит «немую отрисовку»,
/// когда подпись расстояния видна, а красного отрезка и точек нет вовсе —
/// именно так проявлялся отказ MeasureRenderer при пустой Camera.current.
/// </summary>
public class MeasureRenderTests
{
    private const int RenderW = 256;
    private const int RenderH = 256;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private Camera? _camera;

    private int _redPixels;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera.tag = "MainCamera";
        _camera = _mainCamera.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black;
        _mainCamera.transform.position = new Vector3(0f, 0f, -2f);
        _mainCamera.transform.LookAt(Vector3.zero);

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
        MeasureMode.Reset();

        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    /// <summary>Горизонтальный отрезок поперёк кадра — его красный пунктир
    /// обязан дать заметное число красных пикселей на чёрном фоне.</summary>
    [UnityTest]
    public IEnumerator MeasureRenderer_ActiveMode_DrawsRedMarkup()
    {
        MeasureMode.SetActive(true);
        MeasureStore.Add(new MeasureSegment(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f)));

        yield return CountRedPixels();

        Assert.Greater(_redPixels, 50,
            "Разметка рулетки не попала в кадр: виден только текст, линий и точек нет.");
    }

    /// <summary>Обратная проверка — без неё тест выше прошёл бы на любом
    /// красноватом мусоре в кадре.</summary>
    [UnityTest]
    public IEnumerator MeasureRenderer_InactiveMode_DrawsNothing()
    {
        MeasureStore.Add(new MeasureSegment(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f)));

        yield return CountRedPixels();

        Assert.AreEqual(0, _redPixels, "Вне режима рулетки разметки в кадре быть не должно.");
    }

    // Красный пиксель — тот, где красный канал заметно перевешивает остальные:
    // цвет отрезка UIStyle.MeasureLine на чёрном фоне даёт именно такой.
    private IEnumerator CountRedPixels()
    {
        var rt = new RenderTexture(RenderW, RenderH, 24, RenderTextureFormat.ARGB32);
        _camera!.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();

        _redPixels = 0;
        foreach (var p in tex.GetPixels())
            if (p.r > 0.4f && p.r > p.g * 2f && p.r > p.b * 2f) _redPixels++;

        RenderTexture.active = null;
        _camera.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
