using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class LightFieldsEditorTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private LightSourceElement Lamp()
    {
        var go = ElementFactory.CreateLightSource("Лампа", new Vector3(0f, 2f, 0f));
        _spawned.Add(go);
        return go.GetComponent<LightSourceElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_InputField Field(string label) =>
        Panel().Find($"F_{label}")!.GetComponent<TMP_InputField>();

    private static string Text(TMP_InputField f) => f.text.Replace("​", "");

    [Test]
    public void LightRows_KeepTheirWidgetNames()
    {
        foreach (var label in new[] { "Температура", "Мощность", "Рассеивание", "Угол пучка",
                     "Мягкость края", "Свет вверх", "Сила тени", "Свечение плафона",
                     "Отступ вниз", "Верхний конус", "Верхний радиус", "Радиус при 0 %",
                     "Радиус при 100 %", "Светоотдача", "Калибровка" })
            Assert.NotNull(Panel().Find($"F_{label}"), $"поле F_{label} ищется по имени");
        Assert.NotNull(Panel().Find("CtxLightShape"));
        Assert.NotNull(Panel().Find("CtxLightShadow"));
        Assert.NotNull(Panel().Find("CtxLightAdv"));
    }

    [Test]
    public void Open_ShowsEveryLampParameter()
    {
        var lamp = Lamp();
        lamp.TemperatureK = 4200;
        lamp.GlowPct = 42;
        _menu!.Open(lamp);

        Assert.AreEqual("4200", Text(Field("Температура")),
            "каждый параметр лампы обязан приезжать в поле сам: пятнадцать одинаковых "
            + "простыней держались синхронными только руками");
        Assert.AreEqual("42", Text(Field("Свечение плафона")),
            "поля под раскрывашкой заполняются наравне с остальными");
    }

    [Test]
    public void ApplyFromField_WritesThePropertyBack()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);

        Field("Мощность").text = "17";
        Field("Мощность").onEndEdit.Invoke("17");

        Assert.AreEqual(17, lamp.PowerW, "правка применяется сразу (правило 2 UI-GUIDELINES)");
    }

    [Test]
    public void ApplyFromField_ShowsTheClampedValue_NotTheTypedOne()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);

        Field("Рассеивание").text = "900";
        Field("Рассеивание").onEndEdit.Invoke("900");

        Assert.AreEqual(lamp.DiffusionPct.ToString(), Text(Field("Рассеивание")),
            "значение склампилось — в поле обязано стоять применённое, а не введённое");
    }

    [Test]
    public void AdvancedRows_AreHiddenUntilTheExpanderIsClicked()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);
        Assert.IsFalse(Field("Светоотдача").gameObject.activeSelf,
            "калибровка светотехники нужна редко, а места занимает больше всех остальных "
            + "параметров лампы вместе — панель открывается со свёрнутой раскрывашкой");

        Panel().Find("CtxLightAdv")!.GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(Field("Светоотдача").gameObject.activeSelf, "клик раскрывает тонкую настройку");
    }

    [Test]
    public void OpeningAnotherElement_CollapsesTheAdvancedRowsAgain()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);
        Panel().Find("CtxLightAdv")!.GetComponent<Button>().onClick.Invoke();
        Assume.That(Field("Светоотдача").gameObject.activeSelf, Is.True);

        _menu!.Open(lamp);

        Assert.IsFalse(Field("Светоотдача").gameObject.activeSelf,
            "раскрывашка не должна переживать открытие панели");
    }

    [Test]
    public void ShapeDropdown_SecondItemIsTheSphere()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);

        Panel().Find("CtxLightShape")!.GetComponent<TMP_Dropdown>().value = 1;

        Assert.AreEqual(LampShape.Sphere, lamp.Shape,
            "порядок пунктов «Плафон / Шар» — контракт: индекс кастуется в форму потока");
    }

    [Test]
    public void ShadowDropdown_FollowsTheLampShadowEnumOrder()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);
        var dd = Panel().Find("CtxLightShadow")!.GetComponent<TMP_Dropdown>();

        Assert.AreEqual(System.Enum.GetValues(typeof(LampShadow)).Length, dd.options.Count,
            "у каждого значения LampShadow обязан быть пункт");
        dd.value = 2;
        Assert.AreEqual((LampShadow)2, lamp.Shadow);
    }

    [Test]
    public void ArithmeticIsAcceptedInLightFields()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);

        Assert.AreEqual('+', Field("Мощность").onValidateInput("", 0, '+'),
            "арифметика разрешена во всех числовых полях лампы, а не только в первых пяти");
    }

    [Test]
    public void RefreshFromScene_UpdatesLightFields_WhenChangedOutsideThePanel()
    {
        var lamp = Lamp();
        _menu!.Open(lamp);
        lamp.GlowPct = 33;

        _menu!.RefreshTransformFields();

        Assert.AreEqual("33", Text(Field("Свечение плафона")),
            "MCP и undo правят лампу мимо панели — поля обязаны догонять");
    }
}
