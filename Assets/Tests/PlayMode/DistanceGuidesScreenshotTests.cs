using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;
using KitchenDesigner.Core.UI;

/// <summary>PlayMode: направляющие расстояний реально попадают в кадр — пунктир рулетки и
/// подписи, на замороженной копии проекта пользователя (Polka_30 между Polka_29 и Polka_22).
/// Пишет два PNG в test-results/: равный зазор взят (две жёлтые линии по +Z) и отпущен
/// (одна красная). Подписи рисуются тем же MeasureLabelsUI, что и у рулетки; холст здесь
/// ScreenSpaceCamera, иначе подписи не попали бы в RenderTexture.</summary>
public class DistanceGuidesScreenshotTests
{
    private const int Width = 1280;
    private const int Height = 800;
    private const string FixturePath = "Tests/EditMode/Fixtures/distance-guides-scene.save.json";

    private GameObject? _host;
    private GameObject? _canvasHost;
    private Camera? _camera;
    private ElementMover? _mover;
    private KitchenElement? _polka30;
    private Vector3 _start;
    private KitchenSettingsData? _settingsBefore;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        _settingsBefore = KitchenSettings.Instance.ToData();
        KitchenSettings.Instance.AutoSave = false;
        KitchenSettings.Instance.SnapEnabled = true;

        var data = SaveLoadManager.Deserialize(File.ReadAllText(Path.Combine(Application.dataPath, FixturePath)));
        Assert.IsNotNull(data, "фикстура не разобралась");
        var scene = SaveLoadManager.RestoreScene(data!)
            .Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList();
        _polka30 = scene.First(e => e!.PartName == "Polka_30");
        _start = _polka30!.transform.position;

        _host = new GameObject("GuidesHost");
        _camera = _host.AddComponent<Camera>();
        _host.tag = "MainCamera";
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        _camera.fieldOfView = 40f;
        _host.transform.position = new Vector3(1.0f, 6.2f, -0.6f);
        _host.transform.LookAt(new Vector3(3.15f, 1.0f, -1.55f));
        var light = new GameObject("GuidesLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.SetParent(_host.transform, false);
        light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        _host.AddComponent<MeasureRenderer>();
        _host.AddComponent<SelectionManager>();
        _mover = _host.AddComponent<ElementMover>();

        _canvasHost = new GameObject("GuidesCanvas");
        var canvas = _canvasHost.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _camera;
        canvas.planeDistance = 1f;
        _canvasHost.AddComponent<MeasureLabelsUI>().Build(_canvasHost.transform);

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (ElementMover.IsDragging && _mover != null) _mover.TryCancelDragFromEscape();
        DistanceGuideStore.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        if (_canvasHost != null) Object.Destroy(_canvasHost);
        if (_host != null) Object.Destroy(_host);
        KitchenSettings.Instance.ApplyFrom(_settingsBefore);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Guides_EqualGapTakenAndReleased_SavePngs()
    {
        _mover!.BeginDragOn(_polka30!);
        _mover.DragFrameOn(_start + new Vector3(0f, 0f, 0.020f));
        Assert.AreEqual(2, DistanceGuideStore.Lines.Count(l => l.EqualGap),
            "посылка кадра: равный зазор взят — Polka_30↔Polka_29 и Polka_29↔Polka_5");
        var taken = new Overlay();
        yield return CaptureOverlay("distance_guides_equal_gap.png", taken);

        _mover.DragFrameOn(_start + new Vector3(0f, 0f, 0.080f));
        Assert.AreEqual(0, DistanceGuideStore.Lines.Count(l => l.EqualGap),
            "посылка кадра: равный зазор отпущен");
        var released = new Overlay();
        yield return CaptureOverlay("distance_guides_released.png", released);

        Assert.Greater(taken.Accent, 100,
            "равный зазор обязан быть виден: жёлтый пунктир двух линий по +Z");
        Assert.Greater(taken.Line, 100, "и красные направляющие остальных направлений");
        Assert.Less(released.Accent, taken.Accent / 10,
            "после отпускания жёлтых линий нет — вторая линия исчезла, первая снова красная");
        Assert.Greater(released.Line, taken.Line,
            "красного стало больше: линия до Polka_29 перекрасилась из жёлтой в красную");
    }

    private sealed class Overlay
    {
        public int Accent;
        public int Line;
    }

    private IEnumerator CaptureOverlay(string fileName, Overlay counted)
    {
        var guided = new Color[0];
        yield return Capture(fileName, pixels => guided = pixels);

        var lines = DistanceGuideStore.Lines.ToList();
        DistanceGuideStore.Clear();
        var clean = new Color[0];
        yield return Capture(null, pixels => clean = pixels);
        DistanceGuideStore.Set(lines);

        for (int i = 0; i < guided.Length; i++)
        {
            if (Same(guided[i], clean[i])) continue;
            if (IsAccent(guided[i])) counted.Accent++;
            else if (IsLine(guided[i])) counted.Line++;
        }
    }

    private static bool Same(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.05f;

    private static bool IsAccent(Color p) => p.r > 0.9f && p.g > 0.85f && p.b < 0.7f;

    private static bool IsLine(Color p) => p.r > 0.75f && p.g < 0.35f && p.b < 0.35f;

    private IEnumerator Capture(string? fileName, System.Action<Color[]> read)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        _camera!.targetTexture = rt;
        yield return null;
        yield return null;
        _camera.Render();

        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        read(tex.GetPixels());

        if (fileName != null)
        {
            var dir = Path.Combine(Application.dataPath, "..", "test-results");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
        }

        _camera.targetTexture = null;
        Object.Destroy(rt);
        Object.Destroy(tex);
    }
}
