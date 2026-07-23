using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>
/// PlayMode: строим SettingsPanelUI, даём Unity отрисовать UI собственным
/// рендер-пайплайном (TMP, сглаживание, кириллица — всё из коробки) и
/// захватываем результат с камеры в PNG. Никакой ручной растеризации пикселей.
/// </summary>
public class SettingsPanelLayoutDiagramTests
{
    private const int PanelW = 520;
    private const int PanelH = 680;

    private GameObject? _canvasGo;
    private GameObject? _camGo;
    private GameObject? _eventSystem;
    private KitchenSettingsData? _settingsBackup;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_canvasGo != null) Object.Destroy(_canvasGo);
        if (_camGo != null) Object.Destroy(_camGo);
        if (_eventSystem != null) Object.Destroy(_eventSystem);

        // Снапшот сериализует ГЛОБАЛЬНЫЕ настройки — возвращаем прежнее состояние,
        // чтобы тест не зависел от порядка и не заражал соседей.
        var s = KitchenSettings.Instance;
        if (s != null && _settingsBackup != null) s.ApplyFrom(_settingsBackup);
        _settingsBackup = null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator GenerateLayoutDiagram_SavesPng()
    {
        // Детерминизм: строим панель на дефолтных настройках независимо от того,
        // что оставили предыдущие тесты.
        var settings = KitchenSettings.Instance;
        _settingsBackup = settings != null ? settings.ToData() : null;
        if (settings != null) settings.ResetToDefaults();

        // Canvas в ScreenSpaceCamera — рендерится в RenderTexture даже в batchMode.
        _canvasGo = new GameObject("TestCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            _eventSystem = new GameObject("EventSystem");
            _eventSystem.AddComponent<EventSystem>();
            _eventSystem.AddComponent<StandaloneInputModule>();
        }

        // Камера: ортогональная, под размер панели — 1:1 отображение пикселей.
        _camGo = new GameObject("UICamera");
        var cam = _camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = PanelH * 0.5f;
        cam.aspect = (float)PanelW / PanelH;
        cam.cullingMask = 1 << _canvasGo.layer;
        canvas.worldCamera = cam;

        // Строим панель настроек.
        var ui = _canvasGo.AddComponent<SettingsPanelUI>();
        ui.Build(canvas.transform);
        ui.SetVisible(true);

        // RenderTexture под размер панели, рендерим 2 кадра (layout + paint).
        var rt = new RenderTexture(PanelW, PanelH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        // Захват.
        var tex = new Texture2D(PanelW, PanelH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, PanelW, PanelH), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings_panel_layout.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[DIAGRAM] Saved: {path}");

        var jsonPath = Path.ChangeExtension(path, ".json");
        UiSnapshotEngine.CaptureVerified(_canvasGo, jsonPath);
    }
}
