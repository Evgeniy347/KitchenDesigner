using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Положение окон проекта, факт «открыто/закрыто» и тумблеры вида
/// (тонировка, свет) живут в файле проекта.</summary>
public class ProjectWindowsTests
{
    /// <summary>Минимальное окно: RectTransform + видимость, без всего UI.</summary>
    private class FakeWindow : MonoBehaviour, IProjectWindow
    {
        public string Id = "fake";
        public bool Resizable;

        public string WindowId => Id;
        public RectTransform? WindowRect => (RectTransform)transform;
        public bool HeightAdjustable => Resizable;
        public bool IsVisible => gameObject.activeSelf;
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private FakeWindow CreateWindow(string id, Vector2 pos, float height, bool resizable = false)
    {
        var go = new GameObject(id, typeof(RectTransform));
        var w = go.AddComponent<FakeWindow>();
        w.Id = id;
        w.Resizable = resizable;
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300, height);
        ProjectWindows.Register(w);
        _spawned.Add(go);
        return w;
    }

    [SetUp]
    public void SetUp() => ProjectWindows.Clear();

    [TearDown]
    public void TearDown()
    {
        ProjectWindows.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        ElementHighlighter.TintEnabled = true;
        LightSourceElement.SetGlobalOn(true);
    }

    [Test]
    public void Capture_TakesPositionAndVisibility()
    {
        var open = CreateWindow("b_open", new Vector2(-120, -40), 660);
        var closed = CreateWindow("a_closed", new Vector2(15, 30), 400);
        closed.SetVisible(false);

        var states = ProjectWindows.Capture();

        Assert.AreEqual(new[] { "a_closed", "b_open" }, System.Array.ConvertAll(states, s => s.id),
            "порядок записей — по id: иначе JSON проекта зависел бы от порядка "
            + "построения UI и файл менялся бы сам по себе");
        Assert.IsFalse(states[0].visible);
        Assert.IsTrue(states[1].visible);
        Assert.AreEqual(-120f, states[1].x, 0.001f);
        Assert.AreEqual(-40f, states[1].y, 0.001f);
        Assert.IsTrue(open.IsVisible);
    }

    [Test]
    public void Capture_KeepsHeightOnlyForResizableWindow()
    {
        CreateWindow("fixed", Vector2.zero, 640);
        CreateWindow("resizable", Vector2.zero, 512, resizable: true);

        var states = ProjectWindows.Capture();

        Assert.AreEqual(0f, states[0].height, 0.001f, "фиксированная высота не сохраняется");
        Assert.AreEqual(512f, states[1].height, 0.001f);
    }

    [Test]
    public void Apply_RestoresPositionVisibilityAndHeight()
    {
        var window = CreateWindow("hierarchy", new Vector2(-300, -60), 660, resizable: true);
        window.SetVisible(false);

        ProjectWindows.Apply(new[]
        {
            new WindowStateData { id = "hierarchy", visible = true, x = 42f, y = -128f, height = 320f },
        });

        var rt = window.WindowRect!;
        Assert.IsTrue(window.IsVisible);
        Assert.AreEqual(new Vector2(42f, -128f), rt.anchoredPosition);
        Assert.AreEqual(320f, rt.sizeDelta.y, 0.001f);
    }

    [Test]
    public void Apply_IgnoresUnknownIdAndLeavesUnlistedWindowsAlone()
    {
        var window = CreateWindow("settings", new Vector2(7, 9), 900);

        ProjectWindows.Apply(new[]
        {
            new WindowStateData { id = "no-such-window", visible = false, x = 111f, y = 222f },
        });

        Assert.IsTrue(window.IsVisible, "окно без записи в файле остаётся как есть");
        Assert.AreEqual(new Vector2(7, 9), window.WindowRect!.anchoredPosition);
    }

    [Test]
    public void Apply_EmptyStates_ChangesNothing()
    {
        var window = CreateWindow("specification", new Vector2(5, 5), 640);

        ProjectWindows.Apply(null);
        ProjectWindows.Apply(new WindowStateData[0]);

        Assert.IsTrue(window.IsVisible);
        Assert.AreEqual(new Vector2(5, 5), window.WindowRect!.anchoredPosition);
    }

    [Test]
    public void DestroyedWindow_DropsOutOfRegistry()
    {
        var window = CreateWindow("errors", Vector2.zero, 640);
        Object.DestroyImmediate(window.gameObject);

        Assert.AreEqual(0, ProjectWindows.Capture().Length);
        Assert.DoesNotThrow(() => ProjectWindows.Apply(new[]
        {
            new WindowStateData { id = "errors", visible = true },
        }));
    }

    [Test]
    public void WindowsAndViewToggles_RoundTripThroughProjectFile()
    {
        var window = CreateWindow("dayNight", new Vector2(-10, -60), 272);
        ElementHighlighter.TintEnabled = false;
        LightSourceElement.SetGlobalOn(false);

        var data = SaveLoadManager.CaptureScene(new List<KitchenElement>());
        var json = SaveLoadManager.Serialize(data);

        // Состояние в приложении сбито — восстановление обязано вернуть его из файла.
        window.SetVisible(false);
        window.WindowRect!.anchoredPosition = Vector2.zero;
        ElementHighlighter.TintEnabled = true;
        LightSourceElement.SetGlobalOn(true);

        var restored = SaveLoadManager.Deserialize(json);
        SaveLoadManager.RestoreScene(restored!);

        Assert.IsFalse(ElementHighlighter.TintEnabled, "тонировка");
        Assert.IsFalse(LightSourceElement.GlobalOn, "свет");
        Assert.IsTrue(window.IsVisible, "окно снова открыто");
        Assert.AreEqual(new Vector2(-10, -60), window.WindowRect!.anchoredPosition);
    }

    [Test]
    public void OldSaveWithoutWindows_KeepsDefaults()
    {
        var window = CreateWindow("specification", new Vector2(3, 4), 640);

        // Файл старого формата: полей windows/tintEnabled/lightsOn в JSON нет.
        var restored = SaveLoadManager.Deserialize("{\"version\":" + AppConstants.SAVE_FORMAT_VERSION + "}");

        Assert.IsNotNull(restored);
        Assert.AreEqual(0, restored!.windows.Length);
        Assert.IsTrue(restored.tintEnabled, "дефолт приложения — тонировка включена");
        Assert.IsTrue(restored.lightsOn, "дефолт приложения — свет включён");

        SaveLoadManager.RestoreScene(restored);
        Assert.AreEqual(new Vector2(3, 4), window.WindowRect!.anchoredPosition);
    }

    private class UnbuiltWindow : MonoBehaviour, IProjectWindow
    {
        public string WindowId => "unbuilt";
        public RectTransform? WindowRect => null;
        public bool HeightAdjustable => false;
        public bool IsVisible => false;
        public void SetVisible(bool visible) { }
    }

    private class PositionWatchingWindow : MonoBehaviour, IProjectWindow
    {
        public Vector2 PositionWhenShown;

        public string WindowId => "watch";
        public RectTransform? WindowRect => (RectTransform)transform;
        public bool HeightAdjustable => false;
        public bool IsVisible => gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            PositionWhenShown = ((RectTransform)transform).anchoredPosition;
            gameObject.SetActive(visible);
        }
    }

    [Test]
    public void Capture_SkipsWindowThatIsNotBuiltYet()
    {
        var go = new GameObject("unbuilt", typeof(RectTransform));
        _spawned.Add(go);
        ProjectWindows.Register(go.AddComponent<UnbuiltWindow>());

        Assert.AreEqual(0, ProjectWindows.Capture().Length,
            "у непостроенного окна нет ни координат, ни размера — сохранять нечего, "
            + "а запись с нулями перекинула бы окно в угол при следующей загрузке");
    }

    [Test]
    public void Apply_RestoresPosition_BeforeShowingWindow()
    {
        var go = new GameObject("watch", typeof(RectTransform));
        _spawned.Add(go);
        var window = go.AddComponent<PositionWatchingWindow>();
        ((RectTransform)go.transform).anchoredPosition = new Vector2(999, 999);
        window.gameObject.SetActive(false);
        ProjectWindows.Register(window);

        ProjectWindows.Apply(new[]
        {
            new WindowStateData { id = "watch", visible = true, x = -10f, y = -60f },
        });

        Assert.AreEqual(new Vector2(-10f, -60f), window.PositionWhenShown,
            "координаты ставятся ДО показа: WindowScreenGuard клампит окно в "
            + "LateUpdate сразу после OnEnable, и по старым координатам он прижал бы "
            + "его к краю экрана");
    }

    [Test]
    public void WindowIds_AreStableSaveKeys()
    {
        var dir = KitchenDesigner.Tests.Geometry.RepoPaths.Subdir(
            "Assets", "Scripts", "Core", "UI");
        var files = System.IO.Directory.GetFiles(dir, "*.cs", System.IO.SearchOption.AllDirectories);
        Assert.Greater(files.Length, 10, "скан не нашёл исходников панелей — проверять было бы нечего");

        var ids = new List<string>();
        foreach (var file in files)
            foreach (System.Text.RegularExpressions.Match m in
                System.Text.RegularExpressions.Regex.Matches(
                    System.IO.File.ReadAllText(file), "WindowId\s*=>\s*\"([^\"]+)\""))
                ids.Add(m.Groups[1].Value);
        ids.Sort(System.StringComparer.Ordinal);

        Assert.AreEqual(
            new[] { "dayNight", "errors", "hierarchy", "projectInstructions", "settings", "specification" },
            ids.ToArray(),
            "WindowId — ключ в файле проекта: переименование обесценивает уже "
            + "сохранённые проекты (окно молча вернётся к дефолту), а новое окно "
            + "попадает в этот список осознанно");
    }
}
