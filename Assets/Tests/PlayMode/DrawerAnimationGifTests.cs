using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using SimpleGif;
using SimpleGif.Enums;

/// <summary>Генератор docs/drawer_animation.gif: загружает example.save.json, находит двойной ящик
/// A3_gtv_double_lower_A и записывает GIF-анимацию цикла: открыть оба → закрыть
/// верхний → закрыть все. Сохраняет в docs/drawer_animation.gif.</summary>
[Explicit("генератор docs/drawer_animation.gif: 37 кадров по 100 мс — см. tools\\artifacts.ps1")]
public class DrawerAnimationGifTests : DocsArtifactFixture
{
    // Размер кадра упирается не в читаемость, а в вес файла: 37 кадров с
    // индивидуальной палитрой на 256 цветов дают 3,3 МБ при 480×360 и 11,8 МБ
    // при 720×540 — такую анимацию README грузить не должен. Кодировщик пишет
    // КАЖДЫЙ кадр целиком, без межкадровой разницы, поэтому вес растёт быстрее
    // площади: шум текстуры дерева ломает сжатие по строкам.
    private const int GifW = 480;
    private const int GifH = 360;
    private const int FrameDelayMs = 100; // 10 fps

    private const string DoubleDrawerName = "A3_gtv_double_lower_A";

    /// <summary>Орбита из example.save.json на момент записи анимации.
    /// Зашита жёстко, чтобы кадр не ехал вслед за сейвом.</summary>
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

    [UnityTearDown]
    public IEnumerator GifTearDown()
    {
        Time.captureFramerate = 0;
        yield return null;
    }

    [UnityTest]
    public IEnumerator DrawerAnimation_DoubleDrawer_OpensThenClosesBothTiers()
    {
        yield return LoadExampleProject();

        // ── Найти двойной ящик ────────────────────────────────────────────
        var lower = FindPart(DoubleDrawerName) as DrawerElement;
        Assert.IsNotNull(lower, $"{DoubleDrawerName} is not a {nameof(DrawerElement)}");

        lower!.ForceClose();
        yield return null;

        // ── Камера на ящик ────────────────────────────────────────────────
        var cam = Cam;
        cam.fieldOfView = 40f;
        yield return ApplyCameraState(SavedCamera);

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
        var rt = new RenderTexture(GifW, GifH, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        var tex = new Texture2D(GifW, GifH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, GifW, GifH), 0, 0);
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
