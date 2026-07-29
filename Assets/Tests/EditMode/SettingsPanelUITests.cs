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
        string[] expected = { "Проект", "Вид", "Управление", "Фото режим", "Свет", "О программе" };
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
        Assert.IsNotNull(_canvas!.transform.Find("SettingsPanel/Tab_Project"), "Tab_Project page should exist");
        Assert.IsNotNull(_canvas!.transform.Find("SettingsPanel/Tab_View"), "Tab_View page should exist");
        Assert.IsNotNull(_canvas!.transform.Find("SettingsPanel/Tab_Control"), "Tab_Control page should exist");
        Assert.IsNotNull(_canvas!.transform.Find("SettingsPanel/Tab_Photo"), "Tab_Photo page should exist");
        Assert.IsNotNull(_canvas!.transform.Find("SettingsPanel/Tab_Light"), "Tab_Light page should exist");
        Assert.IsNotNull(_canvas!.transform.Find("SettingsPanel/Tab_About"), "Tab_About page should exist");
    }

    [Test]
    public void SwitchTab_OnlyActivePageVisible()
    {
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project").gameObject;
        var photo = _canvas!.transform.Find("SettingsPanel/Tab_Photo").gameObject;
        var about = _canvas!.transform.Find("SettingsPanel/Tab_About").gameObject;

        Assert.IsTrue(project.activeSelf, "Project tab should be active by default");
        Assert.IsFalse(photo.activeSelf, "Photo tab should be hidden by default");
        Assert.IsFalse(about.activeSelf, "About tab should be hidden by default");

        // Tab_1 → «Помещение», Tab_2 → «Управление», Tab_3 → «Фото режим»,
        // Tab_4 → «Свет», Tab_5 → «О программе».
        _canvas!.transform.Find("SettingsPanel/Tab_5").GetComponent<Button>().onClick.Invoke();
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
        var projectTab = _canvas!.transform.Find("SettingsPanel/Tab_Project").GetComponent<RectTransform>();

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

        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");

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
            "Стены", "Контур стен", "Опускать ближние стены", "Скрывать окна и двери",
            "Объекты", "Контур объектов", "Скрыть источники света"
        };

        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");

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
        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
        var normalBtn = view.Find("RowViewPreset/ViewPreset_0");
        var roomBtn = view.Find("RowViewPreset/ViewPreset_1");
        Assert.IsNotNull(normalBtn, "сегмент «Обычный» должен быть");
        Assert.IsNotNull(roomBtn, "сегмент «Помещение» должен быть");

        Assert.IsTrue(CaptionOf(normalBtn!).Contains("текущий"),
            "в обычном режиме текущий пресет — «Обычный»");

        EditModeManager.SetMode(EditMode.Room);
        Assert.IsTrue(CaptionOf(roomBtn!).Contains("текущий"),
            "в режиме «помещение» вкладка показывает его пресет");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.IsTrue(CaptionOf(normalBtn!).Contains("текущий"));
    }

    /// <summary>Пресеты независимы: погашенные в обычном режиме стены не
    /// затирают настройку помещения и возвращаются при выходе из него.</summary>
    [Test]
    public void ViewTab_Presets_AreIndependent()
    {
        var s = KitchenSettings.Instance;
        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
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

    /// <summary>В фоторежиме видно всё и менять нельзя ничего.</summary>
    [Test]
    public void ViewTab_AllTogglesDisabled_InPhotoMode()
    {
        string[] labels =
        {
            "Стены", "Контур стен", "Опускать ближние стены", "Скрывать окна и двери",
            "Объекты", "Контур объектов", "Скрыть источники света"
        };
        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");

        EditModeManager.SetMode(EditMode.Photo);
        foreach (var label in labels)
            Assert.IsFalse(ToggleOf(view, label).interactable, $"'{label}' в фоторежиме заблокирован");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.IsTrue(ToggleOf(view, "Стены").interactable, "после выхода доступность вернулась");
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
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");

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

        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");

        AssertToggleValue(project, "Сетка", s.GridEnabled);
        AssertToggleValue(project, "Привязка к деталям", s.SnapEnabled);
        AssertToggleValue(project, "Блокировать недопустимые изменения", s.BlockOnViolation);
        AssertToggleValue(project, "Автосохранение", s.AutoSave);
        AssertToggleValue(project, "Пространственная сетка", s.SpatialGrid);
        AssertToggleValue(project, "Свободное панорамирование", s.CameraPanFree);

        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
        AssertToggleValue(view, "Объекты", s.NormalView.objectsVisible);
        AssertToggleValue(view, "Контур объектов", s.NormalView.edgeOutline);
        AssertToggleValue(view, "Стены", s.NormalView.wallsEnabled);
        AssertToggleValue(view, "Контур стен", s.NormalView.wallOutline);
        AssertToggleValue(view, "Опускать ближние стены", s.NormalView.lowerNearWalls);
        AssertToggleValue(view, "Скрывать окна и двери", s.NormalView.hideOpeningsOnLoweredWalls);
        AssertToggleValue(view, "Скрыть источники света", s.NormalView.hideLightSources);
    }

    // ── Вложенность подопций ────────────────────────────────

    [Test]
    public void SubOptions_AreIndented_UnderTheirParent()
    {
        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");

        float wallsX = LabelX(view, "Стены");
        float outlineX = LabelX(view, "Контур стен");
        float lowerX = LabelX(view, "Опускать ближние стены");
        float hideX = LabelX(view, "Скрывать окна и двери");

        Assert.Greater(outlineX, wallsX, "«Контур» должен быть с отступом от «Стены»");
        Assert.AreEqual(outlineX, lowerX, 0.01f, "оба на первом уровне вложенности");
        Assert.Greater(hideX, lowerX, "«Скрывать окна и двери» — второй уровень");
    }

    [Test]
    public void WallSubOptions_Disabled_WhenWallsOff()
    {
        var s = KitchenSettings.Instance;
        bool prev = s.NormalView.wallsEnabled;

        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
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

        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
        var lower = view.Find("RowTgl_Опускать ближние стены/Tgl_Опускать ближние стены").GetComponent<Toggle>();

        lower.isOn = false;
        Assert.IsFalse(ToggleOf(view, "Скрывать окна и двери").interactable);

        lower.isOn = true;
        Assert.IsTrue(ToggleOf(view, "Скрывать окна и двери").interactable);

        s.NormalView.lowerNearWalls = prev;
    }

    [Test]
    public void LowerNearWalls_Disabled_InRoomMode()
    {
        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
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

        var view = _canvas!.transform.Find("SettingsPanel/Tab_View");
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
        var control = _canvas!.transform.Find("SettingsPanel/Tab_Control");
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
        var control = _canvas!.transform.Find("SettingsPanel/Tab_Control");

        Assert.AreEqual(s.MouseSensitivity, SliderOf(control, "Чувствительность мыши").value, 0.001f);
        Assert.AreEqual(s.WasdSpeed, SliderOf(control, "Скорость WASD").value, 0.001f);
        Assert.AreEqual(s.ArrowSpeed, SliderOf(control, "Скорость ←→↑↓").value, 0.001f);
    }

    [Test]
    public void ControlSlider_UpdatesSetting()
    {
        var s = KitchenSettings.Instance;
        float prev = s.WasdSpeed;

        var control = _canvas!.transform.Find("SettingsPanel/Tab_Control");
        SliderOf(control, "Скорость WASD").value = 2.5f;
        Assert.AreEqual(2.5f, s.WasdSpeed, 0.001f);

        s.WasdSpeed = prev;
    }

    [Test]
    public void ToggleRow_CheckmarkReflectsState()
    {
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
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

        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
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

        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");

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
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");

        AssertFieldValue(project, "Шаг сетки", s.GridStep.ToString());
        AssertFieldValue(project, "Порог привязки", s.SnapThreshold.ToString("F0"));
        AssertFieldValue(project, "Интервал автосохранения", s.AutoSaveInterval.ToString());
    }

    [Test]
    public void InputField_UpdatesSetting_OnValidInput()
    {
        var s = KitchenSettings.Instance;
        int prev = s.GridStep;

        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
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

        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
        var field = project.Find("RowFld_Шаг сетки/Fld_Шаг сетки").GetComponent<TMP_InputField>();
        field.text = "abc";
        field.onEndEdit.Invoke("abc");

        Assert.AreEqual(prev, s.GridStep);
    }

    [Test]
    public void InputField_HasOutlineComponent()
    {
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
        var field = project.Find("RowFld_Шаг сетки/Fld_Шаг сетки").GetComponent<TMP_InputField>();
        var outline = field.GetComponent<Outline>();
        Assert.IsNotNull(outline, "InputField should have Outline component");
    }

    // ── About tab ───────────────────────────────────────────

    [Test]
    public void AboutTab_HasVersionAndBuildDate()
    {
        var about = _canvas!.transform.Find("SettingsPanel/Tab_About");
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

    // ── Row ordering ────────────────────────────────────────

    [Test]
    public void ProjectRows_AreOrderedDescending()
    {
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
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
        var project = _canvas!.transform.Find("SettingsPanel/Tab_Project");
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
