using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;
using KitchenDesigner.Core.UI;

public class MeasurePropertiesUITests
{
    private GameObject? _canvasGo;
    private GameObject? _host;
    private MeasurePropertiesUI? _ui;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();

        _host = new GameObject("MeasureProps");
        _host!.transform.SetParent(_canvasGo!.transform);
        _ui = _host!.AddComponent<MeasurePropertiesUI>();
        _ui.Build(_canvasGo!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        MeasureStore.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private RectTransform Panel() => (RectTransform)_canvasGo!.transform.Find("MeasurePanel")!;

    private static Transform Node(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

    [Test]
    public void MeasureProperties_IsNotAProjectWindow_BecauseMeasuresDieWithTheRulerMode()
    {
        Assert.IsFalse(typeof(IProjectWindow).IsAssignableFrom(typeof(MeasurePropertiesUI)),
            "Замеры живут лишь до выхода из режима рулетки, поэтому окно не регистрируется "
            + "в ProjectWindows: сохранять его состояние в проект нечего и незачем");
        Assert.IsEmpty(ProjectWindows.All.Where(w => w is MeasurePropertiesUI).ToList(),
            "и в реестре его тоже нет");
    }

    [Test]
    public void MeasureProperties_ShowsNumbersReadOnly_WithDeleteAsTheOnlyAction()
    {
        var panel = Panel();

        Assert.IsEmpty(panel.GetComponentsInChildren<TMP_InputField>(true),
            "Замер задаётся вершинами деталей — менять его числами нечего, поэтому в окне "
            + "нет ни одного поля ввода");

        var buttons = panel.GetComponentsInChildren<Button>(true).Select(b => b.name).ToList();
        CollectionAssert.AreEquivalent(new[] { "MeasureDelete", "CloseBtn" }, buttons,
            "единственное действие окна — «Удалить замер», плюс стандартное закрытие");
    }

    [Test]
    public void MeasureProperties_DeleteButton_LivesInTheFooter_AsAnOutline_NotAFullWidthFill()
    {
        var delete = (RectTransform)Node(Panel(), "MeasureDelete");
        var distance = (RectTransform)Node(Panel(), "MeasureDistance");

        Assert.Less(delete.sizeDelta.x, _ui!.BodyWidth,
            "Деструктивное действие не растягивается на всю ширину (правило 3 UI-GUIDELINES)");
        Assert.Less(delete.position.y, distance.position.y,
            "и стоит в футере под линией — отдельно от чисел, по нему не попасть случайно (D5)");
        Assert.AreEqual(UIStyle.DangerText, delete.GetComponentInChildren<TMP_Text>().color,
            "контур DangerText, заливка Danger — только во взводе (D7, tool-panels.md)");
    }

    [Test]
    public void MeasureProperties_ShowsCoordinatesInWholeMillimetres_InATable_NumbersRight()
    {
        var segment = new MeasureSegment(new Vector3(0.1234f, 0f, -0.3f), new Vector3(1.5f, 0f, 0f));
        MeasureStore.Add(segment);
        MeasureStore.Select(segment);

        var table = _ui!.Points!;
        Assert.AreEqual(2, table.ShownRows.Count);
        Assert.AreEqual("123", table.CellLabel(0, 1)!.text,
            "Координаты показываются целыми миллиметрами (правило 1 UI-GUIDELINES): дробные "
            + "юниты сцены человеку не о чём");
        Assert.AreEqual(NumberFormat.Minus + "300", table.CellLabel(0, 3)!.text,
            "отрицательная координата — с типографским минусом «−», а не дефисом (tool-panels.md)");
        Assert.AreEqual(TextAlignmentOptions.Right, table.CellLabel(0, 1)!.alignment, "числа вправо (D8)");
        Assert.AreEqual("X", table.HeaderLabel(1).text);
    }

    [Test]
    public void MeasureProperties_Distance_IsTheBigNumber_WithTheUnitSmallAndQuietBesideIt()
    {
        var segment = new MeasureSegment(new Vector3(0.1f, 0.72f, -0.3f), new Vector3(1.3f, 0.72f, -0.3f));
        MeasureStore.Add(segment);
        MeasureStore.Select(segment);

        var number = (TMP_Text)Node(Panel(), "MeasureDistance").GetComponent<TMP_Text>();
        var unit = (TMP_Text)Node(Panel(), "MeasureUnit").GetComponent<TMP_Text>();

        Assert.AreEqual("1200", number.text, "расстояние — одно число, без единицы внутри строки");
        Assert.AreEqual(UIStyle.FontDisplay, number.fontSize, "главное число окна — FontDisplay 28");
        Assert.AreEqual(Loc.T("unit.mm"), unit.text);
        Assert.AreEqual(UIStyle.FontBody, unit.fontSize, "единица мельче числа");
        Assert.AreEqual(UIStyle.TextSecondary, unit.color, "и тише");
        Assert.Greater(((RectTransform)unit.transform).anchoredPosition.x,
            number.GetPreferredValues(number.text).x, "единица стоит правее числа");
    }
}
