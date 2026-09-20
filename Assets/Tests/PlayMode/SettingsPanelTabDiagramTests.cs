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
using KitchenDesigner.Core.Keybinding;
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

    /// <summary>
    /// Конфликт привязок — единственное, о чём вкладка «Управление» сообщает ЦВЕТОМ, и
    /// до этого снимка его не проверял никто: чистые тесты доказывают, что список
    /// конфликтов СЧИТАЕТСЯ верно, а между «`KeybindingConflicts` вернул пару» и
    /// «человек увидел красное» лежит весь слой раскраски — `KeybindingRowUI`, палитра,
    /// порядок refresh. Ровно тот шов, где фича умирает молча.
    ///
    /// Цвет в json-эталон не попадает (снимок пишет имена, тексты и геометрию), поэтому
    /// краску здесь спрашивают ассертами, а кадр остаётся для глаза. И спрашивают её
    /// ПАРОЙ ПРОТИВОПОЛОЖНЫХ ВХОДОВ: две конфликтующие ячейки обязаны покраснеть, а
    /// соседняя, ни с кем не конфликтующая, обязана остаться обычной. Без второй половины
    /// тест зеленел бы и в том случае, если бы красным красилось ВСЁ подряд.
    /// </summary>
    [UnityTest]
    public IEnumerator TabControlConflict_SavesPng()
    {
        var built = BuildPanel();

        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings, "без настроек привязок нет — конфликтовать нечему");
        var taken = KeyBindingDefaults.PrimaryOf(InputAction.DuplicateSelected);
        settings!.KeyBindings.SetPrimary(InputAction.SaveProject, taken);

        ClickTab("Управление");
        built.ui.SetVisible(true);
        yield return null;

        ScrollRowIntoView("KbRow_DuplicateSelected");
        yield return null;

        var scroll = BodyScroll();
        foreach (var row in new[]
                 {
                     "KbRow_DeleteSelected", "KbRow_DuplicateSelected", "KbRow_SaveProject",
                 })
            AssertRowIsInFrame(scroll, row);

        var clash = CellCaption("KbBtn_SaveProject_P");
        var other = CellCaption("KbBtn_DuplicateSelected_P");
        var calm = CellCaption("KbBtn_DeleteSelected_P");

        Assert.AreEqual("! " + KeyChordDisplay.Of(taken), other.text,
            "соседняя строка показывает тот самый аккорд, который мы отняли, уже с маркером — "
            + "иначе конфликта в кадре нет и снимать нечего");
        Assert.AreEqual(other.text, clash.text,
            "обе стороны конфликта показывают ОДИН аккорд — на то он и конфликт");
        AssertColor(UIStyle.HighlightError, clash.color, "«Сохранить проект»");
        AssertColor(UIStyle.HighlightError, other.color, "«Дублировать»");
        StringAssert.StartsWith("! ", clash.text,
            "цвет не единственный носитель смысла: у конфликта есть ещё и маркер");
        StringAssert.StartsWith("! ", other.text, "маркер стоит у ОБЕИХ сторон конфликта");

        AssertColor(UIStyle.Text, calm.color,
            "«Удалить объект(ы)» ни с кем не конфликтует и обязана остаться обычной: "
            + "тест, в котором краснеет всё, зеленел бы и при полностью сломанной логике");
        Assert.IsFalse(calm.text.StartsWith("!", System.StringComparison.Ordinal),
            "маркер конфликта у непричастной строки — та же поломка, что и лишняя краска");

        yield return CaptureAndSave("settings_tab_control_conflict.png");
    }

    private ScrollRect BodyScroll()
    {
        var body = _canvasGo!.transform.Find("SettingsPanel/SettingsPanelBody");
        Assert.IsNotNull(body, "области прокрутки окна настроек нет — подводить нечего");
        var scroll = body!.GetComponent<ScrollRect>();
        Assert.IsNotNull(scroll, "SettingsPanelBody без ScrollRect");
        return scroll!;
    }

    private RectTransform FindRow(string rowName)
    {
        var page = _canvasGo!.transform.Find(PagePath + "/Tab_Control");
        Assert.IsNotNull(page, "страницы вкладки «Управление» нет");
        var row = page!.Find(rowName) as RectTransform;
        Assert.IsNotNull(row, $"строки привязки «{rowName}» на вкладке нет");
        return row!;
    }

    /// <summary>Прокрутка считается от самой строки, а не подбирается числом: содержимое
    /// вкладки растёт с каждым новым действием, и застывшая доля прокрутки увела бы кадр
    /// с конфликта молча. Что строки ДЕЙСТВИТЕЛЬНО попали в кадр, проверяется отдельно —
    /// эталон, снятый мимо нужного места, хуже красного теста.</summary>
    private void ScrollRowIntoView(string rowName)
    {
        var scroll = BodyScroll();
        var content = scroll.content;
        var viewport = scroll.viewport;
        Assert.IsNotNull(content, "у прокрутки нет содержимого");
        Assert.IsNotNull(viewport, "у прокрутки нет окна просмотра");

        float rowY = content!.InverseTransformPoint(FindRow(rowName).position).y;
        float viewportH = viewport!.rect.height;
        float offset = -rowY - viewportH * 0.5f;
        float maxOffset = Mathf.Max(0f, content.rect.height - viewportH);

        content.anchoredPosition = new Vector2(
            content.anchoredPosition.x, Mathf.Clamp(offset, 0f, maxOffset));
    }

    private void AssertRowIsInFrame(ScrollRect scroll, string rowName)
    {
        var visible = WorldRectOf(scroll.viewport);
        var row = WorldRectOf(FindRow(rowName));

        Assert.IsTrue(row.yMin >= visible.yMin && row.yMax <= visible.yMax,
            $"строка «{rowName}» не попала в кадр (строка {row.yMin:F0}..{row.yMax:F0}, "
            + $"окно {visible.yMin:F0}..{visible.yMax:F0}). Снимок без обеих конфликтующих "
            + "строк и без одной спокойной ничего не доказывает.");
    }

    private static Rect WorldRectOf(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private TextMeshProUGUI CellCaption(string buttonName)
    {
        var panel = _canvasGo!.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel, "окно настроек не построилось");

        var button = panel!.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.name == buttonName);
        Assert.IsNotNull(button, $"ячейки привязки «{buttonName}» в окне нет");

        var caption = button!.GetComponentInChildren<TextMeshProUGUI>(true);
        Assert.IsNotNull(caption, $"у ячейки «{buttonName}» нет подписи");
        return caption!;
    }

    private static void AssertColor(Color expected, Color actual, string what)
    {
        bool same = Mathf.Approximately(expected.r, actual.r)
            && Mathf.Approximately(expected.g, actual.g)
            && Mathf.Approximately(expected.b, actual.b)
            && Mathf.Approximately(expected.a, actual.a);

        Assert.IsTrue(same,
            $"цвет ячейки {what}: ожидался {expected}, на экране {actual}. Красный во "
            + "вкладке значит ровно одно — конфликт привязки.");
    }

    [UnityTest]
    public IEnumerator TabPhoto_SavesPng() => CaptureTab(Tab("Tab_Photo"));

    [UnityTest]
    public IEnumerator TabLight_SavesPng() => CaptureTab(Tab("Tab_Light"));

    [UnityTest]
    public IEnumerator TabMcp_SavesPng() => CaptureTab(Tab("Tab_Mcp"));

    [UnityTest]
    public IEnumerator TabAbout_SavesPng() => CaptureTab(Tab("Tab_About"));
}
