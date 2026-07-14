using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class SettingsPanelTabDiagramTests
{
    private const int PanelW = 520;
    private const int PanelH = 680;

    private GameObject? _canvasGo;
    private GameObject? _camGo;
    private GameObject? _eventSystem;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_canvasGo != null) Object.Destroy(_canvasGo);
        if (_camGo != null) Object.Destroy(_camGo);
        if (_eventSystem != null) Object.Destroy(_eventSystem);
        yield return null;
    }

    private (Canvas canvas, Camera cam, SettingsPanelUI ui) BuildPanel()
    {
        _canvasGo = new GameObject("TestCanvas");
        var canvas = _canvasGo!.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvasGo!.AddComponent<CanvasScaler>();
        _canvasGo!.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            _eventSystem = new GameObject("EventSystem");
            _eventSystem.AddComponent<EventSystem>();
            _eventSystem.AddComponent<StandaloneInputModule>();
        }

        _camGo = new GameObject("UICamera");
        var cam = _camGo!.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = PanelH * 0.5f;
        cam.aspect = (float)PanelW / PanelH;
        cam.cullingMask = 1 << _canvasGo!.layer;
        canvas.worldCamera = cam;

        var ui = _canvasGo!.AddComponent<SettingsPanelUI>();
        ui.Build(canvas.transform);
        ui.SetVisible(true);

        return (canvas, cam, ui);
    }

    private void SwitchToTab(int index)
    {
        var panel = _canvasGo!.transform.Find("SettingsPanel");
        if (panel == null) return;

        var tabBtn = panel.Find($"Tab_{index}");
        if (tabBtn == null) return;

        var btn = tabBtn.GetComponent<Button>();
        if (btn != null) btn.onClick.Invoke();
    }

    private IEnumerator CaptureAndSave(string fileName)
    {
        var cam = _camGo!.GetComponent<Camera>();
        var rt = new RenderTexture(PanelW, PanelH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(PanelW, PanelH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, PanelW, PanelH), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[SCREENSHOT] Saved: {path}");
    }

    [UnityTest]
    public IEnumerator TabProject_SavesPng()
    {
        BuildPanel();
        SwitchToTab(0);
        yield return null;
        yield return CaptureAndSave("settings_tab_project.png");
    }

    [UnityTest]
    public IEnumerator TabGraphics_SavesPng()
    {
        BuildPanel();
        SwitchToTab(1);
        yield return null;
        yield return CaptureAndSave("settings_tab_graphics.png");
    }

    [UnityTest]
    public IEnumerator TabAbout_SavesPng()
    {
        BuildPanel();
        SwitchToTab(2);
        yield return null;
        yield return CaptureAndSave("settings_tab_about.png");
    }
}
