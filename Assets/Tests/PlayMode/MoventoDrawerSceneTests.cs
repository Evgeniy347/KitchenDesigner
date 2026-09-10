using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Tests;

/// <summary>
/// PlayMode: собирает короб 540×720×600 и вставляет два ящика Movento —
/// нижний глубокий и верхний двойной (внешний + внутренний). Проверяет
/// структуру (3 короба Movento, пара связана), раскладку спецификации на
/// детали и сохраняет изометрический скриншот сцены.
/// </summary>
public class MoventoDrawerSceneTests : ElementFrameTests
{
    private const int RenderW = 640;
    private const int RenderH = 640;
    private const float Mm = AppConstants.MM_TO_UNITS;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera!.tag = "MainCamera";
        _mainCamera.AddComponent<Camera>();
        _mainCamera.transform.position = new Vector3(0f, 3f, -5f);
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
        foreach (var go in _spawned)
            if (go != null) Object.Destroy(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    private void Part(string name, Vector3Int dimsMM, Vector3 posMM)
    {
        var go = ElementFactory.CreatePart(dimsMM, name, posMM * Mm);
        _spawned.Add(go);
    }

    private DrawerElement Movento(string name, DrawerType type, int nl, int lw, Vector3 posMM)
    {
        var go = ElementFactory.CreateDrawer(type, nl, DrawerColor.Anthracite, lw, name, posMM * Mm, DrawerSystem.Movento);
        _spawned.Add(go);
        return go.GetComponent<DrawerElement>();
    }

    [UnityTest]
    public IEnumerator MoventoDouble_InCabinet_540x720x600()
    {
        // Корпус 600×720×540, боковины 16 мм → LW = 568, глубина под NL500.
        const int W = 600, H = 720, D = 540, t = 16;
        const int lw = W - 2 * t; // 568
        const int nl = 500;
        // Центр боковины: W/2 − t/2. Было W/2 − t, и боковины стояли на 8 мм
        // внутри проёма — каждая пересекала каждый ящик, отчего кадр и был
        // розовым (COL-01, «детали пересекаются в объёме»). Дно и крышка
        // оставались зелёными, и это ровно та подсказка, которую видно на PNG.
        float halfX = W / 2f - t / 2f;

        // Каркас: дно, крышка, две боковины.
        Part("Дно_корпуса",   new Vector3Int(W, t, D),          new Vector3(0, t / 2f, 0));
        Part("Крышка",        new Vector3Int(W, t, D),          new Vector3(0, H - t / 2f, 0));
        Part("Бок_L",         new Vector3Int(t, H - 2 * t, D),  new Vector3(-halfX, H / 2f, 0));
        Part("Бок_R",         new Vector3Int(t, H - 2 * t, D),  new Vector3(halfX, H / 2f, 0));

        // Нижний глубокий ящик (тип D) и верхний под двойной (тип C).
        int lowerH = DrawerConstants.GetMinOpeningHeight(DrawerType.D); // 230
        int upperH = DrawerConstants.GetMinOpeningHeight(DrawerType.C); // 198
        float lowerY = t + lowerH / 2f;                 // над дном корпуса
        float upperY = t + lowerH + upperH / 2f;        // над нижним ящиком

        var lower = Movento("Нижний", DrawerType.D, nl, lw, new Vector3(0, lowerY, 0));
        var top = Movento("Верхний", DrawerType.C, nl, lw, new Vector3(0, upperY, 0));
        yield return null;

        // Делаем верхний двойным — над ним появляется внутренний ящик (тип A).
        var inner = DrawerLinks.CreatePair(top);
        Assert.IsNotNull(inner, "CreatePair должен создать внутренний ящик");
        _spawned.Add(inner!.gameObject);
        yield return null;
        yield return null;

        // ── Структура ────────────────────────────────────────────
        var drawers = Object.FindObjectsByType<DrawerElement>(FindObjectsSortMode.None);
        Assert.AreEqual(3, drawers.Length, "нижний + двойной (внешний+внутренний) = 3 короба");
        foreach (var d in drawers)
            Assert.AreEqual(DrawerSystem.Movento, d.System, "все короба — Movento");

        Assert.IsTrue(top.IsDouble, "верхний помечен двойным");
        Assert.AreSame(inner, top.FindPaired(), "пара верхнего — внутренний ящик");
        Assert.IsTrue(inner.IsUpperDrawer, "внутренний — верхний в паре");
        Assert.IsFalse(lower.IsDouble, "нижний одиночный");

        // ── Спецификация раскладывается на детали ────────────────
        var spec = SpecificationManager.Build(drawers.Cast<KitchenElement>().ToList());
        Assert.IsTrue(spec.lines.Count > 0, "спецификация не пуста");
        var partSuffixes = new HashSet<string>
        {
            MoventoDrawerMesh.SUFFIX_SIDE,
            MoventoDrawerMesh.SUFFIX_FRONT,
            MoventoDrawerMesh.SUFFIX_BACK,
            MoventoDrawerMesh.SUFFIX_BOTTOM,
        };
        foreach (var line in spec.lines)
            Assert.IsTrue(partSuffixes.Contains(line.name),
                $"каждая позиция — деталь короба, названная по детали: {line.name}");
        Assert.IsTrue(spec.lines.Any(l => l.name == MoventoDrawerMesh.SUFFIX_BOTTOM), "есть дно");
        Assert.IsTrue(spec.lines.Any(l => l.name == MoventoDrawerMesh.SUFFIX_SIDE), "есть боковины");

        // ── Скриншот ─────────────────────────────────────────────
        // Про нарушения расстановки спрашивает сама съёмка: валидационный тон в
        // кадровых наборах выключен (ElementFrameTests), поэтому «кадр вышел
        // розовым» больше не сигнал, и вопрос валидатору задан ПЕРЕД каждым
        // кадром без исключения — см. ElementFrameTests.CaptureFramePng.
        KitchenSettings.Instance.NormalView.edgeOutline = true;
        yield return RenderIso(new Vector3(0, H / 2f, 0) * Mm,
            new Vector3(W, H, D) * Mm, "iso_movento_double_540x720x600.png");
    }

    private IEnumerator RenderIso(Vector3 center, Vector3 size, string fileName)
    {
        var camGo = new GameObject("IsoCam");
        _spawned.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        var isoDir = new Vector3(0.5f, 0.4f, -0.866f).normalized;
        float maxDim = Mathf.Max(size.x, size.y, size.z);
        camGo.transform.position = center + isoDir * Mathf.Max(maxDim * 2.2f, 0.5f);
        camGo.transform.LookAt(center);

        yield return CaptureFramePng(cam, fileName, RenderW, RenderH);
    }
}
