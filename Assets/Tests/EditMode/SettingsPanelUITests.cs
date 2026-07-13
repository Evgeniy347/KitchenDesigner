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
    private Canvas _canvas;
    private SettingsPanelUI _ui;

    [SetUp]
    public void Setup()
    {
        var go = new GameObject("TestCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _ui = _canvas.gameObject.AddComponent<SettingsPanelUI>();
        _ui.Build(_canvas.transform);
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
    }

    // ── Build / smoke ───────────────────────────────────────

    [Test]
    public void Build_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _ui.Build(_canvas.transform));
    }

    [Test]
    public void Panel_Exists_WithCorrectSize()
    {
        var panel = _canvas.transform.Find("SettingsPanel");
        Assert.IsNotNull(panel);
        var rt = panel.GetComponent<RectTransform>();
        Assert.AreEqual(440, rt.sizeDelta.x, 0.01f);
        Assert.AreEqual(680, rt.sizeDelta.y, 0.01f);
    }

    [Test]
    public void Title_Exists_WithCorrectText()
    {
        var title = _canvas.transform.Find("SettingsPanel/SetTitle");
        Assert.IsNotNull(title);
        var label = title.GetComponent<TextMeshProUGUI>();
        Assert.AreEqual("Настройки", label.text.Replace("\u200b", ""));
        Assert.AreEqual(24, label.fontSize);
    }

    // ── Tabs ────────────────────────────────────────────────

    [Test]
    public void ThreeTabButtons_Exist()
    {
        for (int i = 0; i < 3; i++)
        {
            var tab = _canvas.transform.Find($"SettingsPanel/Tab_{i}");
            Assert.IsNotNull(tab, $"Tab_{i} should exist");
        }
    }

    [Test]
    public void TabButtons_HaveCorrectLabels()
    {
        string[] expected = { "Проект", "Графика", "О программе" };
        for (int i = 0; i < expected.Length; i++)
        {
            var tab = _canvas.transform.Find($"SettingsPanel/Tab_{i}");
            var label = tab.Find($"Tab_{i}_Label");
            Assert.IsNotNull(label, $"Tab_{i}_Label should exist");
            Assert.AreEqual(expected[i], label.GetComponent<TextMeshProUGUI>().text.Replace("\u200b", ""));
        }
    }

    [Test]
    public void ThreeTabPages_Exist()
    {
        Assert.IsNotNull(_canvas.transform.Find("SettingsPanel/Tab_Project"), "Tab_Project page should exist");
        Assert.IsNotNull(_canvas.transform.Find("SettingsPanel/Tab_Graphics"), "Tab_Graphics page should exist");
        Assert.IsNotNull(_canvas.transform.Find("SettingsPanel/Tab_About"), "Tab_About page should exist");
    }

    [Test]
    public void SwitchTab_OnlyActivePageVisible()
    {
        var project = _canvas.transform.Find("SettingsPanel/Tab_Project").gameObject;
        var graphics = _canvas.transform.Find("SettingsPanel/Tab_Graphics").gameObject;
        var about = _canvas.transform.Find("SettingsPanel/Tab_About").gameObject;

        Assert.IsTrue(project.activeSelf, "Project tab should be active by default");
        Assert.IsFalse(graphics.activeSelf, "Graphics tab should be hidden by default");
        Assert.IsFalse(about.activeSelf, "About tab should be hidden by default");

        var tab2Btn = _canvas.transform.Find("SettingsPanel/Tab_1").GetComponent<Button>();
        tab2Btn.onClick.Invoke();
        Assert.IsFalse(project.activeSelf, "Project should hide after switching to Graphics");
        Assert.IsTrue(graphics.activeSelf, "Graphics should show after click");
        Assert.IsFalse(about.activeSelf);

        var tab3Btn = _canvas.transform.Find("SettingsPanel/Tab_2").GetComponent<Button>();
        tab3Btn.onClick.Invoke();
        Assert.IsFalse(project.activeSelf);
        Assert.IsFalse(graphics.activeSelf);
        Assert.IsTrue(about.activeSelf, "About should show after click");
    }

    [Test]
    public void ActiveTab_HasHighlightColor()
    {
        var tab0Img = _canvas.transform.Find("SettingsPanel/Tab_0").GetComponent<Image>();
        var tab1Img = _canvas.transform.Find("SettingsPanel/Tab_1").GetComponent<Image>();

        Color active = new(0.28f, 0.33f, 0.42f, 1f);
        Color inactive = new(0.15f, 0.16f, 0.20f, 1f);

        AssertColorEqual(active, tab0Img.color);
        AssertColorEqual(inactive, tab1Img.color);

        var tab1Btn = _canvas.transform.Find("SettingsPanel/Tab_1").GetComponent<Button>();
        tab1Btn.onClick.Invoke();

        AssertColorEqual(inactive, tab0Img.color);
        AssertColorEqual(active, tab1Img.color);
    }

    // ── Visibility ──────────────────────────────────────────

    [Test]
    public void Panel_StartsHidden()
    {
        var panel = _canvas.transform.Find("SettingsPanel");
        Assert.IsFalse(panel.gameObject.activeSelf);
    }

    [Test]
    public void SetVisible_ShowsAndHides()
    {
        var panel = _canvas.transform.Find("SettingsPanel").gameObject;
        _ui.SetVisible(true);
        Assert.IsTrue(panel.activeSelf);
        _ui.SetVisible(false);
        Assert.IsFalse(panel.activeSelf);
    }

    [Test]
    public void Toggle_FlipsVisibility()
    {
        var panel = _canvas.transform.Find("SettingsPanel").gameObject;
        Assert.IsFalse(panel.activeSelf);
        _ui.Toggle();
        Assert.IsTrue(panel.activeSelf);
        _ui.Toggle();
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
        var btn = _canvas.transform.Find("SettingsPanel/SetClose");
        Assert.IsNotNull(btn);
    }

    [Test]
    public void CloseButton_HidesPanel()
    {
        var panel = _canvas.transform.Find("SettingsPanel").gameObject;
        _ui.SetVisible(true);
        Assert.IsTrue(panel.activeSelf);

        var btn = _canvas.transform.Find("SettingsPanel/SetClose").GetComponent<Button>();
        btn.onClick.Invoke();
        Assert.IsFalse(panel.activeSelf);
    }

    [Test]
    public void CloseButton_IsBelowTabs()
    {
        var panel = _canvas.transform.Find("SettingsPanel").GetComponent<RectTransform>();
        var closeBtn = _canvas.transform.Find("SettingsPanel/SetClose").GetComponent<RectTransform>();
        var projectTab = _canvas.transform.Find("SettingsPanel/Tab_Project").GetComponent<RectTransform>();

        float closeY = closeBtn.anchoredPosition.y;
        Assert.IsTrue(closeY < 0, "Close button should be below the panel center");
    }

    // ── Project tab: toggle rows ────────────────────────────

    [Test]
    public void ProjectTab_HasAllToggleRows()
    {
        string[] expectedToggles =
        {
            "Сетка", "Снэппинг", "Блокировать ошибки", "Автосохранение",
            "Пространственная сетка", "Оконный режим", "Контур (чёрные рёбра)",
            "Стены", "Опускать ближние стены"
        };

        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");

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

    [Test]
    public void ToggleRows_HaveCorrectInitialValues()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");

        AssertToggleValue(project, "Сетка", s.GridEnabled);
        AssertToggleValue(project, "Снэппинг", s.SnapEnabled);
        AssertToggleValue(project, "Блокировать ошибки", s.BlockOnViolation);
        AssertToggleValue(project, "Автосохранение", s.AutoSave);
        AssertToggleValue(project, "Пространственная сетка", s.SpatialGrid);
        AssertToggleValue(project, "Оконный режим", s.WindowedMode);
        AssertToggleValue(project, "Контур (чёрные рёбра)", s.EdgeOutline);
        AssertToggleValue(project, "Стены", s.WallsEnabled);
        AssertToggleValue(project, "Опускать ближние стены", s.LowerNearWalls);
    }

    [Test]
    public void ToggleRow_UpdatesLabel_OnValueChange()
    {
        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
        var toggleObj = project.Find("RowTgl_Сетка/Tgl_Сетка");
        var label = toggleObj.Find("Tgl_Сетка_Label").GetComponent<TextMeshProUGUI>();

        Assert.AreEqual("Вкл", label.text.Replace("\u200b", ""));

        var toggle = toggleObj.GetComponent<Toggle>();
        toggle.isOn = false;
        Assert.AreEqual("Выкл", label.text.Replace("\u200b", ""));
    }

    [Test]
    public void ToggleRow_InvokesCallback()
    {
        var s = KitchenSettings.Instance;
        bool prev = s.GridEnabled;

        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
        var toggle = project.Find("RowTgl_Сетка/Tgl_Сетка").GetComponent<Toggle>();
        toggle.isOn = !prev;

        Assert.AreEqual(!prev, s.GridEnabled);

        s.GridEnabled = prev;
        s.Save();
    }

    // ── Project tab: input rows ─────────────────────────────

    [Test]
    public void ProjectTab_HasAllInputRows()
    {
        string[] expectedInputs =
        {
            "Шаг сетки, мм", "Порог снэпа, мм", "Интервал автосейва, с"
        };

        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");

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
        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");

        AssertFieldValue(project, "Шаг сетки, мм", s.GridStep.ToString());
        AssertFieldValue(project, "Порог снэпа, мм", s.SnapThreshold.ToString("F0"));
        AssertFieldValue(project, "Интервал автосейва, с", s.AutoSaveInterval.ToString());
    }

    [Test]
    public void InputField_UpdatesSetting_OnValidInput()
    {
        var s = KitchenSettings.Instance;
        int prev = s.GridStep;

        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
        var field = project.Find("RowFld_Шаг сетки, мм/Fld_Шаг сетки, мм").GetComponent<TMP_InputField>();
        field.text = "42";
        field.onEndEdit.Invoke("42");

        Assert.AreEqual(42, s.GridStep);

        s.GridStep = prev;
        s.Save();
    }

    [Test]
    public void InputField_IgnoresInvalidInput()
    {
        var s = KitchenSettings.Instance;
        int prev = s.GridStep;

        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
        var field = project.Find("RowFld_Шаг сетки, мм/Fld_Шаг сетки, мм").GetComponent<TMP_InputField>();
        field.text = "abc";
        field.onEndEdit.Invoke("abc");

        Assert.AreEqual(prev, s.GridStep);

        s.Save();
    }

    [Test]
    public void InputField_HasOutlineComponent()
    {
        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
        var field = project.Find("RowFld_Шаг сетки, мм/Fld_Шаг сетки, мм").GetComponent<TMP_InputField>();
        var outline = field.GetComponent<Outline>();
        Assert.IsNotNull(outline, "InputField should have Outline component");
    }

    // ── About tab ───────────────────────────────────────────

    [Test]
    public void AboutTab_HasVersionAndBuildDate()
    {
        var about = _canvas.transform.Find("SettingsPanel/Tab_About");
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

    // ── Graphics tab ────────────────────────────────────────

    [Test]
    public void GraphicsTab_Exists_WithNoContent()
    {
        var graphics = _canvas.transform.Find("SettingsPanel/Tab_Graphics");
        Assert.IsNotNull(graphics);
        Assert.AreEqual(0, graphics.childCount, "Graphics tab should have no child rows");
    }

    // ── Row ordering ────────────────────────────────────────

    [Test]
    public void ProjectRows_AreOrderedDescending()
    {
        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
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
        var root = _canvas.transform.Find("SettingsPanel");
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
        Assert.DoesNotThrow(() => _ui.Build(_canvas.transform));
    }

    [Test]
    public void ToggleRows_HaveCheckboxGraphic()
    {
        var project = _canvas.transform.Find("SettingsPanel/Tab_Project");
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
