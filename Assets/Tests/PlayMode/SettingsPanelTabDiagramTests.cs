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
    private const int PanelW = 920;
    private const int PanelH = 640;
    private const string PagePath = "SettingsPanel/SettingsPanelBody/SettingsPanelBodyContent";


    /// <summary>
    /// Страница — это ПОДПИСЬ пункта навигации и ИМЯ её страницы, а не номер в списке. Девятая страница
    /// встала бы в середину, и снимки, выбиравшие страницу по индексу, разъехались бы на одну позицию и
    /// замерли под чужими именами (так уже было, когда вкладкой стало «Строительство»).
    ///
    /// Отсюда одна таблица: подпись → id страницы, а из id выводятся и имя файла снимка, и имя теста,
    /// который его снимает. Порядок в таблице — порядок пунктов навигации, и он сверяется с живой панелью в
    /// <see cref="EveryNavItem_HasASnapshotNamedAfterItsCaption"/>.
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
        public string PageNode => "Page_" + Page;
        public string Snapshot => "settings_page_" + Page + ".png";
        public string CaptureMethod => "Page" + char.ToUpperInvariant(Page[0]) + Page.Substring(1) + "_SavesPng";
    }

    private static readonly TabCase[] Tabs =
    {
        new TabCase("Общие", "general"),
        new TabCase("Проект", "project"),
        new TabCase("Вид", "view"),
        new TabCase("Управление", "control"),
        new TabCase("Свет", "light"),
        new TabCase("Фоторежим", "photo"),
        new TabCase("Строительство", "construction"),
        new TabCase("MCP", "mcp"),
        new TabCase("О программе", "about"),
    };

    private static TabCase Tab(string page)
    {
        var tab = Tabs.FirstOrDefault(t => t.Page == page);
        Assert.IsNotNull(tab, $"страницы {page} нет в таблице");
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
        ui.OpenTab(0);

        return (canvas, cam, ui);
    }

    private static string Caption(Transform tabButton)
    {
        var label = tabButton.GetComponentInChildren<TextMeshProUGUI>();
        return label == null ? "" : label.text.Replace("​", "");
    }

    private IEnumerable<Transform> TabButtons()
    {
        var nav = _canvasGo!.transform.Find(SettingsWindowPaths.Nav);
        Assert.IsNotNull(nav, "окно настроек не построилось — снимать нечего");
        foreach (Transform child in nav!)
            if (child.name.StartsWith(SettingsNav.ItemPrefix, System.StringComparison.Ordinal))
                yield return child;
    }

    /// <summary>
    /// Выбор страницы по ПОДПИСИ, а не по индексу: так же, как это делает человек, и так же, как
    /// <c>SettingsPanelUITests.SwitchPage_OnlyActivePageVisible</c>. Вставка страницы в середину навигации
    /// номера сдвигает, а подписи — нет.
    /// </summary>
    private void ClickTab(string caption)
    {
        foreach (var button in TabButtons())
        {
            if (Caption(button) != caption) continue;
            var btn = button.GetComponent<Button>();
            Assert.IsNotNull(btn, $"пункт «{caption}» без кнопки — нажать его нечем");
            btn!.onClick.Invoke();
            return;
        }
        Assert.Fail($"пункта навигации «{caption}» нет");
    }

    private string TheOnlyVisiblePage()
    {
        var content = _canvasGo!.transform.Find(PagePath);
        Assert.IsNotNull(content, "область прокрутки окна настроек: " + PagePath);

        var visible = new List<string>();
        foreach (Transform child in content!)
            if (child.name.StartsWith("Page_", System.StringComparison.Ordinal) && child.gameObject.activeSelf)
                visible.Add(child.name);

        Assert.AreEqual(1, visible.Count,
            "видна обязана быть ровно одна страница — иначе в снимок попадут параметры двух "
            + "страниц сразу. Активны: " + string.Join(", ", visible));
        return visible[0];
    }

    private IEnumerator CaptureTab(TabCase tab)
    {
        BuildPanel();
        ClickTab(tab.Caption);
        yield return null;

        Assert.AreEqual(tab.PageNode, TheOnlyVisiblePage(),
            $"снимок «{tab.Snapshot}» обязан показывать страницу «{tab.Caption}» "
            + $"({tab.PageNode}). Эталон, замерший под чужим именем, хуже красного теста: "
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
    /// Десятая страница обязана уронить ИМЕННО этот тест — на своей подписи, — а не молча перемешать чужие
    /// эталоны. Сторожатся три вещи разом: список пунктов навигации совпадает с таблицей <see cref="Tabs"/>
    /// подпись-в-подпись и по порядку; клик по подписи открывает ровно ту страницу, из id которой выведено
    /// имя файла снимка; у каждой строки таблицы есть свой снимающий тест.
    /// </summary>
    [UnityTest]
    public IEnumerator EveryNavItem_HasASnapshotNamedAfterItsCaption()
    {
        BuildPanel();
        yield return null;

        var onScreen = TabButtons().Select(Caption).ToList();
        CollectionAssert.AreEqual(Tabs.Select(t => t.Caption).ToList(), onScreen,
            "навигация и таблица снимков разошлись. Новая страница заводится в `Tabs` "
            + "вместе со своим тестом `Page{Имя}_SavesPng`, иначе снимки сдвинутся на позицию "
            + "и замрут под чужими именами. В навигации: " + string.Join(", ", onScreen));

        foreach (var tab in Tabs)
        {
            ClickTab(tab.Caption);
            Assert.AreEqual(tab.PageNode, TheOnlyVisiblePage(),
                $"клик по «{tab.Caption}» обязан открыть {tab.PageNode} — страницу, чей id стоит "
                + $"в имени снимка «{tab.Snapshot}»");

            var capture = GetType().GetMethod(tab.CaptureMethod,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(capture,
                $"у страницы «{tab.Caption}» нет снимающего теста {tab.CaptureMethod}(): "
                + $"эталон {tab.Snapshot} никто не обновит, и страница останется неснятой");
            Assert.IsNotEmpty(capture!.GetCustomAttributes(typeof(UnityTestAttribute), false),
                $"{tab.CaptureMethod}() существует, но не помечен [UnityTest] — прогон его "
                + $"не запустит, и эталон {tab.Snapshot} останется старым");
        }
    }

    // ── Снимки: одна вкладка — один тест, вкладка выбирается по подписи ─

    [UnityTest]
    public IEnumerator PageProject_SavesPng() => CaptureTab(Tab("project"));

    [UnityTest]
    public IEnumerator PageView_SavesPng() => CaptureTab(Tab("view"));

    [UnityTest]
    public IEnumerator PageConstruction_SavesPng() => CaptureTab(Tab("construction"));

    [UnityTest]
    public IEnumerator PageControl_SavesPng() => CaptureTab(Tab("control"));

    [UnityTest]
    public IEnumerator PageGeneral_SavesPng() => CaptureTab(Tab("general"));

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
    public IEnumerator PageControlConflict_SavesPng()
    {
        var built = BuildPanel();

        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings, "без настроек привязок нет — конфликтовать нечему");
        var taken = KeyBindingDefaults.PrimaryOf(InputAction.DuplicateSelected);
        settings!.KeyBindings.SetPrimaryBinding(InputAction.SaveProject, taken);

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

        Assert.AreEqual(InputBindingDisplay.Of(taken), other.text,
            "соседняя строка показывает тот самый аккорд, который мы отняли — иначе "
            + "конфликта в кадре нет и снимать нечего");
        Assert.AreEqual(other.text, clash.text,
            "обе стороны конфликта показывают ОДИН аккорд — на то он и конфликт");
        AssertColor(UIStyle.TextError, clash.color, "«Сохранить проект»");
        AssertColor(UIStyle.TextError, other.color, "«Дублировать»");
        AssertMarkerVisible("KbMark_SaveProject_P",
            "цвет не единственный носитель смысла: у конфликта есть ещё и маркер");
        AssertMarkerVisible("KbMark_DuplicateSelected_P", "маркер стоит у ОБЕИХ сторон конфликта");

        AssertColor(UIStyle.Text, calm.color,
            "«Удалить объект(ы)» ни с кем не конфликтует и обязана остаться обычной: "
            + "тест, в котором краснеет всё, зеленел бы и при полностью сломанной логике");
        AssertMarkerHidden("KbMark_DeleteSelected_P");
        AssertNoCaptionIsClipped();

        yield return CaptureAndSave("settings_page_control_conflict.png");
    }

    /// <summary>Тот же сторож для жестов мыши. Отдельным кадром, а не параметром к
    /// прошлому: жест идёт через другую ветку и захвата, и подписи
    /// (<c>InputBindingDisplay</c>), и «красное» у него уже однажды могло бы потеряться
    /// молча — конфликтов между клавишей и жестом модель не признаёт по построению,
    /// поэтому кадр обязан доказать, что внутри ОДНОГО рода входа краснеет как надо.</summary>
    [UnityTest]
    public IEnumerator PageControlGestureConflict_SavesPng()
    {
        var built = BuildPanel();

        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings, "без настроек привязок нет — конфликтовать нечему");
        var taken = KeyBindingDefaults.PrimaryOf(InputAction.CameraPan);
        Assume.That(taken.IsGesture, "предпосылка: панорамирование по умолчанию — жест мыши");
        settings!.KeyBindings.SetPrimaryBinding(InputAction.CameraOrbit, taken);

        ClickTab("Управление");
        built.ui.SetVisible(true);
        yield return null;

        ScrollRowIntoView("KbRow_CameraPan");
        yield return null;

        var scroll = BodyScroll();
        foreach (var row in new[] { "KbRow_CameraOrbit", "KbRow_CameraPan", "KbRow_CameraZoomWheel" })
            AssertRowIsInFrame(scroll, row);

        var clash = CellCaption("KbBtn_CameraOrbit_P");
        var other = CellCaption("KbBtn_CameraPan_P");
        var calm = CellCaption("KbBtn_CameraZoomWheel_P");

        Assert.AreEqual(InputBindingDisplay.Of(taken), other.text,
            "жест показывается по-русски, а маркер конфликта стоит своей дорожкой рядом");
        Assert.AreEqual(other.text, clash.text, "обе стороны конфликта показывают ОДИН жест");
        AssertColor(UIStyle.TextError, clash.color, "«Камера: поворот на месте»");
        AssertColor(UIStyle.TextError, other.color, "«Камера: панорамирование»");
        AssertMarkerVisible("KbMark_CameraOrbit_P",
            "у жеста маркер терялся: он был префиксом внутри подписи, а подпись занимала "
            + "ячейку целиком — именно этот случай кадр и обязан стеречь");
        AssertMarkerVisible("KbMark_CameraPan_P", "маркер стоит у ОБЕИХ сторон конфликта");

        AssertColor(UIStyle.Text, calm.color,
            "«Камера: зум колёсиком» ни с кем не конфликтует и обязана остаться обычной");
        AssertMarkerHidden("KbMark_CameraZoomWheel_P");
        AssertNoCaptionIsClipped();

        yield return CaptureAndSave("settings_page_control_gesture_conflict.png");
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
        var page = _canvasGo!.transform.Find(PagePath + "/Page_control");
        Assert.IsNotNull(page, "страницы «Управление» нет");
        var row = page!.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == rowName);
        Assert.IsNotNull(row, $"строки привязки «{rowName}» на странице нет");
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

    /// <summary>Единственное место, где «подпись не обрезана» можно спросить ЧЕСТНО:
    /// здесь текст меряет сам TMP настоящими метриками шрифта. Быстрые проверки в чистом
    /// слое считают раскладку домашней оценкой «доля кегля на знак», а она для кириллицы
    /// занижена — четыре итерации подряд кадр приезжал с многоточием, а быстрый набор
    /// оставался зелёным. Поэтому вопрос «влезло ли» задаётся ровно там, где рисуют.</summary>
    private void AssertNoCaptionIsClipped()
    {
        var panel = _canvasGo!.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel, "окно настроек не построилось");

        var clipped = new List<string>();
        int asked = 0;

        foreach (var caption in panel!.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!caption.name.StartsWith("KbBtn_", System.StringComparison.Ordinal)) continue;
            if (caption.font == null) continue;

            asked++;
            float needs = caption.GetPreferredValues(caption.text).x;
            float has = caption.rectTransform.rect.width;
            if (!KeybindingCellLayout.CaptionFits(needs, has + 0.5f))
                clipped.Add($"{caption.name}: «{caption.text}» просит {needs:F0} px, дано {has:F0}");
        }

        Assert.That(asked, Is.GreaterThan(0),
            "ни одной подписи привязки не нашлось — проверка зеленела бы вхолостую");
        CollectionAssert.IsEmpty(clipped,
            "подпись не помещается в свою ячейку и уедет в многоточие: человек увидит, ЧТО "
            + "конфликт есть, и не увидит, С ЧЕМ. " + string.Join(" | ", clipped));
    }

    /// <summary>Маркер спрашивается ПРЕДМЕТНО, а не по префиксу в тексте подписи: он
    /// перестал быть частью текста именно потому, что в этом виде его срезало вместе с
    /// подписью, и ни один тест этого не заметил.</summary>
    private void AssertMarkerVisible(string markerName, string why)
    {
        var marker = Marker(markerName);
        Assert.IsTrue(marker.gameObject.activeInHierarchy, why);
        Assert.AreEqual(KeybindingCaption.MarkerText, marker.text);
        Assert.That(marker.rectTransform.rect.width, Is.GreaterThan(0f),
            "нулевая ширина дорожки — тот же невидимый маркер, только тихо");

        if (marker.font != null)
            Assert.IsTrue(
                KeybindingCellLayout.MarkerFitsItsLane(marker.GetPreferredValues(marker.text).x),
                "маркер не помещается в свою дорожку по НАСТОЯЩИМ метрикам шрифта — правило "
                + "живёт в чистом слое, а число обязан давать тот, кто рисует");

        AssertColor(UIStyle.TextError, marker.color, "маркер конфликта");
    }

    private void AssertMarkerHidden(string markerName) =>
        Assert.IsFalse(Marker(markerName).gameObject.activeInHierarchy,
            "маркер конфликта у непричастной строки — та же поломка, что и лишняя краска");

    private TextMeshProUGUI Marker(string markerName)
    {
        var panel = _canvasGo!.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel, "окно настроек не построилось");

        var marker = panel!.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(t => t.name == markerName);
        Assert.IsNotNull(marker, $"маркера конфликта «{markerName}» в окне нет");
        return marker!;
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
    public IEnumerator PagePhoto_SavesPng() => CaptureTab(Tab("photo"));

    [UnityTest]
    public IEnumerator PageLight_SavesPng() => CaptureTab(Tab("light"));

    [UnityTest]
    public IEnumerator PageMcp_SavesPng() => CaptureTab(Tab("mcp"));

    [UnityTest]
    public IEnumerator PageAbout_SavesPng() => CaptureTab(Tab("about"));
}
