using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

public class SettingsPanelTabDiagramTests
{
    private const int PanelW = 600;
    private const int PanelH = 900;
    private const string PagePath = "SettingsPanel/SettingsPanelBody/SettingsPanelBodyContent";

    /// <summary>
    /// Вкладка — это ПОДПИСЬ на кнопке и ИМЯ её страницы, а не номер на полосе. Восьмая
    /// вкладка «Строительство» (3ffa6d09) встала третьей, и снимки, выбиравшие вкладку по
    /// индексу `Tab_N`, разъехались на одну позицию: `ui_settings_tab_control` снял страницу
    /// строительства, `..._light` — фотографию, `..._mcp` — свет, `..._photo` — управление,
    /// `..._about` — MCP, а страница «О программе» не снималась вовсе. Прими такие кандидаты —
    /// и пять эталонов замрут под чужими именами.
    ///
    /// Отсюда одна таблица: подпись → имя страницы, а из имени страницы выводятся и имя файла
    /// снимка, и имя теста, который его снимает. Порядок в таблице — порядок кнопок на полосе,
    /// и он сверяется с живой панелью в
    /// <see cref="EveryTabOnTheStrip_HasASnapshotNamedAfterItsCaption"/>.
    /// </summary>
    private sealed class TabCase
    {
        public TabCase(string caption, string page)
        {
            Caption = caption;
            Page = page;
        }

        public string Caption { get; }
        public string Page { get; }
        private string Key => Page.Substring("Tab_".Length);
        public string Snapshot => "settings_tab_" + Key.ToLowerInvariant() + ".png";
        public string CaptureMethod => "Tab" + Key + "_SavesPng";
    }

    private static readonly TabCase[] Tabs =
    {
        new TabCase("Проект", "Tab_Project"),
        new TabCase("Вид", "Tab_View"),
        new TabCase("Строительство", "Tab_Construction"),
        new TabCase("Управление", "Tab_Control"),
        new TabCase("Фото режим", "Tab_Photo"),
        new TabCase("Свет", "Tab_Light"),
        new TabCase("MCP", "Tab_Mcp"),
        new TabCase("О программе", "Tab_About"),
    };

    private static TabCase Tab(string page)
    {
        var tab = Tabs.FirstOrDefault(t => t.Page == page);
        Assert.IsNotNull(tab, $"страницы {page} нет в таблице вкладок");
        return tab!;
    }

    private GameObject? _canvasGo;
    private GameObject? _camGo;
    private GameObject? _eventSystem;
    private KitchenSettingsData? _settingsBackup;
    private int? _mcpPortBackup;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_canvasGo != null) Object.Destroy(_canvasGo);
        if (_camGo != null) Object.Destroy(_camGo);
        if (_eventSystem != null) Object.Destroy(_eventSystem);

        var s = KitchenSettings.Instance;
        if (s != null && _settingsBackup != null) s.ApplyFrom(_settingsBackup);
        _settingsBackup = null;

        McpBridgeStatus.TestPort = _mcpPortBackup;
        McpBridgeStatus.Forget();
        yield return null;
    }

    private (Canvas canvas, Camera cam, SettingsPanelUI ui) BuildPanel()
    {
        // Детерминизм снапшота: дефолтные настройки независимо от прочих тестов.
        // Порт MCP тоже: соседние тесты уводят его на 19337, и вкладка «MCP»
        // показала бы чужое число.
        _mcpPortBackup = McpBridgeStatus.TestPort;
        McpBridgeStatus.TestPort = McpBridgeStatus.DefaultPort;
        McpBridgeStatus.Forget();

        var settings = KitchenSettings.Instance;
        _settingsBackup = settings != null ? settings.ToData() : null;
        if (settings != null) settings.ResetToDefaults();

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

    private static string Caption(Transform tabButton)
    {
        var label = tabButton.GetComponentInChildren<TextMeshProUGUI>();
        return label == null ? "" : label.text.Replace("​", "");
    }

    private IEnumerable<Transform> TabButtons()
    {
        var panel = _canvasGo!.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel, "окно настроек не построилось — снимать нечего");
        foreach (Transform child in panel!)
            if (child.name.StartsWith("Tab_", System.StringComparison.Ordinal))
                yield return child;
    }

    /// <summary>
    /// Выбор вкладки по ПОДПИСИ, а не по индексу: так же, как это делает человек, и так же,
    /// как `SettingsPanelUITests.SwitchTab_OnlyActivePageVisible` (66bb02b5). Вставка вкладки
    /// в середину полосы номера сдвигает, а подписи — нет.
    /// </summary>
    private void ClickTab(string caption)
    {
        foreach (var button in TabButtons())
        {
            if (Caption(button) != caption) continue;
            var btn = button.GetComponent<Button>();
            Assert.IsNotNull(btn, $"вкладка «{caption}» без кнопки — нажать её нечем");
            btn!.onClick.Invoke();
            return;
        }
        Assert.Fail($"кнопки вкладки «{caption}» на полосе нет");
    }

    private string TheOnlyVisiblePage()
    {
        var content = _canvasGo!.transform.Find(PagePath);
        Assert.IsNotNull(content, "область прокрутки окна настроек: " + PagePath);

        var visible = new List<string>();
        foreach (Transform child in content!)
            if (child.name.StartsWith("Tab_", System.StringComparison.Ordinal) && child.gameObject.activeSelf)
                visible.Add(child.name);

        Assert.AreEqual(1, visible.Count,
            "видна обязана быть ровно одна страница — иначе в снимок попадут параметры двух "
            + "вкладок сразу. Активны: " + string.Join(", ", visible));
        return visible[0];
    }

    private IEnumerator CaptureTab(TabCase tab)
    {
        BuildPanel();
        ClickTab(tab.Caption);
        yield return null;

        Assert.AreEqual(tab.Page, TheOnlyVisiblePage(),
            $"снимок «{tab.Snapshot}» обязан показывать страницу вкладки «{tab.Caption}» "
            + $"({tab.Page}). Эталон, замерший под чужим именем, хуже красного теста: "
            + "следующий человек будет отлаживать, почему в снимке «Свет» настройки фотографии.");

        yield return CaptureAndSave(tab.Snapshot);
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

        var jsonPath = Path.ChangeExtension(path, ".json");
        UiSnapshotEngine.CaptureVerified(_canvasGo, jsonPath);
    }

    // ── Сенсор: имя снимка обязано сойтись с подписью вкладки ───────────

    /// <summary>
    /// Девятая вкладка обязана уронить ИМЕННО этот тест — на своей подписи, — а не молча
    /// перемешать чужие эталоны. Сторожатся три вещи разом: полоса кнопок совпадает с
    /// таблицей <see cref="Tabs"/> подпись-в-подпись и по порядку; клик по подписи открывает
    /// ровно ту страницу, из имени которой выведено имя файла снимка; у каждой строки таблицы
    /// есть свой снимающий тест. Раньше не сторожилось ничто — сдвиг пяти эталонов поймал
    /// человек, читавший дифф глазами.
    /// </summary>
    [UnityTest]
    public IEnumerator EveryTabOnTheStrip_HasASnapshotNamedAfterItsCaption()
    {
        BuildPanel();
        yield return null;

        var onScreen = TabButtons().Select(Caption).ToList();
        CollectionAssert.AreEqual(Tabs.Select(t => t.Caption).ToList(), onScreen,
            "полоса вкладок и таблица снимков разошлись. Новая вкладка заводится в `Tabs` "
            + "вместе со своим тестом `Tab{Имя}_SavesPng`, иначе снимки сдвинутся на позицию "
            + "и замрут под чужими именами. На полосе: " + string.Join(", ", onScreen));

        foreach (var tab in Tabs)
        {
            ClickTab(tab.Caption);
            Assert.AreEqual(tab.Page, TheOnlyVisiblePage(),
                $"клик по «{tab.Caption}» обязан открыть {tab.Page} — страницу, чьё имя стоит "
                + $"в имени снимка «{tab.Snapshot}»");

            var capture = GetType().GetMethod(tab.CaptureMethod,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(capture,
                $"у вкладки «{tab.Caption}» нет снимающего теста {tab.CaptureMethod}(): "
                + $"эталон {tab.Snapshot} никто не обновит, и вкладка останется неснятой");
            Assert.IsNotEmpty(capture!.GetCustomAttributes(typeof(UnityTestAttribute), false),
                $"{tab.CaptureMethod}() существует, но не помечен [UnityTest] — прогон его "
                + $"не запустит, и эталон {tab.Snapshot} останется старым");
        }
    }

    // ── Снимки: одна вкладка — один тест, вкладка выбирается по подписи ─

    [UnityTest]
    public IEnumerator TabProject_SavesPng() => CaptureTab(Tab("Tab_Project"));

    [UnityTest]
    public IEnumerator TabView_SavesPng() => CaptureTab(Tab("Tab_View"));

    [UnityTest]
    public IEnumerator TabConstruction_SavesPng() => CaptureTab(Tab("Tab_Construction"));

    [UnityTest]
    public IEnumerator TabControl_SavesPng() => CaptureTab(Tab("Tab_Control"));

    [UnityTest]
    public IEnumerator TabPhoto_SavesPng() => CaptureTab(Tab("Tab_Photo"));

    [UnityTest]
    public IEnumerator TabLight_SavesPng() => CaptureTab(Tab("Tab_Light"));

    [UnityTest]
    public IEnumerator TabMcp_SavesPng() => CaptureTab(Tab("Tab_Mcp"));

    [UnityTest]
    public IEnumerator TabAbout_SavesPng() => CaptureTab(Tab("Tab_About"));
}
