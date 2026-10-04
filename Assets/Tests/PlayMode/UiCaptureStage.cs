using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Сцена для снимка одного окна вне живого приложения: камера-канва нужного размера, белый лист
// фона D4 и запись PNG в test-results/. Окна, переезжающие на WindowChrome (T5–T11), снимают себя
// этим же стендом; голден — CaptureVerified по имени, когда окно его заводит.
public sealed class UiCaptureStage
{
    private readonly GameObject _canvasGo;
    private readonly GameObject _camGo;
    private readonly GameObject? _eventSystem;
    private readonly RenderTexture _rt;

    public UiCaptureStage(int width, int height)
    {
        Width = width;
        Height = height;
        _canvasGo = new GameObject("CaptureCanvas");
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

        _camGo = new GameObject("CaptureCamera");
        var cam = _camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = height * 0.5f;
        cam.aspect = (float)width / height;
        cam.cullingMask = 1 << _canvasGo.layer;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;

        _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = _rt;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(width, height);
        Canvas = canvas.transform;
    }

    public int Width { get; }

    public int Height { get; }

    public Transform Canvas { get; }

    public GameObject Host => _canvasGo;

    public IEnumerator Capture(string pngName)
    {
        yield return null;
        yield return null;

        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        RenderTexture.active = _rt;
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, pngName);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG пустой: " + path);
        Debug.Log("[DIAGRAM] Saved: " + path);
    }

    public void Dispose()
    {
        _camGo.GetComponent<Camera>().targetTexture = null;
        Object.Destroy(_rt);
        Object.Destroy(_canvasGo);
        Object.Destroy(_camGo);
        if (_eventSystem != null) Object.Destroy(_eventSystem);
    }
}
