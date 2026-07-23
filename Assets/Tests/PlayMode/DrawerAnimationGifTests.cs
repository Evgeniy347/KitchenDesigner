using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using SimpleGif;
using SimpleGif.Enums;

/// <summary>PlayMode-тест: загружает example.save.json, находит двойной ящик
/// A3_gtv_double_lower_A и записывает GIF-анимацию цикла: открыть оба → закрыть
/// верхний → закрыть все. Сохраняет в docs/drawer_animation.gif.</summary>
public class DrawerAnimationGifTests
{
    private const int RenderW = 640;
    private const int RenderH = 480;
    private const int FrameDelayMs = 50; // 20 fps → 3 phases × 20 = 60 frames

    private GameObject? _bootstrap;
    private GameObject? _testCamera;
    private GameObject? _sunGo;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _testCamera = new GameObject("Main Camera");
        _testCamera!.tag = "MainCamera";
        _testCamera!.AddComponent<Camera>();
        _testCamera!.transform.position = Vector3.zero;
        _testCamera!.transform.rotation = Quaternion.identity;

        _sunGo = new GameObject("Directional Light");
        var light = _sunGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        RenderSettings.sun = light;
        SunController.Reset();

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
        Time.captureFramerate = 0;
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_testCamera != null) Object.Destroy(_testCamera);
        if (_sunGo != null) Object.Destroy(_sunGo);
        yield return null;
    }

    private static readonly CameraState SavedCamera = new CameraState
    {
        valid = true,
        targetX = 0.9263714f,
        targetY = 0.75004f,
        targetZ = -2.1795108f,
        angleX = 31.59988f,
        angleY = -588.40015f,
        distance = 1.2f
    };

    [UnityTest]
    public IEnumerator RecordDrawerAnimation()
    {
        // ── 1. Загрузить сцену ────────────────────────────────────────────
        string savePath = Path.Combine(Application.dataPath, "..", "docs", "example.save.json");
        Assert.IsTrue(File.Exists(savePath), $"Save file not found: {savePath}");

        var data = SaveLoadManager.LoadFromFile(savePath);
        Assert.IsNotNull(data, "Failed to deserialize save file");

        SaveLoadManager.ClearBoards(PartRegistry.GetAll());
        var created = SaveLoadManager.RestoreScene(data!);
        Assert.IsTrue(created.Count > 0, "No elements created from save");

        yield return null;
        yield return null;
        yield return null;

        // ── 2. Камера ─────────────────────────────────────────────────────
        if (CameraController.Instance != null)
            CameraController.Instance.SetState(SavedCamera);

        yield return null;

        // ── 3. Найти двойной ящик ─────────────────────────────────────────
        DrawerElement? lower = null;
        foreach (var el in PartRegistry.GetAll())
        {
            if (el is DrawerElement d && d.PartName == "A3_gtv_double_lower_A")
            {
                lower = d;
                break;
            }
        }
        Assert.IsNotNull(lower, "Drawer A3_gtv_double_lower_A not found");

        lower!.ForceClose();
        yield return null;

        // ── 4. Камера на ящик ─────────────────────────────────────────────
        var cam = _testCamera!.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.85f, 0.87f, 0.9f, 1f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 50f;
        cam.transform.position = lower.transform.position + new Vector3(-0.8f, 0.3f, -0.6f);
        cam.transform.LookAt(lower.transform.position + Vector3.up * 0.05f);

        yield return null;

        // ── 5. Запись кадров ──────────────────────────────────────────────
        // Зафиксировать fps, чтобы Time.deltaTime был предсказуемым в batch-mode
        int targetFps = Mathf.RoundToInt(1000f / FrameDelayMs);
        Time.captureFramerate = targetFps;

        var frames = new List<Texture2D>();
        int animSteps = 20; // 1 секунда на фазу при 20 fps

        // Кадр 0: ящик закрыт.
        frames.Add(CaptureFrame(cam));

        // ── Открыть оба ящика ─────────────────────────────────────────────
        lower.DoubleState = DoubleDrawerState.BothOpen;
        for (int i = 0; i < animSteps; i++)
        {
            frames.Add(CaptureFrame(cam));
            yield return null;
        }
        // Кадры в финальной позиции.
        frames.Add(CaptureFrame(cam));
        frames.Add(CaptureFrame(cam));

        // ── Закрыть верхний → LowerOnly ───────────────────────────────────
        lower.DoubleState = DoubleDrawerState.LowerOnly;
        for (int i = 0; i < animSteps; i++)
        {
            frames.Add(CaptureFrame(cam));
            yield return null;
        }
        frames.Add(CaptureFrame(cam));
        frames.Add(CaptureFrame(cam));

        // ── Закрыть все → Closed ──────────────────────────────────────────
        lower.DoubleState = DoubleDrawerState.Closed;
        for (int i = 0; i < animSteps; i++)
        {
            frames.Add(CaptureFrame(cam));
            yield return null;
        }
        frames.Add(CaptureFrame(cam));
        frames.Add(CaptureFrame(cam));

        Time.captureFramerate = 0;

        // ── 6. Сохранить PNG-кадры ─────────────────────────────────────────
        string outDir = Path.Combine(Application.dataPath, "..", "docs");
        string pngDir = Path.Combine(outDir, "drawer_animation-png");
        Directory.CreateDirectory(pngDir);
        for (int i = 0; i < frames.Count; i++)
        {
            File.WriteAllBytes(Path.Combine(pngDir, $"frame_{i:D3}.png"), frames[i].EncodeToPNG());
        }
        Debug.Log($"[GIF] Saved {frames.Count} PNG frames to {pngDir}");

        // ── 7. Сохранить GIF ──────────────────────────────────────────────
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "drawer_animation.gif");

        EncodeGifWithSimpleGif(frames.ToArray(), outPath);

        Assert.IsTrue(File.Exists(outPath), $"GIF was not created at {outPath}");
        Assert.IsTrue(new FileInfo(outPath).Length > 0, "GIF file is empty");
        Debug.Log($"[GIF] Saved: {outPath} ({new FileInfo(outPath).Length} bytes, {frames.Count} frames)");

        yield return null;
    }

    private Texture2D CaptureFrame(Camera cam)
    {
        var rt = new RenderTexture(RenderW, RenderH, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        var tex = new Texture2D(RenderW, RenderH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RenderW, RenderH), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);

        return tex;
    }

    private void EncodeGifWithSimpleGif(Texture2D[] unityFrames, string outPath)
    {
        var gifFrames = new List<SimpleGif.Data.GifFrame>();
        float delaySec = FrameDelayMs / 1000f;

        foreach (var uTex in unityFrames)
        {
            var uPixels = uTex.GetPixels32();
            var sgTex = new SimpleGif.Data.Texture2D(uTex.width, uTex.height);
            var sgPixels = new SimpleGif.Data.Color32[uPixels.Length];
            for (int i = 0; i < uPixels.Length; i++)
                sgPixels[i] = new SimpleGif.Data.Color32(uPixels[i].r, uPixels[i].g, uPixels[i].b, uPixels[i].a);
            sgTex.SetPixels32(sgPixels);
            sgTex.Apply();

            gifFrames.Add(new SimpleGif.Data.GifFrame
            {
                Texture = sgTex,
                Delay = delaySec,
                DisposalMethod = DisposalMethod.DoNotDispose
            });

            Object.DestroyImmediate(uTex);
        }

        var gif = new Gif(gifFrames);
        byte[] gifBytes = gif.Encode();

        File.WriteAllBytes(outPath, gifBytes);
    }
}
