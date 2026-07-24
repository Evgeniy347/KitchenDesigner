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
    private const int RenderW = 480;
    private const int RenderH = 360;
    private const int FrameDelayMs = 100; // 10 fps

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
        // example.save.json несёт handleMode="Move" → RestoreScene выставил
        // глобальный статик. Возвращаем дефолт, иначе тулбар «Ручки: перенос»
        // течёт в снапшоты последующих фикстур (Iso*, тулбар).
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
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
        int animSteps = 10; // 1 секунда на фазу при 10 fps

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

        string outDir = Path.Combine(Application.dataPath, "..", "docs"); 

        // ── 6. Сохранить GIF ──────────────────────────────────────────────
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

        // 1. Собрать все пиксели всех кадров для построения общей палитры
        var allPixels = new List<SimpleGif.Data.Color32>();
        var framePixels = new SimpleGif.Data.Color32[unityFrames.Length][];
        for (int i = 0; i < unityFrames.Length; i++)
        {
            var uPixels = unityFrames[i].GetPixels32();
            var sgPixels = new SimpleGif.Data.Color32[uPixels.Length];
            for (int j = 0; j < uPixels.Length; j++)
                sgPixels[j] = new SimpleGif.Data.Color32(uPixels[j].r, uPixels[j].g, uPixels[j].b, uPixels[j].a);
            framePixels[i] = sgPixels;
            allPixels.AddRange(sgPixels);
        }

        // 2. Median-cut квантизация до 256 цветов
        var palette = BuildPalette256(allPixels);

        // 3. Применить палитру к каждому кадру
        for (int i = 0; i < framePixels.Length; i++)
        {
            QuantizeToPalette(framePixels[i], palette);
            var sgTex = new SimpleGif.Data.Texture2D(unityFrames[i].width, unityFrames[i].height);
            sgTex.SetPixels32(framePixels[i]);
            sgTex.Apply();

            gifFrames.Add(new SimpleGif.Data.GifFrame
            {
                Texture = sgTex,
                Delay = delaySec,
                DisposalMethod = SimpleGif.Enums.DisposalMethod.DoNotDispose
            });

            Object.DestroyImmediate(unityFrames[i]);
        }

        var gif = new SimpleGif.Gif(gifFrames);
        byte[] gifBytes = gif.Encode();

        File.WriteAllBytes(outPath, gifBytes);
    }

    private struct RGB { public byte r, g, b; }

    private static List<SimpleGif.Data.Color32> BuildPalette256(List<SimpleGif.Data.Color32> pixels)
    {
        var buckets = new List<List<SimpleGif.Data.Color32>> { new List<SimpleGif.Data.Color32>(pixels) };

        while (buckets.Count < 256)
        {
            int bestIdx = -1;
            int bestRange = -1;
            for (int i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Count < 2) continue;
                GetChannelRange(buckets[i], out int r, out int g, out int b);
                int range = Mathf.Max(r, Mathf.Max(g, b));
                if (range > bestRange) { bestRange = range; bestIdx = i; }
            }
            if (bestIdx < 0) break;

            var bucket = buckets[bestIdx];
            GetChannelRange(bucket, out int rr, out int gg, out int bb);
            int ch = rr >= gg ? (rr >= bb ? 0 : 2) : (gg >= bb ? 1 : 2);
            bucket.Sort((a, b2) =>
                ch == 0 ? a.r.CompareTo(b2.r) :
                ch == 1 ? a.g.CompareTo(b2.g) :
                a.b.CompareTo(b2.b));

            int mid = bucket.Count / 2;
            var left = bucket.GetRange(0, mid);
            var right = bucket.GetRange(mid, bucket.Count - mid);
            buckets.RemoveAt(bestIdx);
            buckets.Add(left);
            buckets.Add(right);
        }

        var palette = new List<SimpleGif.Data.Color32>(256);
        foreach (var bucket in buckets)
        {
            long r = 0, g = 0, b = 0;
            foreach (var p in bucket) { r += p.r; g += p.g; b += p.b; }
            int cnt = bucket.Count;
            palette.Add(new SimpleGif.Data.Color32((byte)(r / cnt), (byte)(g / cnt), (byte)(b / cnt), 255));
        }
        return palette;
    }

    private static void GetChannelRange(List<SimpleGif.Data.Color32> pixels, out int r, out int g, out int b)
    {
        int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
        foreach (var p in pixels)
        {
            if (p.r < rMin) rMin = p.r; if (p.r > rMax) rMax = p.r;
            if (p.g < gMin) gMin = p.g; if (p.g > gMax) gMax = p.g;
            if (p.b < bMin) bMin = p.b; if (p.b > bMax) bMax = p.b;
        }
        r = rMax - rMin; g = gMax - gMin; b = bMax - bMin;
    }

    private static void QuantizeToPalette(SimpleGif.Data.Color32[] pixels, List<SimpleGif.Data.Color32> palette)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            var px = pixels[i];
            if (px.a < 128) { pixels[i] = new SimpleGif.Data.Color32(0, 0, 0, 0); continue; }
            int best = 0, bestDist = int.MaxValue;
            for (int j = 0; j < palette.Count; j++)
            {
                int dr = px.r - palette[j].r;
                int dg = px.g - palette[j].g;
                int db = px.b - palette[j].b;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist) { bestDist = dist; best = j; }
            }
            var c = palette[best];
            pixels[i] = new SimpleGif.Data.Color32(c.r, c.g, c.b, px.a);
        }
    }
}
