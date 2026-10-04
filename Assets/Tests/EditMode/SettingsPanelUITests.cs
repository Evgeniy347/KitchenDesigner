using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

public class SettingsPanelUITests
{
    private Canvas? _canvas;
    private SettingsPanelUI? _ui;

    // Страницы лежат НЕ прямо на панели, а в области прокрутки окна. Путь собран в
    // SettingsWindowPaths намеренно — когда структура окна поменяется снова, править
    // придётся одну строку, а не сорок.
    private static readonly string[] PageIds = SettingsPanelUI.PageOrder;

    private ProjectLoadStateGuard? _globals;

    /// <summary>`SettingsPanelUI.Build` стоит 0,10 с, а тестов в классе пятьдесят — пять
    /// секунд стены на пересборку одного и того же окна. Панель строится ОДИН раз, а
    /// потестовым остаётся то, что тесты действительно портят: глобальные настройки
    /// (<see cref="ProjectLoadStateGuard"/>), режим редактора и состояние самой панели —
    /// открытая страница и значения виджетов.</summary>
    [OneTimeSetUp]
    public void BuildPanelOnce()
    {
        var go = new GameObject("TestCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas!.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _ui = _canvas!.gameObject.AddComponent<SettingsPanelUI>();
        _ui!.Build(_canvas!.transform);
    }

    /// <summary>Панель обязана быть УНИЧТОЖЕНА, а не просто забыта: `SettingsViewTab`
    /// подписан на статический `EditModeManager.Changed`, `SettingsPhotoTab` — на
    /// `PhotoMode.Changed`, и отписка живёт только в `OnDestroy`. Уцелевшая панель
    /// стреляла бы мёртвым делегатом в тестах соседних классов, которые про настройки
    /// ничего не знают.</summary>
    [OneTimeTearDown]
    public void DestroyPanelOnce()
    {
        EditModeManager.Reset();
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        _canvas = null;
        _ui = null;

        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
    }

    [SetUp]
    public void Setup()
    {
        // Тесты крутят восемнадцать виджетов и пишут прямо в KitchenSettings.Instance,
        // а откатывали это руками и не полностью. Снимок глобального состояния снимается
        // ДО первой правки теста.
        _globals = ProjectLoadStateGuard.Capture();

        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null) events.SetSelectedGameObject(null);

        // Навигация общая: тест, кликнувший «О программе», оставил бы её открытой
        // следующему. Приводим панель к тому же виду, в котором её оставляет Build.
        _ui!.OpenTab(0);

        // SetVisible(true) — ЕДИНСТВЕННЫЙ путь к SyncFromSettings: без него виджеты не
        // перечитают настройки и тест увидит значения предыдущего. Закрываем сразу же —
        // после Build панель тоже спрятана, и это проверяет Panel_StartsHidden.
        _ui!.SetVisible(true);
        _ui!.SetVisible(false);
    }

    [TearDown]
    public void TearDown()
    {
        // Режим редактора — глобальное состояние: тест, который его крутит,
        // обязан вернуть исходное (см. правила снапшотов в AGENTS.md).
        EditModeManager.Reset();
        _globals!.Restore();
        _globals = null;
    }

    // ── Build / smoke ───────────────────────────────────────

    /// <summary>Сборку проверяет ОТДЕЛЬНАЯ панель на своём холсте, а не общая: `Build`
    /// не убирает прежний корень, он создаёт рядом второй «SettingsPanel», и все
    /// последующие `Find("SettingsPanel")` отдавали бы осиротевший — с разобранными
    /// вкладками и без связи с `_ui`.</summary>
    [Test]
    public void Build_DoesNotThrow()
    {
        var probe = NewProbePanel(out var canvas);
        try
        {
            Assert.DoesNotThrow(() => probe.Build(canvas.transform));
            Assert.IsNotNull(canvas.transform.Find("SettingsPanel"), "панель собралась");
        }
        finally
        {
            Object.DestroyImmediate(canvas.gameObject);
        }
    }

    private static SettingsPanelUI NewProbePanel(out Canvas canvas)
    {
        var go = new GameObject("ProbeCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return go.AddComponent<SettingsPanelUI>();
    }

    [Test]
    public void Panel_Exists_WithCorrectSize()
    {
        var panel = _canvas!.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel);
        var rt = panel.GetComponent<RectTransform>();
        Assert.AreEqual(UIStyle.SettingsSize.x, rt.sizeDelta.x, 0.01f);
        Assert.AreEqual(UIStyle.SettingsSize.y, rt.sizeDelta.y, 0.01f);
        Assert.AreEqual(920f, rt.sizeDelta.x, 0.01f, "docs/ui-redesign/settings.md: 920×640");
        Assert.AreEqual(640f, rt.sizeDelta.y, 0.01f);
    }

    [Test]
    public void Title_Exists_WithCorrectText()
    {
        var title = _canvas!.transform.Find("SettingsPanel/SettingsPanelTitle");
        Assert.IsNotNull(title);
        var label = title.GetComponent<TextMeshProUGUI>();
        Assert.AreEqual("Настройки", label.text.Replace("\u200b", ""));
        Assert.AreEqual(UIStyle.FontWindowTitle, label.fontSize);
    }

    // ── Navigation ──────────────────────────────────────────

    private static readonly string[] NavCaptions =
        { "Общие", "Проект", "Вид", "Управление", "Свет", "Фоторежим", "Строительство", "MCP", "О программе" };

    private List<Transform> NavItems()
    {
        var nav = _canvas!.transform.Find(SettingsWindowPaths.Nav);
        Assert.IsNotNull(nav, "левой навигации нет: " + SettingsWindowPaths.Nav);
        return nav!.Cast<Transform>().Where(c => c.name.StartsWith("NavItem_", System.StringComparison.Ordinal)).ToList();
    }

    [Test]
    public void EveryPage_HasANavItem_InTheOrderOfTheMockup()
    {
        var items = NavItems();
        Assert.AreEqual(PageIds.Length, items.Count, "пунктов навигации столько же, сколько страниц");
        for (int i = 0; i < PageIds.Length; i++)
            Assert.AreEqual("NavItem_" + PageIds[i], items[i].name, "порядок пунктов — порядок страниц");
    }

    [Test]
    public void NavItems_HaveCorrectLabels()
    {
        var items = NavItems();
        for (int i = 0; i < NavCaptions.Length; i++)
        {
            var label = items[i].GetComponentInChildren<TextMeshProUGUI>();
            Assert.AreEqual(NavCaptions[i], label.text.Replace("\u200b", ""));
        }
    }

    [Test]
    public void TabPages_Exist()
    {
        foreach (var id in PageIds)
            Assert.IsNotNull(_canvas!.transform.Find(SettingsWindowPaths.Page(id)), $"страница {id} должна существовать");
    }

    [Test]
    public void SwitchPage_OnlyActivePageVisible()
    {
        // Страницы — по имени, а не по индексу: вставка страницы в середину навигации
        // сдвигает номера, а подписи и id — нет. Предмет проверки — «видна ровно одна страница»,
        // и он спрашивается у ВСЕХ страниц сразу, а не у трёх выбранных руками.
        var pages = TabPages();
        Assert.AreEqual(PageIds.Length, pages.Count,
            "страниц столько же, сколько пунктов навигации: " + string.Join(", ", pages.Select(p => p.name)));

        Assert.AreEqual("Page_general", TheOnlyVisiblePage(pages), "по умолчанию открыта первая страница — «Общие»");

        ClickTab("О программе");
        Assert.AreEqual("Page_about", TheOnlyVisiblePage(pages),
            "после клика по «О программе» видна её страница и никакая другая");

        ClickTab("Строительство");
        Assert.AreEqual("Page_construction", TheOnlyVisiblePage(pages),
            "и остальные страницы переключаются так же");
    }

    private List<GameObject> TabPages()
    {
        var content = _canvas!.transform.Find(SettingsWindowPaths.Body.TrimEnd('/'));
        Assert.IsNotNull(content, "область прокрутки окна настроек: " + SettingsWindowPaths.Body);
        var pages = new List<GameObject>();
        foreach (Transform child in content!)
            if (child.name.StartsWith("Page_", System.StringComparison.Ordinal))
                pages.Add(child.gameObject);
        return pages;
    }

    private static string TheOnlyVisiblePage(IReadOnlyList<GameObject> pages)
    {
        var visible = pages.Where(p => p.activeSelf).Select(p => p.name).ToList();
        Assert.AreEqual(1, visible.Count,
            "видна обязана быть ровно одна страница — иначе параметры двух страниц наложатся "
            + "друг на друга прямо в окне. Активны: " + string.Join(", ", visible));
        return visible[0];
    }

    private void ClickTab(string caption)
    {
        foreach (var item in NavItems())
        {
            var label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null || label.text.Replace("\u200b", "") != caption) continue;
            item.GetComponent<Button>().onClick.Invoke();
            return;
        }
        Assert.Fail($"пункта навигации «{caption}» нет");
    }

    [Test]
    public void ActiveNavItem_IsMarkedWithTheAccentBar()
    {
        var items = NavItems();
        Assert.IsTrue(items[0].Find(SettingsNav.BarNode).gameObject.activeSelf, "открытая страница отмечена полосой");
        AssertColorEqual(UIStyle.AccentSubtle, items[0].GetComponent<Image>().color);
        Assert.IsFalse(items[1].Find(SettingsNav.BarNode).gameObject.activeSelf, "соседняя — нет");

        items[1].GetComponent<Button>().onClick.Invoke();

        Assert.IsFalse(items[0].Find(SettingsNav.BarNode).gameObject.activeSelf);
        Assert.IsTrue(items[1].Find(SettingsNav.BarNode).gameObject.activeSelf);
        AssertColorEqual(UIStyle.AccentSubtle, items[1].GetComponent<Image>().color);
    }

    // ── Visibility ──────────────────────────────────────────

    [Test]
    public void Panel_StartsHidden()
    {
        var panel = _canvas!.transform.Find("SettingsPanel");
        Assert.IsFalse(panel.gameObject.activeSelf);
    }

    [Test]
    public void SetVisible_ShowsAndHides()
    {
        var panel = _canvas!.transform.Find("SettingsPanel").gameObject;
        _ui!.SetVisible(true);
        Assert.IsTrue(panel.activeSelf);
        _ui!.SetVisible(false);
        Assert.IsFalse(panel.activeSelf);
    }

    [Test]
    public void Toggle_FlipsVisibility()
    {
        var panel = _canvas!.transform.Find("SettingsPanel").gameObject;
        Assert.IsFalse(panel.activeSelf);
        _ui!.Toggle();
        Assert.IsTrue(panel.activeSelf);
        _ui!.Toggle();
        Assert.IsFalse(panel.activeSelf);
    }

    [Test]
    public void SetVisible_NullRoot_DoesNotThrow()
    {
        var go = new GameObject("Temp");
        var ui = go.AddComponent<SettingsPanelUI>();
        Assert.DoesNotThrow(() => ui.SetVisible(true));
        Assert.DoesNotThrow(() => ui.Toggle());
        Object.DestroyImmediate(go);
    }

    // ── Close button ────────────────────────────────────────

    [Test]
    public void CloseButton_Exists_AndIsTheOnlyDoor()
    {
        var btn = _canvas!.transform.Find("SettingsPanel/" + WindowChrome.CloseButtonName);
        Assert.IsNotNull(btn);

        string close = Loc.T("common.close");
        var duplicates = _canvas!.transform.Find("SettingsPanel").GetComponentsInChildren<Button>(true)
            .Where(b => b.name != WindowChrome.CloseButtonName)
            .Where(b => b.GetComponentsInChildren<TMP_Text>(true).Any(l => l.text == close))
            .Select(b => b.name).ToList();
        Assert.IsEmpty(duplicates, "«Закрыть» рядом с × — две двери с одним смыслом (D5): " + string.Join(", ", duplicates));
    }

    [Test]
    public void CloseButton_HidesPanel()
    {
        var panel = _canvas!.transform.Find("SettingsPanel").gameObject;
        _ui!.SetVisible(true);
        Assert.IsTrue(panel.activeSelf);

        var btn = _canvas!.transform.Find("SettingsPanel/" + WindowChrome.CloseButtonName).GetComponent<Button>();
        btn.onClick.Invoke();
        Assert.IsFalse(panel.activeSelf);
    }

    // ── Project tab: toggle rows ────────────────────────────

    [Test]
    public void ProjectTab_HasAllToggleRows()
    {
        string[] expectedToggles =
        {
            "Сетка", "Привязка к деталям", "Блокировать недопустимые изменения", "Автосохранение",
            "Пространственная сетка",
            "Свободное панорамирование"
        };

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));

        foreach (var label in expectedToggles)
        {
            var row = project.Find($"Row_{label}");
            Assert.IsNotNull(row, $"Toggle row '{label}' should exist");

            var toggleObj = row.Find($"Sw_{label}");
            Assert.IsNotNull(toggleObj, $"Toggle '{label}' should exist");

            var toggle = toggleObj.GetComponent<Toggle>();
            Assert.IsNotNull(toggle, $"Toggle component on '{label}' should exist");
        }
    }

    /// <summary>Всё, что описывает «что показывать в сцене», собрано на странице
    /// «Вид»: стены, объекты, контуры и свет — это один пресет режима, а не
    /// правила работы с деталями.</summary>
    [Test]
    public void ViewTab_HasAllViewRows()
    {
        string[] expectedToggles =
        {
            "Стены", "Контур стен", "Опускать ближние стены", "Опускать все стены",
            "Скрывать окна и двери",
            "Объекты", "Контур объектов", "Скрыть источники света"
        };

        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));

        foreach (var label in expectedToggles)
        {
            Assert.IsNotNull(view.Find($"Row_{label}"), $"'{label}' должен быть на странице «Вид»");
            Assert.IsNull(project.Find($"Row_{label}"), $"'{label}' больше не на странице «Проект»");
        }
    }

    /// <summary>Переключатель пресетов: на странице правится набор для одного
    /// режима, и он сам встаёт на пресет текущего режима.</summary>
    [Test]
    public void ViewTab_PresetSwitch_FollowsEditMode()
    {
        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        var normalBtn = view.Find("Row_ViewPreset/ViewPreset/ViewPreset_0");
        var roomBtn = view.Find("Row_ViewPreset/ViewPreset/ViewPreset_1");
        var photoBtn = view.Find("Row_ViewPreset/ViewPreset/ViewPreset_2");
        Assert.IsNotNull(normalBtn, "сегмент «Обычный» должен быть");
        Assert.IsNotNull(roomBtn, "сегмент «Помещение» должен быть");
        Assert.IsNotNull(photoBtn, "сегмент «Фоторежим» должен быть");

        EditModeManager.SetMode(EditMode.Normal);
        AssertOnlyThisPresetIsMarked(normalBtn!, roomBtn!, photoBtn!,
            "в обычном режиме текущий пресет — «Обычный»");

        EditModeManager.SetMode(EditMode.Room);
        AssertOnlyThisPresetIsMarked(roomBtn!, normalBtn!, photoBtn!,
            "в режиме «помещение» вкладка показывает его пресет");

        EditModeManager.SetMode(EditMode.Photo);
        AssertOnlyThisPresetIsMarked(photoBtn!, normalBtn!, roomBtn!,
            "в фоторежиме вкладка показывает пресет фоторежима");

        EditModeManager.SetMode(EditMode.Normal);
        AssertOnlyThisPresetIsMarked(normalBtn!, roomBtn!, photoBtn!, "возврат в обычный режим");
    }

    /// <summary>Пресеты независимы: погашенные в обычном режиме стены не
    /// затирают настройку помещения и возвращаются при выходе из него.</summary>
    [Test]
    public void ViewTab_Presets_AreIndependent()
    {
        var s = KitchenSettings.Instance;
        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        var walls = view.Find("Row_Стены/Sw_Стены").GetComponent<Toggle>();

        walls.isOn = false;
        Assert.IsFalse(s.NormalView.wallsEnabled, "правится пресет обычного режима");
        Assert.IsTrue(s.RoomView.wallsEnabled, "пресет помещения не тронут");

        EditModeManager.SetMode(EditMode.Room);
        Assert.IsTrue(walls.isOn, "в помещении стены форсированы включёнными");
        Assert.IsFalse(walls.interactable, "и тумблер заблокирован");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.IsFalse(walls.isOn, "настройка обычного режима восстановилась");

        s.NormalView.wallsEnabled = true;
    }

    /// <summary>Фоторежим — третий пресет, а не набор зашитых значений: его
    /// тумблеры правятся и пишут в свой пресет, не задевая рабочий режим.</summary>
    [Test]
    public void ViewTab_PhotoPreset_IsEditable_AndSeparateFromNormal()
    {
        var s = KitchenSettings.Instance;
        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        bool prevNormal = s.NormalView.wallsEnabled;
        bool prevPhoto = s.PhotoView.wallsEnabled;

        EditModeManager.SetMode(EditMode.Photo);
        var walls = ToggleOf(view, "Стены");
        Assert.IsTrue(walls.interactable,
            "в фоторежиме вкладка открыта на его собственном пресете и правится");

        walls.isOn = false;
        Assert.IsFalse(s.PhotoView.wallsEnabled, "правится пресет фоторежима");
        Assert.AreEqual(prevNormal, s.NormalView.wallsEnabled, "пресет обычного режима не тронут");

        EditModeManager.SetMode(EditMode.Normal);
        s.PhotoView.wallsEnabled = prevPhoto;
    }

    private static void AssertOnlyThisPresetIsMarked(Transform current, Transform otherA, Transform otherB,
        string because)
    {
        Assert.IsTrue(MarkOf(current).gameObject.activeSelf, because);
        Assert.IsFalse(MarkOf(otherA).gameObject.activeSelf, because);
        Assert.IsFalse(MarkOf(otherB).gameObject.activeSelf, because);
        StringAssert.DoesNotContain("(", CaptionOf(current),
            "текущий режим отмечен звездой, а не словом в скобках: слово делает подпись вдвое длиннее кнопки");
    }

    private static Transform MarkOf(Transform button)
    {
        var mark = button.Find(PresetCurrentMark.MarkName);
        Assert.IsNotNull(mark, "у кнопки пресета нет места под звезду текущего режима");
        return mark!;
    }

    private static string CaptionOf(Transform button)
    {
        var caption = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        Assert.IsNotNull(caption);
        return caption!.text;
    }

    /// <summary>Нижний порог кромки: ниже него наезд соседа на торец не считается
    /// ошибкой EDG-01.</summary>
    [Test]
    public void ProjectTab_HasEdgeThresholdField()
    {
        var s = KitchenSettings.Instance;
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));

        var row = project.Find("Row_Нижний порог кромки");
        Assert.IsNotNull(row, "строка порога должна быть на странице «Проект»");

        var field = row.GetComponentInChildren<TMP_InputField>();
        Assert.IsNotNull(field);
        Assert.AreEqual(s.EdgePartialThresholdPct.ToString(), field.text);

        field.text = "12";
        field.onEndEdit.Invoke("12");
        Assert.AreEqual(12, s.EdgePartialThresholdPct);

        field.text = "5";
        field.onEndEdit.Invoke("5");
        Assert.AreEqual(KitchenSettings.EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT, s.EdgePartialThresholdPct);
    }

    [Test]
    public void ToggleRows_HaveCorrectInitialValues()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));

        AssertToggleValue(project, "Сетка", s.GridEnabled);
        AssertToggleValue(project, "Привязка к деталям", s.SnapEnabled);
        AssertToggleValue(project, "Блокировать недопустимые изменения", s.BlockOnViolation);
        AssertToggleValue(project, "Автосохранение", s.AutoSave);
        AssertToggleValue(project, "Пространственная сетка", s.SpatialGrid);
        AssertToggleValue(project, "Свободное панорамирование", s.CameraPanFree);

        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        AssertToggleValue(view, "Объекты", s.NormalView.objectsVisible);
        AssertToggleValue(view, "Контур объектов", s.NormalView.edgeOutline);
        AssertToggleValue(view, "Стены", s.NormalView.wallsEnabled);
        AssertToggleValue(view, "Контур стен", s.NormalView.wallOutline);
        AssertToggleValue(view, "Опускать ближние стены", s.NormalView.lowerNearWalls);
        AssertToggleValue(view, "Скрывать окна и двери", s.NormalView.hideOpeningsOnLoweredWalls);
        AssertToggleValue(view, "Скрыть источники света", s.NormalView.hideLightSources);
    }

    /// <summary>Панель строится ОДИН раз при старте, а проект грузится позже и
    /// приносит свои настройки. Открытие обязано перечитать их: без этого
    /// выключенная в проекте привязка показывалась включённым тумблером, и
    /// «прилипание не работает» выглядело как поломка снэпа, хотя
    /// SnapSystem.TrySnap честно отключён настройкой.</summary>
    [Test]
    public void Open_RereadsSettings_ChangedAfterBuild()
    {
        var s = KitchenSettings.Instance;
        bool prevSnap = s.SnapEnabled;
        bool prevGrid = s.GridEnabled;
        float prevThreshold = s.SnapThreshold;
        float prevWasd = s.WasdSpeed;

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var control = _canvas!.transform.Find(SettingsWindowPaths.Page("control"));
        Assert.IsTrue(ToggleOf(project, "Привязка к деталям").isOn, "на старте привязка включена");

        try
        {
            // Так выглядит загрузка проекта: KitchenSettings.FromData пишет
            // прямо в настройки, мимо UI.
            s.SnapEnabled = false;
            s.GridEnabled = false;
            s.SnapThreshold = 33f;
            s.WasdSpeed = 2.5f;

            _ui!.SetVisible(true);

            Assert.IsFalse(ToggleOf(project, "Привязка к деталям").isOn,
                "тумблер показывает состояние загруженного проекта");
            Assert.IsFalse(ToggleOf(project, "Сетка").isOn);
            AssertFieldValue(project, "Порог привязки", "33");
            Assert.AreEqual(2.5f, SliderOf(control, "Скорость WASD").value, 0.001f);
        }
        finally
        {
            // Настройки глобальные: упавший ассерт не должен утащить за собой
            // соседние тесты.
            _ui!.SetVisible(false);
            s.SnapEnabled = prevSnap;
            s.GridEnabled = prevGrid;
            s.SnapThreshold = prevThreshold;
            s.WasdSpeed = prevWasd;
        }
    }

    // ── Вложенность подопций ────────────────────────────────

    [Test]
    public void SubOptions_AreIndented_UnderTheirParent()
    {
        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));

        float wallsX = LabelX(view, "Стены");
        float outlineX = LabelX(view, "Контур стен");
        float lowerX = LabelX(view, "Опускать ближние стены");
        float hideX = LabelX(view, "Скрывать окна и двери");

        Assert.Greater(outlineX, wallsX, "«Контур» должен быть с отступом от «Стены»");
        Assert.AreEqual(outlineX, lowerX, 0.01f, "оба на первом уровне вложенности");
        Assert.Greater(hideX, lowerX, "«Скрывать окна и двери» — второй уровень");
        Assert.AreEqual(hideX, LabelX(view, "Опускать все стены"), 0.01f,
            "«Опускать все стены» — тоже подопция опускания, тот же уровень");
    }

    [Test]
    public void WallSubOptions_Disabled_WhenWallsOff()
    {
        var s = KitchenSettings.Instance;
        bool prev = s.NormalView.wallsEnabled;

        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        var walls = view.Find("Row_Стены/Sw_Стены").GetComponent<Toggle>();

        walls.isOn = false;
        Assert.IsFalse(ToggleOf(view, "Контур стен").interactable);
        Assert.IsFalse(ToggleOf(view, "Опускать ближние стены").interactable);

        walls.isOn = true;
        Assert.IsTrue(ToggleOf(view, "Контур стен").interactable);

        s.NormalView.wallsEnabled = prev;
    }

    [Test]
    public void HideOpenings_Disabled_WhenLowerWallsOff()
    {
        var s = KitchenSettings.Instance;
        bool prev = s.NormalView.lowerNearWalls;

        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        var lower = view.Find("Row_Опускать ближние стены/Sw_Опускать ближние стены").GetComponent<Toggle>();

        lower.isOn = false;
        Assert.IsFalse(ToggleOf(view, "Скрывать окна и двери").interactable);
        Assert.IsFalse(ToggleOf(view, "Опускать все стены").interactable,
            "«все стены» без опускания ничего не значит — тумблер заблокирован");

        lower.isOn = true;
        Assert.IsTrue(ToggleOf(view, "Скрывать окна и двери").interactable);
        Assert.IsTrue(ToggleOf(view, "Опускать все стены").interactable);

        s.NormalView.lowerNearWalls = prev;
    }

    [Test]
    public void LowerNearWalls_Disabled_InRoomMode()
    {
        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        Assert.IsTrue(ToggleOf(view, "Опускать ближние стены").interactable,
            "в обычном режиме тумблер активен");

        EditModeManager.SetMode(EditMode.Room);
        Assert.IsFalse(ToggleOf(view, "Опускать ближние стены").interactable,
            "в режиме «помещение» опускание запрещено");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.IsTrue(ToggleOf(view, "Опускать ближние стены").interactable);
    }

    [Test]
    public void ObjectOutline_Disabled_WhenObjectsOff()
    {
        var s = KitchenSettings.Instance;
        bool prev = s.NormalView.objectsVisible;

        var view = _canvas!.transform.Find(SettingsWindowPaths.Page("view"));
        var objects = view.Find("Row_Объекты/Sw_Объекты").GetComponent<Toggle>();

        objects.isOn = false;
        Assert.IsFalse(ToggleOf(view, "Контур объектов").interactable);

        objects.isOn = true;
        Assert.IsTrue(ToggleOf(view, "Контур объектов").interactable);

        s.NormalView.objectsVisible = prev;
    }

    // ── Вкладка «Управление» ────────────────────────────────

    [Test]
    public void ControlTab_HasThreeSliders()
    {
        var control = _canvas!.transform.Find(SettingsWindowPaths.Page("control"));
        Assert.IsNotNull(control);

        string[] labels = { "Чувствительность мыши", "Скорость WASD", "Скорость ←→↑↓" };
        foreach (var label in labels)
        {
            var row = control.Find($"Row_{label}");
            Assert.IsNotNull(row, $"Slider row '{label}' should exist");
            var slider = row.Find($"Sld_{label}").GetComponent<Slider>();
            Assert.IsNotNull(slider, $"Slider '{label}' should exist");
            Assert.AreEqual(KitchenSettings.MIN_INPUT_SPEED, slider.minValue, 0.001f);
            Assert.AreEqual(KitchenSettings.MAX_INPUT_SPEED, slider.maxValue, 0.001f);
        }
    }

    [Test]
    public void ControlSliders_HaveCorrectInitialValues()
    {
        var s = KitchenSettings.Instance;
        var control = _canvas!.transform.Find(SettingsWindowPaths.Page("control"));

        Assert.AreEqual(s.MouseSensitivity, SliderOf(control, "Чувствительность мыши").value, 0.001f);
        Assert.AreEqual(s.WasdSpeed, SliderOf(control, "Скорость WASD").value, 0.001f);
        Assert.AreEqual(s.ArrowSpeed, SliderOf(control, "Скорость ←→↑↓").value, 0.001f);
    }

    [Test]
    public void ControlSlider_UpdatesSetting()
    {
        var s = KitchenSettings.Instance;
        float prev = s.WasdSpeed;

        var control = _canvas!.transform.Find(SettingsWindowPaths.Page("control"));
        SliderOf(control, "Скорость WASD").value = 2.5f;
        Assert.AreEqual(2.5f, s.WasdSpeed, 0.001f);

        s.WasdSpeed = prev;
    }

    [Test]
    public void ToggleRow_CheckmarkReflectsState()
    {
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var toggleObj = project.Find("Row_Сетка/Sw_Сетка");
        var toggle = toggleObj.GetComponent<Toggle>();

        Assert.IsTrue(toggle.isOn);

        toggle.isOn = false;
        Assert.IsFalse(toggle.isOn);
    }

    [Test]
    public void ToggleRow_InvokesCallback()
    {
        var s = KitchenSettings.Instance;
        bool prev = s.GridEnabled;

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var toggle = project.Find("Row_Сетка/Sw_Сетка").GetComponent<Toggle>();
        toggle.isOn = !prev;

        Assert.AreEqual(!prev, s.GridEnabled);

        s.GridEnabled = prev;
    }

    // ── Project tab: input rows ─────────────────────────────

    [Test]
    public void ProjectTab_HasAllInputRows()
    {
        string[] expectedInputs =
        {
            "Шаг сетки", "Порог привязки", "Интервал автосохранения"
        };

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));

        foreach (var label in expectedInputs)
        {
            var row = project.Find($"Row_{label}");
            Assert.IsNotNull(row, $"Input row '{label}' should exist");

            var fieldObj = row.Find($"F_{label}");
            Assert.IsNotNull(fieldObj, $"Field '{label}' should exist");

            var field = fieldObj.GetComponent<TMP_InputField>();
            Assert.IsNotNull(field, $"InputField component on '{label}' should exist");
        }
    }

    [Test]
    public void InputRows_HaveCorrectInitialValues()
    {
        var s = KitchenSettings.Instance;
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));

        AssertFieldValue(project, "Шаг сетки", s.GridStep.ToString());
        AssertFieldValue(project, "Порог привязки", s.SnapThreshold.ToString("F0"));
        AssertFieldValue(project, "Интервал автосохранения", s.AutoSaveInterval.ToString());
    }

    [Test]
    public void InputField_UpdatesSetting_OnValidInput()
    {
        var s = KitchenSettings.Instance;
        int prev = s.GridStep;

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var field = project.Find("Row_Шаг сетки/F_Шаг сетки").GetComponent<TMP_InputField>();
        field.text = "42";
        field.onEndEdit.Invoke("42");

        Assert.AreEqual(42, s.GridStep);

        s.GridStep = prev;
    }

    [Test]
    public void InputField_IgnoresInvalidInput()
    {
        var s = KitchenSettings.Instance;
        int prev = s.GridStep;

        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var field = project.Find("Row_Шаг сетки/F_Шаг сетки").GetComponent<TMP_InputField>();
        field.text = "abc";
        field.onEndEdit.Invoke("abc");

        Assert.AreEqual(prev, s.GridStep);
    }

    [Test]
    public void InputField_HasOutlineComponent()
    {
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var field = project.Find("Row_Шаг сетки/F_Шаг сетки").GetComponent<TMP_InputField>();
        var outline = field.GetComponent<Outline>();
        Assert.IsNotNull(outline, "InputField should have Outline component");
    }

    // ── About tab ───────────────────────────────────────────

    private static readonly string[] AboutValueNames =
        { "AboutBuild", "AboutPlatform", "AboutUnity", "AboutGraphicsApi", "AboutGpu" };

    private static string TextOf(Transform line) =>
        line.GetComponent<TMP_InputField>().text;

    private static Transform AboutNode(Transform about, string name)
    {
        var node = about.Find("Row_" + name + "/" + name);
        Assert.IsNotNull(node, $"строки «{name}» нет на странице «О программе»");
        return node!;
    }

    [Test]
    public void AboutTab_ShowsEveryBuildInfoLine_WithoutPressingAnything()
    {
        var about = _canvas!.transform.Find(SettingsWindowPaths.Page("about"));
        Assert.IsNotNull(about);

        var env = SettingsAboutTab.CurrentEnvironment();
        var lines = AboutLines.For(env);
        var rows = AboutRows.For(env);
        Assert.AreEqual(AboutValueNames.Length, rows.Count,
            "таблица имён строк разошлась с набором строк страницы");

        Assert.AreEqual(lines[0], TextOf(AboutNode(about, "AboutProduct")), "название и версия — одной строкой");
        for (int i = 0; i < AboutValueNames.Length; i++)
        {
            Assert.AreEqual(rows[i].Value, TextOf(AboutNode(about, AboutValueNames[i])),
                $"строка «{AboutValueNames[i]}» показывает не то, что собрал AboutRows");
            var caption = about.Find("Row_" + AboutValueNames[i] + "/" + AboutValueNames[i] + "_Caption");
            Assert.AreEqual(rows[i].Caption, caption.GetComponent<TextMeshProUGUI>().text,
                $"подпись строки «{AboutValueNames[i]}»");
        }
        Assert.AreEqual(lines[lines.Count - 1], TextOf(AboutNode(about, "AboutCopyright")), "копирайт — последняя строка");
    }

    [Test]
    public void AboutTab_GpuLine_NamesTheVideoCardTheEngineRendersOn()
    {
        var about = _canvas!.transform.Find(SettingsWindowPaths.Page("about"));

        StringAssert.Contains(SystemInfo.graphicsDeviceName, TextOf(AboutNode(about, "AboutGpu")),
            "строка видеокарты берёт SystemInfo.graphicsDeviceName — устройство, на котором "
            + "движок действительно рисует, а не первую карту из списка системы");
        StringAssert.Contains(SystemInfo.graphicsDeviceType.ToString(), TextOf(AboutNode(about, "AboutGraphicsApi")),
            "строка графического API берёт SystemInfo.graphicsDeviceType");
    }

    [Test]
    public void AboutTab_HasNoCopyButton_ButItsTextCanBeSelected()
    {
        var about = _canvas!.transform.Find(SettingsWindowPaths.Page("about"));

        Assert.IsEmpty(about.GetComponentsInChildren<Button>(true),
            "сведения видны сразу — кнопка «скопировать» не нужна и не должна возвращаться (решение пользователя)");
        var names = new List<string> { "AboutProduct", "AboutCopyright" };
        names.AddRange(AboutValueNames);
        foreach (var name in names)
        {
            var input = AboutNode(about, name).GetComponent<TMP_InputField>();
            Assert.IsNotNull(input,
                $"«{name}» обязана быть выделяемым текстом: так сведения о сборке копируют вручную");
            Assert.IsTrue(input!.readOnly, $"«{name}» — только для чтения: править версию сборки в окне нельзя");
        }
    }

    [Test]
    public void AboutTab_Card_StandsAtTheTopLeftOfThePage_NotInTheMiddleOfTheWindow()
    {
        var about = _canvas!.transform.Find(SettingsWindowPaths.Page("about"));
        var product = (RectTransform)AboutNode(about, "AboutProduct");
        var row = (RectTransform)product.parent;
        var title = (RectTransform)about.Find(SettingsPage.TitleNode);

        Assert.AreEqual(title.anchoredPosition.x, row.anchoredPosition.x, 0.01f,
            "карточка стоит у левого края страницы, под её заголовком");
        Assert.Less(row.anchoredPosition.y, title.anchoredPosition.y, "карточка ниже заголовка страницы");
        Assert.Greater(row.anchoredPosition.y, -UIStyle.SettingsSize.y * 0.5f,
            "карточка в верхней половине окна, а не по центру: прежняя «О программе» висела посередине 900×900");
    }

    // ── Photo tab ───────────────────────────────────────────

    [Test]
    public void PhotoTab_HasNoPhotoModeSwitch_BecauseTheToolbarOwnsIt()
    {
        var photo = _canvas!.transform.Find(SettingsWindowPaths.Page("photo"));
        Assert.IsNotNull(photo);

        Assert.IsNull(photo.Find("Row_Фоторежим"),
            "включатель фоторежима живёт на панели инструментов; вторая копия на странице настроек "
            + "дублировала его и была вторым писателем одного состояния");
        var quality = photo.Find("Row_" + SettingsPhotoTab.QualityId);
        Assert.IsNotNull(quality, "пара к проверке: страница не пуста, первой строкой стоит качество фоторежима");
        float topRowY = photo.Cast<Transform>().Where(t => t.name.StartsWith("Row_"))
            .Max(t => ((RectTransform)t).anchoredPosition.y);
        Assert.AreEqual(topRowY, ((RectTransform)quality!).anchoredPosition.y, 0.01f,
            "строка качества стоит выше всех остальных строк страницы: на месте убранного включателя пустоты нет");
    }

    [Test]
    public void PhotoTab_Quality_IsASegmentedControl_OfLowMediumHigh()
    {
        var photo = _canvas!.transform.Find(SettingsWindowPaths.Page("photo"));
        var segmented = photo.Find("Row_" + SettingsPhotoTab.QualityId).GetComponentInChildren<SegmentedControl>(true);

        Assert.IsNotNull(segmented, "«Качество» — сегментный контрол, а не кнопка-переключатель по кругу");
        CollectionAssert.AreEqual(
            new[] { Loc.T("settings.photo.presetLow"), Loc.T("settings.photo.presetMedium"), Loc.T("settings.photo.presetHigh") },
            segmented!.Segments.Select(s => s.GetComponentInChildren<TMP_Text>().text.Replace("\u200b", "")).ToList());
    }

    // ── MCP tab ─────────────────────────────────────────────

    /// <summary>Вкладка существует ради одного действия: скопировать текст и
    /// отдать его агенту. Проверяется, что это действие на месте — обе кнопки
    /// копирования и кнопка, открывающая инструкцию, которая едет вместе с
    /// плеером.</summary>
    [Test]
    public void McpTab_HasTheCopyButtonsAndTheGuideButton()
    {
        var mcp = _canvas!.transform.Find(SettingsWindowPaths.Page("mcp"));
        Assert.IsNotNull(mcp, "вкладки MCP нет в панели");

        Assert.IsNotNull(mcp.Find("Row_McpCopyPrompt/McpCopyPrompt"), "кнопка «скопировать инструкцию для агента»");
        Assert.IsNotNull(mcp.Find("Row_McpCopyConfig/McpCopyConfig"), "кнопка «скопировать конфиг mcp.json»");
        Assert.IsNotNull(mcp.Find("Row_McpOpenGuide/McpOpenGuide"), "кнопка «открыть инструкцию»");
        Assert.IsNotNull(mcp.Find("McpGuidePath"), "где инструкция лежит — сказано словами");
    }

    /// <summary>Инструкция обязана ехать ВМЕСТЕ с плеером, а не жить ссылкой на
    /// GitHub: у человека, который открыл программу без интернета (или просто не
    /// хочет ходить на чужой сайт), подключение не должно упираться в браузер.
    /// StreamingAssets попадает в сборку целиком, а установщик копирует всю папку
    /// Build — значит файл доедет до пользователя сам.</summary>
    [Test]
    public void McpGuide_ShipsWithThePlayer_AndIsNotEmpty()
    {
        Assert.IsTrue(SettingsMcpTab.GuideShipped,
            "инструкции нет в StreamingAssets — в сборку она не поедет: " + SettingsMcpTab.GuidePath);

        var text = SettingsMcpTab.GuideText();
        StringAssert.Contains(SettingsMcpTab.ServerName, text, "имя MCP-сервера");
        StringAssert.Contains("127.0.0.1:9337/mcp", text, "адрес, который вводит пользователь");
        StringAssert.DoesNotContain("npm", text,
            "мост на Node больше не собирают: сервер живёт внутри программы, и любое "
            + "упоминание npm отправляет пользователя ставить то, чего мы не поставляем");
        Assert.Greater(text.Length, 1000, "инструкция подозрительно короткая — файл обрезан?");
    }

    /// <summary>Текст для агента больше НЕ содержит инструкцию целиком: агенту
    /// нужен один адрес, а не страница про сборку моста. Порт живой — приложение
    /// можно запустить с -mcpPort, и в буфер обязан лечь тот порт, который
    /// программа действительно слушает.</summary>
    [Test]
    public void McpTab_AgentPrompt_IsShort_NamesTheUrl_AndAsksForPing()
    {
        var prompt = SettingsMcpTab.AgentPrompt(9500);

        StringAssert.Contains("http://127.0.0.1:9500/mcp", prompt, "живой адрес названа целиком");
        StringAssert.Contains(SettingsMcpTab.ServerName, prompt, "под каким именем прописать");
        StringAssert.Contains("ping", prompt, "чем проверить связь");
        StringAssert.DoesNotContain("node", prompt,
            "ставить Node пользователю больше не нужно, и просить его об этом нельзя");
        StringAssert.DoesNotContain("npm", prompt);
        Assert.Less(prompt.Length, 1500,
            "текст читает агент и человек: страница на 200 строк вместо адреса — это "
            + "ровно та ситуация, ради ухода от которой сервер и переехал внутрь программы");
    }

    /// <summary>Второй способ — вставить конфигурацию руками. Это готовый JSON,
    /// и он обязан быть валидным: сломанный не даст даже сообщения об ошибке,
    /// агент просто не увидит сервер.</summary>
    [Test]
    public void McpTab_ConfigSnippet_IsValidJsonWithTheServerAndPort()
    {
        var snippet = SettingsMcpTab.ConfigSnippet(9500);

        var parsed = Newtonsoft.Json.Linq.JObject.Parse(snippet);
        var server = parsed["mcpServers"]?[SettingsMcpTab.ServerName];
        Assert.IsNotNull(server, "в конфиге нет сервера " + SettingsMcpTab.ServerName);
        Assert.AreEqual("http://127.0.0.1:9500/mcp", (string?)server!["url"],
            "клиент подключается по адресу; порт — тот же, что показан на странице");
        Assert.IsNull(server["command"],
            "command означало бы «запусти процесс»: запускать больше нечего, и попытка "
            + "запустить node у пользователя без Node молча оставит его без сервера");
    }

    /// <summary>Состояние моста — первое, что смотрит человек, у которого агент
    /// не подключился. Строка обязана называть порт и говорить, работает мост
    /// или нет; в тестовой сцене моста нет, значит «остановлен».</summary>
    [Test]
    public void McpTab_StatusLine_TellsPortAndWhetherTheBridgeRuns()
    {
        var mcp = _canvas!.transform.Find(SettingsWindowPaths.Page("mcp"));
        var status = mcp.Find("McpStatus");
        Assert.IsNotNull(status, "строки состояния нет");

        var text = status!.GetComponent<TextMeshProUGUI>().text;
        StringAssert.Contains(SettingsMcpTab.ActivePort().ToString(), text, "порт назван");
        Assert.IsFalse(SettingsMcpTab.BridgeRunning(), "в тестовой сцене моста нет");
        StringAssert.Contains("остановлен", text, "и это сказано словами, а не только цветом");
    }

    // ── Row ordering ────────────────────────────────────────

    [Test]
    public void ProjectRows_AreOrderedDescending()
    {
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var rows = new List<RectTransform>();
        foreach (Transform child in project)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && (rt.name.StartsWith("Row_") || rt.name.StartsWith("Sec_"))) rows.Add(rt);
        }
        Assert.GreaterOrEqual(rows.Count, 10, "обход не нашёл строк страницы — он бы зеленел впустую");

        for (int i = 1; i < rows.Count; i++)
        {
            Assert.IsTrue(rows[i - 1].anchoredPosition.y > rows[i].anchoredPosition.y,
                $"Row {rows[i - 1].name} should be above {rows[i].name}");
        }
    }

    // ── Panel fits content ──────────────────────────────────

    [Test]
    public void PanelHeight_FitsAllContent()
    {
        var root = (RectTransform)_canvas!.transform.Find("SettingsPanel");
        _ui!.SetVisible(true);

        var corners = new Vector3[4];
        foreach (Transform child in root)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt == null || !child.gameObject.activeSelf) continue;
            if (child.GetComponent<WindowDecoration>() != null) continue;

            rt.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var p = root.InverseTransformPoint(corner);
                Assert.IsTrue(p.x >= root.rect.xMin - 0.01f && p.x <= root.rect.xMax + 0.01f
                        && p.y >= root.rect.yMin - 0.01f && p.y <= root.rect.yMax + 0.01f,
                    $"{child.name}: угол ({p.x:F1}; {p.y:F1}) вне окна {root.rect}");
            }
        }
    }

    [Test]
    public void TwoConsecutiveBuilds_DoNotThrow()
    {
        var probe = NewProbePanel(out var canvas);
        try
        {
            probe.Build(canvas.transform);
            Assert.DoesNotThrow(() => probe.Build(canvas.transform));
        }
        finally
        {
            Object.DestroyImmediate(canvas.gameObject);
        }
    }

    [Test]
    public void ToggleRows_AreSwitches_NotCheckboxes()
    {
        var project = _canvas!.transform.Find(SettingsWindowPaths.Page("project"));
        var toggle = project.Find("Row_Сетка/Sw_Сетка").GetComponent<Toggle>();
        Assert.IsNotNull(toggle.targetGraphic, "у переключателя есть дорожка");
        Assert.IsNotNull(toggle.GetComponent<SwitchControl>(), "булевы в настройках — Switch (D6), не квадратик");
        Assert.IsNotNull(toggle.transform.Find(SwitchControl.TrackNode + "/" + SwitchControl.KnobNode), "и бегунок");
    }

    // ── helpers ─────────────────────────────────────────────

    private static void AssertColorEqual(Color expected, Color actual)
    {
        const float delta = 0.01f;
        Assert.AreEqual(expected.r, actual.r, delta, $"R: expected {expected.r}, got {actual.r}");
        Assert.AreEqual(expected.g, actual.g, delta, $"G: expected {expected.g}, got {actual.g}");
        Assert.AreEqual(expected.b, actual.b, delta, $"B: expected {expected.b}, got {actual.b}");
        Assert.AreEqual(expected.a, actual.a, delta, $"A: expected {expected.a}, got {actual.a}");
    }

    private static Toggle ToggleOf(Transform parent, string key) =>
        parent.Find($"Row_{key}/Sw_{key}").GetComponent<Toggle>();

    private static Slider SliderOf(Transform parent, string key) =>
        parent.Find($"Row_{key}/Sld_{key}").GetComponent<Slider>();

    private static float LabelX(Transform parent, string key) =>
        parent.Find($"Row_{key}/L_{key}").GetComponent<RectTransform>().anchoredPosition.x;

    private static void AssertToggleValue(Transform parent, string label, bool expected)
    {
        var toggle = parent.Find($"Row_{label}/Sw_{label}").GetComponent<Toggle>();
        Assert.AreEqual(expected, toggle.isOn, $"Toggle '{label}' should be {(expected ? "on" : "off")}");
    }

    private static void AssertFieldValue(Transform parent, string label, string expected)
    {
        var field = parent.Find($"Row_{label}/F_{label}").GetComponent<TMP_InputField>();
        Assert.AreEqual(expected, field.text.Replace("\u200b", ""));
    }
}
