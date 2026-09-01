using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Measure;
using KitchenDesigner.Core.UI;

public class MeasurePropertiesUITests
{
    private GameObject? _canvasGo;
    private GameObject? _host;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();

        _host = new GameObject("MeasureProps");
        _host!.transform.SetParent(_canvasGo!.transform);
        _host!.AddComponent<MeasurePropertiesUI>().Build(_canvasGo!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        MeasureStore.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private RectTransform Panel() => (RectTransform)_canvasGo!.transform.Find("MeasurePanel")!;

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
            "единственное действие окна — «Удалить», плюс стандартное закрытие");
    }

    [Test]
    public void MeasureProperties_DeleteButton_IsNarrowerThanTheRow_AndSetApartFromIt()
    {
        var panel = Panel();
        var delete = (RectTransform)panel.Find("MeasureDelete")!;
        var distance = (RectTransform)panel.Find("MeasureDistance")!;

        Assert.Less(delete.sizeDelta.x, MeasurePropertiesUI.RowWidth,
            "Деструктивное действие не растягивается на всю ширину (правило 3 UI-GUIDELINES)");
        float gap = distance.anchoredPosition.y - distance.sizeDelta.y * 0.5f
            - (delete.anchoredPosition.y + delete.sizeDelta.y * 0.5f);
        Assert.GreaterOrEqual(gap, UIStyle.GapSection,
            "и отделяется от прочих контролов отступом между смысловыми группами, чтобы по "
            + "нему не попали случайно");
    }

    [Test]
    public void MeasureProperties_ShowsCoordinatesInWholeMillimetres()
    {
        var segment = new MeasureSegment(new Vector3(0.1234f, 0f, 0f), new Vector3(1.5f, 0f, 0f));
        MeasureStore.Add(segment);
        MeasureStore.Select(segment);

        var a = Panel().Find("MeasureA")!.GetComponent<TMP_Text>();

        Assert.AreEqual("Точка A: X 123, Y 0, Z 0 мм", a.text,
            "Координаты показываются целыми миллиметрами (правило 1 UI-GUIDELINES): дробные "
            + "юниты сцены человеку не о чём");
    }
}
