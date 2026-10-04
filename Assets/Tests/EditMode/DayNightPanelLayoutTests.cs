using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class DayNightPanelLayoutTests
{
    private GameObject? _canvasGo;
    private DayNightPanelUI? _ui;

    [SetUp]
    public void Setup()
    {
        SunController.Reset();
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();
        var go = new GameObject("DayNight");
        go.transform.SetParent(_canvasGo.transform);
        _ui = go.AddComponent<DayNightPanelUI>();
        _ui.Build(_canvasGo.transform);
        _ui.SetVisible(true);
    }

    [TearDown]
    public void Teardown()
    {
        SunController.Reset();
        ProjectWindows.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private RectTransform Panel => (RectTransform)_canvasGo!.transform.Find("DayNightPanel")!;

    private static Rect World(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private T Find<T>(string name) where T : Component =>
        Panel.GetComponentsInChildren<T>(true).First(c => c.name == name);

    [Test]
    public void DayNightPanel_ResetLink_DoesNotStickToTheBottomEdge()
    {
        var reset = World((RectTransform)Panel.GetComponentsInChildren<Transform>(true).First(t => t.name == "DnReset"));
        var panel = World(Panel);

        Assert.GreaterOrEqual(reset.yMin - panel.yMin, UIStyle.ToolPanelPad,
            "контенту нужен отступ снизу не меньше паддинга панели-инструмента (D2)");
    }

    [Test]
    public void TheWindow_IsAToolPanelOfTheStandardWidth_WithAQuietClose()
    {
        Assert.AreEqual(UIStyle.ToolPanelW, Panel.sizeDelta.x, "ширина панели — токен ToolPanelW (D5)");
        Assert.IsNotNull(Panel.Find(WindowChrome.CloseButtonName));
        Assert.AreEqual("День / Ночь", Panel.GetComponentsInChildren<TMP_Text>(true)
            .First(t => t.name == "DayNightPanelTitle").text);
    }

    [Test]
    public void EachRow_IsALabelThenASliderThenAnExactValueField_LeftToRight()
    {
        foreach (var (node, label) in new[] { ("DnTime", "Время"), ("DnAzimuth", "Азимут"), ("DnIntensity", "Яркость") })
        {
            var caption = Find<TMP_Text>("L_" + node);
            var slider = World((RectTransform)Find<Slider>("Sld_" + node).transform);
            var field = World((RectTransform)Find<TMP_InputField>("F_" + node).transform);

            Assert.AreEqual(label, caption.text, "подпись — только имя величины, значение живёт в поле справа");
            Assert.Less(World(caption.rectTransform).xMax, slider.xMin + 0.5f, node + ": подпись левее ползунка");
            Assert.Less(slider.xMax, field.xMin, node + ": ползунок левее поля");
            Assert.AreEqual(UIStyle.ToolFieldW, field.width, 0.5f, node + ": поле значения — токен ToolFieldW (72)");
            Assert.AreEqual(UIStyle.ControlHCompact, field.height, 0.5f);
        }
    }

    [Test]
    public void TheFields_ShowTheCurrentSun_InTheirOwnFormat()
    {
        Assert.AreEqual("12:00", Find<TMP_InputField>("F_DnTime").text,
            "время — ЧЧ:ММ, без «(день)»: фаза суток из значения убрана");
        Assert.AreEqual("135", Find<TMP_InputField>("F_DnAzimuth").text);
        Assert.AreEqual("1,00", Find<TMP_InputField>("F_DnIntensity").text, "яркость — две цифры, запятая языка");
        Assert.IsTrue(Find<TMP_InputField>("F_DnAzimuth").GetComponentsInChildren<TMP_Text>(true)
            .Any(t => t.text == "°"), "у азимута в поле единица «°»");
    }

    [Test]
    public void TypingATime_MovesTheSun_TheSlider_AndRewritesTheField()
    {
        var field = Find<TMP_InputField>("F_DnTime");
        field.text = "18:30";
        field.onEndEdit.Invoke("18:30");

        Assert.AreEqual(18.5f, SunController.TimeOfDay, 1e-4f, "точный ввод времени — закрытый пункт чек-листа Этапа 5");
        Assert.AreEqual(18.5f, Find<Slider>("Sld_DnTime").value, 1e-4f, "ползунок идёт за полем");
        Assert.AreEqual("18:30", field.text);
    }

    [Test]
    public void TypingGarbage_KeepsTheSun_AndRestoresTheField()
    {
        var field = Find<TMP_InputField>("F_DnTime");
        field.text = "25:99";
        field.onEndEdit.Invoke("25:99");

        Assert.AreEqual(SunController.DEFAULT_TIME, SunController.TimeOfDay, "невозможное время солнце не двигает");
        Assert.AreEqual("12:00", field.text, "поле возвращает показанное значение");
        Assert.IsTrue(field.GetComponent<Outline>().enabled, "ошибка ввода подсвечена");
    }

    [Test]
    public void TypingAnAzimuthAndABrightness_AppliesThem()
    {
        var azimuth = Find<TMP_InputField>("F_DnAzimuth");
        azimuth.onEndEdit.Invoke("200");
        var intensity = Find<TMP_InputField>("F_DnIntensity");
        intensity.onEndEdit.Invoke("1,5");

        Assert.AreEqual(200f, SunController.Azimuth, 1e-3f);
        Assert.AreEqual(1.5f, SunController.Intensity, 1e-3f);
        Assert.AreEqual("1,50", intensity.text);
    }

    [Test]
    public void MovingASlider_RewritesItsField()
    {
        Find<Slider>("Sld_DnTime").value = 6.25f;

        Assert.AreEqual("06:15", Find<TMP_InputField>("F_DnTime").text);
        Assert.AreEqual(6.25f, SunController.TimeOfDay, 1e-4f);
    }

    [Test]
    public void ResetLink_IsAQuietLink_AndPutsTheSunBackAtNoon()
    {
        SunController.SetTimeOfDay(3f);
        SunController.SetAzimuth(10f);
        var reset = Find<Button>("DnReset");

        Assert.AreEqual(0f, reset.colors.normalColor.a, 1e-4f, "«Сбросить на полдень» — ссылка без заливки (D5)");
        Assert.AreEqual(UIStyle.AccentText, reset.GetComponentInChildren<TMP_Text>().color);
        Assert.AreEqual("Сбросить на полдень", reset.GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual(World(Find<TMP_Text>("L_DnTime").rectTransform).xMin, World((RectTransform)reset.transform).xMin, 0.5f,
            "ссылка стоит у левого края тела, под подписями, а не посередине окна");

        reset.onClick.Invoke();

        Assert.AreEqual("12:00", Find<TMP_InputField>("F_DnTime").text);
        Assert.AreEqual("135", Find<TMP_InputField>("F_DnAzimuth").text);
        Assert.AreEqual(12f, Find<Slider>("Sld_DnTime").value, 1e-4f);
    }
}
