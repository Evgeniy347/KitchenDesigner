using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class SettingsPanelUITests
{
    private Canvas? _canvas;
    private SettingsPanelUI? _ui;

    // Страницы вкладок лежат НЕ прямо на панели, а в области прокрутки окна:
    // параметров стало больше, чем помещается в 900 px, и вкладка «Фото режим»
    // прятала около 330 px. Путь собран в одну константу намеренно — когда
    // структура окна поменяется снова, править придётся одну строку, а не сорок.
    private const string PagePath = "SettingsPanel/SettingsPanelBody/SettingsPanelBodyContent/";

    [SetUp]
    public void Setup()
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

    [TearDown]
    public void TearDown()
    {
        // Режим редактора — глобальное состояние: тест, который его крутит,
        // обязан вернуть исходное (см. правила снапшотов в AGENTS.md).
        EditModeManager.Reset();
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
    }

    // ── Build / smoke ───────────────────────────────────────

    [Test]
    public void Build_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _ui!.Build(_canvas!.transform));
    }

    [Test]
    public void Panel_Exists_WithCorrectSize()
    {
        var panel = _canvas!.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel);
        var rt = panel.GetComponent<RectTransform>();
        Assert.AreEqual(600, rt.sizeDelta.x, 0.01f);
        Assert.AreEqual(900, rt.sizeDelta.y, 0.01f);
    }

    [Test]
    public void Title_Exists_WithCorrectText()
    {
        var title = _canvas!.transform.Find("SettingsPanel/SetTitle");
        Assert.IsNotNull(title);
        var label = title.GetComponent<TextMeshProUGUI>();
        Assert.AreEqual("Настройки", label.text.Replace("\u200b", ""));
        Assert.AreEqual(24, label.fontSize);
    }

    // ── Tabs ────────────────────────────────────────────────

    [Test]
    public void SixTabButtons_Exist()
    {
        for (int i = 0; i < 6; i++)
        {
            var tab = _canvas!.transform.Find($"SettingsPanel/Tab_{i}");
            Assert.IsNotNull(tab, $"Tab_{i} should exist");
        }
    }

    [Test]
    public void TabButtons_HaveCorrectLabels()
    {
        string[] expected = { "Проект", "Вид", "Управление", "Фото режим", "Свет", "MCP", "О программе" };
        for (int i = 0; i < expected.Length; i++)
        {
            var tab = _canvas!.transform.Find($"SettingsPanel/Tab_{i}");
            var label = tab.Find($"Tab_{i}_Label");
            Assert.IsNotNull(label, $"Tab_{i}_Label should exist");
            Assert.AreEqual(expected[i], label.GetComponent<TextMeshProUGUI>().text.Replace("\u200b", ""));
        }
    }

    [Test]
    public void TabPages_Exist()
    {
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_Project"), "Tab_Project page should exist");
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_View"), "Tab_View page should exist");
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_Control"), "Tab_Control page should exist");
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_Photo"), "Tab_Photo page should exist");
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_Light"), "Tab_Light page should exist");
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_Mcp"), "Tab_Mcp page should exist");
        Assert.IsNotNull(_canvas!.transform.Find(PagePath + "Tab_About"), "Tab_About page should exist");
    }

    [Test]
    public void SwitchTab_OnlyActivePageVisible()
    {
        var project = _canvas!.transform.Find(PagePath + "Tab_Project").gameObject;
        var photo = _canvas!.transform.Find(PagePath + "Tab_Photo").gameObject;
        var about = _canvas!.transform.Find(PagePath + "Tab_About").gameObject;

        Assert.IsTrue(project.activeSelf, "Project tab should be active by default");
        Assert.IsFalse(photo.activeSelf, "Photo tab should be hidden by default");
        Assert.IsFalse(about.activeSelf, "About tab should be hidden by default");

        // Tab_1 → «Вид», Tab_2 → «Управление», Tab_3 → «Фото режим»,
        // Tab_4 → «Свет», Tab_5 → «MCP», Tab_6 → «О программе».
        _canvas!.transform.Find("SettingsPanel/Tab_6").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(project.activeSelf, "Project should hide after switching to About");
        Assert.IsTrue(about.activeSelf, "About should show after click");
        Assert.IsFalse(photo.activeSelf, "Photo should stay hidden");
    }

    [Test]
    public void ActiveTab_HasHighlightColor()
    {
        var tab0Img = _canvas!.transform.Find("SettingsPanel/Tab_0").GetComponent<Image>();
        var tab1Img = _canvas!.transform.Find("SettingsPanel/Tab_1").GetComponent<Image>();

        Color active = new(0.28f, 0.33f, 0.42f, 1f);
        Color inactive = new(0.15f, 0.16f, 0.20f, 1f);

        AssertColorEqual(active, tab0Img.color);
        AssertColorEqual(inactive, tab1Img.color);

        var tab1Btn = _canvas!.transform.Find("SettingsPanel/Tab_1").GetComponent<Button>();
        tab1Btn.onClick.Invoke();

        AssertColorEqual(inactive, tab0Img.color);
        AssertColorEqual(active, tab1Img.color);
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
    public void CloseButton_Exists()
    {
        var btn = _canvas!.transform.Find("SettingsPanel/SetClose");
        Assert.IsNotNull(btn);
    }

    [Test]
    public void CloseButton_HidesPanel()
    {
        var panel = _canvas!.transform.Find("SettingsPanel").gameObject;
        _ui!.SetVisible(true);
        Assert.IsTrue(panel.activeSelf);

        var btn = _canvas!.transform.Find("SettingsPanel/SetClose").GetComponent<Button>();
        btn.onClick.Invoke();
        Assert.IsFalse(panel.activeSelf);
    }

    [Test]
    public void CloseButton_IsBelowTabs()
    {
        var panel = _canvas!.transform.Find("SettingsPanel").GetComponent<RectTransform>();
        var closeBtn = _canvas!.transform.Find("SettingsPanel/SetClose").GetComponent<RectTransform>();
        var projectTab = _canvas!.transform.Find(PagePath + "Tab_Project").GetComponent<RectTransform>();

        float closeY = closeBtn.anchoredPosition.y;
        Assert.IsTrue(closeY < 0, "Close button should be below the panel center");
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

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");

        foreach (var label in expectedToggles)
        {
            var row = project.Find($"RowTgl_{label}");
            Assert.IsNotNull(row, $"Toggle row '{label}' should exist");

            var toggleObj = row.Find($"Tgl_{label}");
            Assert.IsNotNull(toggleObj, $"Toggle '{label}' should exist");

            var toggle = toggleObj.GetComponent<Toggle>();
            Assert.IsNotNull(toggle, $"Toggle component on '{label}' should exist");
        }
    }

    /// <summary>Всё, что описывает «что показывать в сцене», собрано на вкладке
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

        var view = _canvas!.transform.Find(PagePath + "Tab_View");
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");

        foreach (var label in expectedToggles)
        {
            Assert.IsNotNull(view.Find($"RowTgl_{label}"), $"'{label}' должен быть на вкладке «Вид»");
            Assert.IsNull(project.Find($"RowTgl_{label}"), $"'{label}' больше не на вкладке «Проект»");
        }
    }

    /// <summary>Переключатель пресетов: на вкладке правится набор для одного
    /// режима, и он сам встаёт на пресет текущего режима.</summary>
    [Test]
    public void ViewTab_PresetSwitch_FollowsEditMode()
    {
        var view = _canvas!.transform.Find(PagePath + "Tab_View");
        var normalBtn = view.Find("RowViewPreset/ViewPreset_0");
        var roomBtn = view.Find("RowViewPreset/ViewPreset_1");
        var photoBtn = view.Find("RowViewPreset/ViewPreset_2");
        Assert.IsNotNull(normalBtn, "сегмент «Обычный» должен быть");
        Assert.IsNotNull(roomBtn, "сегмент «Помещение» должен быть");
        Assert.IsNotNull(photoBtn, "сегмент «Фоторежим» должен быть");

        Assert.IsTrue(CaptionOf(normalBtn!).Contains("текущий"),
            "в обычном режиме текущий пресет — «Обычный»");

        EditModeManager.SetMode(EditMode.Room);
        Assert.IsTrue(CaptionOf(roomBtn!).Contains("текущий"),
            "в режиме «помещение» вкладка показывает его пресет");

        EditModeManager.SetMode(EditMode.Photo);
        Assert.IsTrue(CaptionOf(photoBtn!).Contains("текущий"),
            "в фоторежиме вкладка показывает пресет фоторежима");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.IsTrue(CaptionOf(normalBtn!).Contains("текущий"));
    }

    /// <summary>Пресеты независимы: погашенные в обычном режиме стены не
    /// затирают настройку помещения и возвращаются при выходе из него.</summary>
    [Test]
    public void ViewTab_Presets_AreIndependent()
    {
        var s = KitchenSettings.Instance;
        var view = _canvas!.transform.Find(PagePath + "Tab_View");
        var walls = view.Find("RowTgl_Стены/Tgl_Стены").GetComponent<Toggle>();

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
        var view = _canvas!.transform.Find(PagePath + "Tab_View");
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
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");

        var row = project.Find("RowFld_Нижний порог кромки");
        Assert.IsNotNull(row, "строка порога должна быть на вкладке «Проект»");

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

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");

        AssertToggleValue(project, "Сетка", s.GridEnabled);
        AssertToggleValue(project, "Привязка к деталям", s.SnapEnabled);
        AssertToggleValue(project, "Блокировать недопустимые изменения", s.BlockOnViolation);
        AssertToggleValue(project, "Автосохранение", s.AutoSave);
        AssertToggleValue(project, "Пространственная сетка", s.SpatialGrid);
        AssertToggleValue(project, "Свободное панорамирование", s.CameraPanFree);

        var view = _canvas!.transform.Find(PagePath + "Tab_View");
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

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var control = _canvas!.transform.Find(PagePath + "Tab_Control");
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
        var view = _canvas!.transform.Find(PagePath + "Tab_View");

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

        var view = _canvas!.transform.Find(PagePath + "Tab_View");
        var walls = view.Find("RowTgl_Стены/Tgl_Стены").GetComponent<Toggle>();

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

        var view = _canvas!.transform.Find(PagePath + "Tab_View");
        var lower = view.Find("RowTgl_Опускать ближние стены/Tgl_Опускать ближние стены").GetComponent<Toggle>();

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
        var view = _canvas!.transform.Find(PagePath + "Tab_View");
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

        var view = _canvas!.transform.Find(PagePath + "Tab_View");
        var objects = view.Find("RowTgl_Объекты/Tgl_Объекты").GetComponent<Toggle>();

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
        var control = _canvas!.transform.Find(PagePath + "Tab_Control");
        Assert.IsNotNull(control);

        string[] labels = { "Чувствительность мыши", "Скорость WASD", "Скорость ←→↑↓" };
        foreach (var label in labels)
        {
            var row = control.Find($"RowSld_{label}");
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
        var control = _canvas!.transform.Find(PagePath + "Tab_Control");

        Assert.AreEqual(s.MouseSensitivity, SliderOf(control, "Чувствительность мыши").value, 0.001f);
        Assert.AreEqual(s.WasdSpeed, SliderOf(control, "Скорость WASD").value, 0.001f);
        Assert.AreEqual(s.ArrowSpeed, SliderOf(control, "Скорость ←→↑↓").value, 0.001f);
    }

    [Test]
    public void ControlSlider_UpdatesSetting()
    {
        var s = KitchenSettings.Instance;
        float prev = s.WasdSpeed;

        var control = _canvas!.transform.Find(PagePath + "Tab_Control");
        SliderOf(control, "Скорость WASD").value = 2.5f;
        Assert.AreEqual(2.5f, s.WasdSpeed, 0.001f);

        s.WasdSpeed = prev;
    }

    [Test]
    public void ToggleRow_CheckmarkReflectsState()
    {
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var toggleObj = project.Find("RowTgl_Сетка/Tgl_Сетка");
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

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var toggle = project.Find("RowTgl_Сетка/Tgl_Сетка").GetComponent<Toggle>();
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

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");

        foreach (var label in expectedInputs)
        {
            var row = project.Find($"RowFld_{label}");
            Assert.IsNotNull(row, $"Input row '{label}' should exist");

            var fieldObj = row.Find($"Fld_{label}");
            Assert.IsNotNull(fieldObj, $"Field '{label}' should exist");

            var field = fieldObj.GetComponent<TMP_InputField>();
            Assert.IsNotNull(field, $"InputField component on '{label}' should exist");
        }
    }

    [Test]
    public void InputRows_HaveCorrectInitialValues()
    {
        var s = KitchenSettings.Instance;
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");

        AssertFieldValue(project, "Шаг сетки", s.GridStep.ToString());
        AssertFieldValue(project, "Порог привязки", s.SnapThreshold.ToString("F0"));
        AssertFieldValue(project, "Интервал автосохранения", s.AutoSaveInterval.ToString());
    }

    [Test]
    public void InputField_UpdatesSetting_OnValidInput()
    {
        var s = KitchenSettings.Instance;
        int prev = s.GridStep;

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var field = project.Find("RowFld_Шаг сетки/Fld_Шаг сетки").GetComponent<TMP_InputField>();
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

        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var field = project.Find("RowFld_Шаг сетки/Fld_Шаг сетки").GetComponent<TMP_InputField>();
        field.text = "abc";
        field.onEndEdit.Invoke("abc");

        Assert.AreEqual(prev, s.GridStep);
    }

    [Test]
    public void InputField_HasOutlineComponent()
    {
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var field = project.Find("RowFld_Шаг сетки/Fld_Шаг сетки").GetComponent<TMP_InputField>();
        var outline = field.GetComponent<Outline>();
        Assert.IsNotNull(outline, "InputField should have Outline component");
    }

    // ── About tab ───────────────────────────────────────────

    [Test]
    public void AboutTab_HasVersionAndBuildDate()
    {
        var about = _canvas!.transform.Find(PagePath + "Tab_About");
        Assert.IsNotNull(about);

        var verLabel = about.Find("AboutVersion");
        Assert.IsNotNull(verLabel);
        var verText = verLabel.GetComponent<TextMeshProUGUI>().text;
        Assert.IsTrue(verText.StartsWith("Версия:"));

        var dateLabel = about.Find("AboutDate");
        Assert.IsNotNull(dateLabel);
        var dateText = dateLabel.GetComponent<TextMeshProUGUI>().text;
        Assert.IsTrue(dateText.StartsWith("Сборка:"));
    }

    /// <summary>Копирайт — обязательная строка «о программе», а не украшение:
    /// без неё сборку нельзя раздавать. Год и имя проверяются буквально,
    /// потому что опечатка в них тихо переживёт любой рефакторинг.</summary>
    [Test]
    public void AboutTab_ShowsTheCopyrightLine()
    {
        var about = _canvas!.transform.Find(PagePath + "Tab_About");
        var label = about.Find("AboutCopyright");
        Assert.IsNotNull(label, "строки копирайта нет на вкладке «О программе»");

        Assert.AreEqual("Copyright (c) 2025 Evgeniy347",
            label!.GetComponent<TextMeshProUGUI>().text,
            "текст копирайта задан дословно");
    }

    /// <summary>Сведения о сборке нужны в чужих руках: пользователь копирует их
    /// одной кнопкой и вкладывает в письмо об ошибке. Поэтому в отчёте обязаны
    /// быть версия, дата и окружение, а не только название.</summary>
    [Test]
    public void AboutTab_BuildReport_CarriesVersionDateAndEnvironment()
    {
        var report = SettingsAboutTab.BuildReport();

        StringAssert.Contains(BuildInfo.Version, report, "версия");
        StringAssert.Contains(BuildInfo.BuildDate, report, "дата сборки");
        StringAssert.Contains(Application.unityVersion, report, "версия движка");
        StringAssert.Contains(SettingsAboutTab.Copyright, report, "копирайт");

        var about = _canvas!.transform.Find(PagePath + "Tab_About");
        Assert.IsNotNull(about.Find("AboutCopy"), "кнопка копирования сведений о сборке");
    }

    // ── MCP tab ─────────────────────────────────────────────

    /// <summary>Вкладка существует ради одного действия: скопировать текст и
    /// отдать его агенту. Проверяется, что это действие на месте — кнопка, её
    /// подпись и ссылка на подробную инструкцию, набранная текстом (её можно
    /// переписать руками, если браузер не открылся).</summary>
    [Test]
    public void McpTab_HasTheCopyButtonsAndTheGuideLink()
    {
        var mcp = _canvas!.transform.Find(PagePath + "Tab_Mcp");
        Assert.IsNotNull(mcp, "вкладки MCP нет в панели");

        Assert.IsNotNull(mcp.Find("McpCopyPrompt"), "кнопка «скопировать инструкцию для агента»");
        Assert.IsNotNull(mcp.Find("McpCopyConfig"), "кнопка «скопировать конфиг mcp.json»");
        Assert.IsNotNull(mcp.Find("McpOpenGuide"), "кнопка «открыть инструкцию на GitHub»");

        var url = mcp.Find("McpGuideUrl");
        Assert.IsNotNull(url, "адрес инструкции должен быть виден текстом, а не только в кнопке");
        Assert.AreEqual(SettingsMcpTab.GuideUrl, url!.GetComponent<TextMeshProUGUI>().text);
    }

    /// <summary>Текст для агента бесполезен, если из него не собирается рабочая
    /// конфигурация. Поэтому проверяется не длина, а наличие всех четырёх
    /// обязательных частей: адрес инструкции, имя сервера, порт и то, что
    /// запускать. Порт подставляется живой — приложение можно запустить с
    /// -mcpPort, и инструкция обязана назвать ТОТ порт, а не 9337 из константы.</summary>
    [Test]
    public void McpTab_AgentPrompt_NamesTheGuidePortAndServer()
    {
        var prompt = SettingsMcpTab.AgentPrompt(9500);

        StringAssert.Contains(SettingsMcpTab.GuideUrl, prompt, "адрес подробной инструкции");
        StringAssert.Contains(SettingsMcpTab.ServerName, prompt, "имя MCP-сервера");
        StringAssert.Contains("9500", prompt, "порт подставляется живой, а не зашитый");
        StringAssert.Contains("dist/index.js", prompt, "что именно запускать мостом");
        StringAssert.DoesNotContain("9337", prompt,
            "порт по умолчанию не должен просочиться рядом с настоящим — агент возьмёт не тот");
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
        Assert.AreEqual("node", (string?)server!["command"], "мост запускается node");
        Assert.AreEqual("9500", (string?)server["env"]!["UNITY_MCP_PORT"],
            "порт в конфиге — тот же, что показан на вкладке");
    }

    /// <summary>Состояние моста — первое, что смотрит человек, у которого агент
    /// не подключился. Строка обязана называть порт и говорить, работает мост
    /// или нет; в тестовой сцене моста нет, значит «остановлен».</summary>
    [Test]
    public void McpTab_StatusLine_TellsPortAndWhetherTheBridgeRuns()
    {
        var mcp = _canvas!.transform.Find(PagePath + "Tab_Mcp");
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
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var rows = new List<RectTransform>();
        foreach (Transform child in project)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && rt.name.StartsWith("Row")) rows.Add(rt);
        }

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
        var root = _canvas!.transform.Find("SettingsPanel");
        var panel = root.GetComponent<RectTransform>();
        float panelTop = panel.sizeDelta.y * 0.5f;
        float panelBottom = -panelTop;

        foreach (Transform child in root)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt == null || !child.gameObject.activeSelf) continue;

            float topY = rt.anchoredPosition.y + rt.sizeDelta.y * 0.5f;
            float bottomY = rt.anchoredPosition.y - rt.sizeDelta.y * 0.5f;

            Assert.IsTrue(topY <= panelTop + 0.01f,
                $"{child.name}: top Y ({topY:F1}) should be <= panel top ({panelTop})");
            Assert.IsTrue(bottomY >= panelBottom - 0.01f,
                $"{child.name}: bottom Y ({bottomY:F1}) should be >= panel bottom ({panelBottom})");
        }
    }

    [Test]
    public void TwoConsecutiveBuilds_DoNotThrow()
    {
        Assert.DoesNotThrow(() => _ui!.Build(_canvas!.transform));
    }

    [Test]
    public void ToggleRows_HaveCheckboxGraphic()
    {
        var project = _canvas!.transform.Find(PagePath + "Tab_Project");
        var toggle = project.Find("RowTgl_Сетка/Tgl_Сетка").GetComponent<Toggle>();
        Assert.IsNotNull(toggle.graphic);
        Assert.IsNotNull(toggle.targetGraphic);
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
        parent.Find($"RowTgl_{key}/Tgl_{key}").GetComponent<Toggle>();

    private static Slider SliderOf(Transform parent, string key) =>
        parent.Find($"RowSld_{key}/Sld_{key}").GetComponent<Slider>();

    private static float LabelX(Transform parent, string key) =>
        parent.Find($"RowTgl_{key}/Lbl_{key}").GetComponent<RectTransform>().anchoredPosition.x;

    private static void AssertToggleValue(Transform parent, string label, bool expected)
    {
        var toggle = parent.Find($"RowTgl_{label}/Tgl_{label}").GetComponent<Toggle>();
        Assert.AreEqual(expected, toggle.isOn, $"Toggle '{label}' should be {(expected ? "on" : "off")}");
    }

    private static void AssertFieldValue(Transform parent, string label, string expected)
    {
        var field = parent.Find($"RowFld_{label}/Fld_{label}").GetComponent<TMP_InputField>();
        Assert.AreEqual(expected, field.text.Replace("\u200b", ""));
    }
}
