using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>
/// <see cref="InspectorRows"/> — тонкая надстройка над <see cref="FormRows"/>: видимость по фасетам
/// элемента, секции, у которых шапка гаснет вместе с содержимым, и вычисляемые габариты. Колонки,
/// шаг и контролы проверяет <see cref="FormRowsTests"/>; здесь — только то, что добавляет
/// инспектор.
/// </summary>
public class InspectorRowsTests
{
    private readonly List<GameObject> _spawned = new();
    private ElementFacet _facets = ElementFacet.None;
    private InspectorRows _rows = null!;
    private RectTransform _host = null!;

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("RowsCanvas");
        _spawned.Add(canvas.gameObject);
        _host = UIFactory.CreateRect("Host", canvas.transform);
        _facets = ElementFacet.None;
        _rows = new InspectorRows(_host, () => _facets);
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void FacetRow_IsShownOnlyForItsFacet()
    {
        var drawerField = _rows.NumberField("Ширина короба", RowVisibility.For(ElementFacet.Drawer));
        _rows.Relayout();
        Assert.IsFalse(drawerField.transform.IsShown(), "у детали строки ящика нет");

        _facets = ElementFacet.Drawer;
        _rows.Relayout();
        Assert.IsTrue(drawerField.transform.IsShown(), "у ящика строка есть");
    }

    [Test]
    public void SectionHeader_FollowsItsMembers()
    {
        var section = _rows.BeginSection("Specific", "Ящик", true);
        _rows.NumberField("Ширина короба", RowVisibility.For(ElementFacet.Drawer));
        _rows.EndSection();

        _rows.Relayout();
        Assert.IsFalse(section.View.transform.IsShown(),
            "секция без единой видимой строки не показывает и заголовок: пустой «Ящик» у детали — шум");

        _facets = ElementFacet.Drawer;
        _rows.Relayout();
        Assert.IsTrue(section.View.transform.IsShown(), "появилась строка — появился и заголовок");
    }

    [Test]
    public void CollapsedSection_HidesItsRows_ButNotItsHeader()
    {
        var section = _rows.BeginSection("Gaps", "Зазоры", true);
        var field = _rows.NumberField("Зазор", RowVisibility.Always);
        _rows.EndSection();
        _rows.Relayout();
        Assert.IsTrue(field.transform.IsShown());

        section.View.SetExpanded(false, notify: true);

        Assert.IsFalse(field.transform.IsShown(), "свёрнутая секция прячет строки");
        Assert.IsTrue(section.View.transform.IsShown(), "а заголовок остаётся, чтобы её можно было открыть");
    }

    [Test]
    public void Relayout_StacksTheVisibleRowsWithoutGaps()
    {
        _rows.NumberField("Одна", RowVisibility.For(ElementFacet.Drawer));
        var second = _rows.NumberField("Вторая", RowVisibility.Always);
        var third = _rows.NumberField("Третья", RowVisibility.Always);

        _rows.Relayout();

        float step = _rows.Metrics.RowStep;
        Assert.AreEqual(0f, RowTop(second), 0.01f, "скрытая строка не оставляет дырки: вторая стоит первой");
        Assert.AreEqual(-step, RowTop(third), 0.01f, "шаг строки — из RowMetrics, а не из числа по месту");
    }

    [Test]
    public void NumberOrComputed_ShowsTheEditableRow_UntilTheFieldIsLocked()
    {
        var row = _rows.NumberOrComputed("Ширина", RowVisibility.Always);
        _rows.Relayout();
        Assert.IsTrue(row.Field.transform.IsShown(), "правится — поле видно");
        Assert.IsNull(_host.FindNode("Val_Computed_Ширина").IsShownOrNull());

        row.Field.text = "400";
        UIRowEnabled.SetControlEnabled(row.Field, false);
        _rows.Relayout();
        _rows.SyncComputed();

        Assert.IsFalse(row.Field.transform.IsShown(), "вычисляемое значение не рисуется полем с рамкой (D10)");
        var shown = _host.FindNode("Val_Computed_Ширина").GetComponent<TMP_Text>();
        Assert.IsTrue(shown.transform.IsShown(), "вместо поля — текст без рамки");
        Assert.AreEqual(NumberFormat.WithUnit("400", "мм"), shown.text, "текст несёт значение поля вместе с единицей");
    }

    [Test]
    public void NumberOrComputed_FollowsTheFieldWhileLocked()
    {
        var row = _rows.NumberOrComputed("Высота", RowVisibility.Always);
        UIRowEnabled.SetControlEnabled(row.Field, false);
        _rows.Relayout();

        row.Field.text = "115";
        _rows.SyncComputed();
        row.Field.text = "350";
        _rows.SyncComputed();

        Assert.AreEqual(NumberFormat.WithUnit("350", "мм"),
            _host.FindNode("Val_Computed_Высота").GetComponent<TMP_Text>().text,
            "значение пересчитывается элементом, а поле — его хранилище: текст идёт за ним");
    }

    [Test]
    public void ValueButton_StartsAtTheValueColumn_AndIsNoWiderThanIt()
    {
        var button = _rows.ValueButton("CtxProbe", "Двойной", () => { }, RowVisibility.Always);
        _rows.Relayout();

        var rt = (RectTransform)button.transform;
        Assert.AreEqual(_rows.Metrics.ValueX, rt.anchoredPosition.x, 0.01f,
            "кнопка действия стоит в колонке значений, а не во всю ширину панели");
        Assert.AreEqual(_rows.Metrics.ValueW, rt.sizeDelta.x, 0.01f);
    }

    [Test]
    public void PairField_PutsTheFirstFieldAtTheValueColumn_AndBothInsideIt()
    {
        var (first, second) = _rows.PairField("Слева / справа, мм", "gapLeft", "gapRight", "0", RowVisibility.Always);
        _rows.Relayout();

        var a = (RectTransform)first.transform;
        var b = (RectTransform)second.transform;
        Assert.AreEqual(_rows.Metrics.ValueX, a.anchoredPosition.x, 0.01f);
        Assert.Greater(b.anchoredPosition.x, a.anchoredPosition.x + a.sizeDelta.x - 0.01f, "поля не налезают друг на друга");
        Assert.LessOrEqual(b.anchoredPosition.x + b.sizeDelta.x, _rows.Metrics.Width + 0.01f, "и не выходят за колонку");
    }

    [Test]
    public void ReadOnlyField_DimsItsLabel_AndHasNoFrame()
    {
        var value = _rows.ReadOnlyField("Наружный диаметр", RowVisibility.Always);

        Assert.IsNull(value.GetComponentInParent<TMP_InputField>(), "вычисляемое — не поле ввода");
        Assert.AreEqual(UIStyle.TextDisabled, _host.FindNode("L_Наружный диаметр").GetComponent<TMP_Text>().color,
            "подпись вычисляемой строки погашена (§9)");
    }

    private static float RowTop(TMP_InputField field)
    {
        for (var t = field.transform; t != null; t = t.parent)
            if (t.name.StartsWith("Row_")) return ((RectTransform)t).anchoredPosition.y;
        return float.NaN;
    }
}

internal static class InspectorRowsTestExtensions
{
    public static Transform? IsShownOrNull(this Transform? node) => node != null && node.IsShown() ? node : null;
}
