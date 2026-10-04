using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>
/// Колонки панели свойств (docs/ui-redesign/inspector.md, приёмка): ВСЕ подписи с одного x,
/// ВСЕ значения с одного x. Правило записано механизмом, а не перечнем строк:
/// перебираются ВСЕ типы из <see cref="EveryElementType"/>, и каждая видимая пара «подпись —
/// контрол» меряется в системе координат панели. Новая строка попадает под сторожа сама.
///
/// Раньше подписи стояли с трёх x (10, 16, 32), значения с двух (списки с 146, числа с 222):
/// две фабрики строк со своими магическими числами. Теперь колонки берутся из
/// <c>RowMetrics</c>, а сторож проверяет результат, а не намерение.
/// </summary>
public class InspectorColumnsGuardTests
{
    private const float Epsilon = 0.6f;

    private Canvas? _canvas;
    private ContextMenuUI? _menu;

    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    [SetUp]
    public void Setup()
    {
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) { _menu!.Close(); _menu!.TestHooks.ForgetSectionStates(); }
        EveryElementType.ClearScene();
    }

    private RectTransform Panel() => (RectTransform)_canvas!.transform.FindNode("ContextMenu");

    private static float LeftEdge(RectTransform panel, RectTransform node)
    {
        var corners = new Vector3[4];
        node.GetWorldCorners(corners);
        var local = panel.InverseTransformPoint(corners[0]);
        return local.x - panel.rect.xMin;
    }

    private static string Where(string type, string node) => type + ": «" + node + "»";

    private void ExpandEverySection()
    {
        foreach (var section in Panel().GetComponentsInChildren<CollapsibleSection>(true))
            section.SetExpanded(true, notify: true);
    }

    [Test]
    public void EveryElementType_LabelsStartAtTheLabelColumn_AndValuesAtTheValueColumn()
    {
        var offenders = new List<string>();
        float valueColumn = UIStyle.ToolPanelPad + UIStyle.InspectorLabelW + UIStyle.InspectorColumnGap;
        var labelColumns = new[] { UIStyle.ToolPanelPad, UIStyle.ToolPanelPad + UIStyle.SubRowIndent };
        int measured = 0;

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);
            ExpandEverySection();
            var panel = Panel();

            var firstControlOf = new Dictionary<TMP_Text, float>();
            foreach (var (label, control) in _menu!.Rows.LabelledRows)
            {
                if (label == null || control == null) continue;
                if (!label.gameObject.activeInHierarchy || !control.gameObject.activeInHierarchy) continue;
                if (control is Button || control.transform.parent.name.StartsWith("Vec_")
                    || control.transform.parent.name.StartsWith(InspectorRows.PairRowPrefix)) continue;

                float labelX = LeftEdge(panel, label.rectTransform);
                if (!labelColumns.Any(c => Mathf.Abs(c - labelX) < Epsilon))
                    offenders.Add($"{Where(type.Name, label.text)}: подпись с x={labelX:0.#}, допустимо {string.Join(" или ", labelColumns)}");

                float controlX = LeftEdge(panel, (RectTransform)control.transform);
                if (!firstControlOf.TryGetValue(label, out var best) || controlX < best) firstControlOf[label] = controlX;
                measured++;
            }

            foreach (var pair in firstControlOf)
                if (Mathf.Abs(pair.Value - valueColumn) > Epsilon)
                    offenders.Add($"{Where(type.Name, pair.Key.text)}: значение с x={pair.Value:0.#}, колонка значений — {valueColumn}");

            foreach (var value in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!value.name.StartsWith("Val_")) continue;
                float x = LeftEdge(panel, value.rectTransform);
                if (Mathf.Abs(x - valueColumn) > Epsilon)
                    offenders.Add($"{Where(type.Name, value.name)}: вычисляемое значение с x={x:0.#}, колонка значений — {valueColumn}");
            }

            _menu!.Close();
            EveryElementType.ClearScene();
        }

        Assert.Greater(measured, 100, "сторож обязан что-то мерить: пустой обход — зелёный сторож, который ничего не видит");
        Assert.IsEmpty(offenders,
            "Правило D7: левый край каждой подписи ∈ {12, 28}, левый край каждого значения = 160. "
            + "Строка, собранная мимо InspectorRows/FormRows со своими числами, ломает колонку:\n"
            + string.Join("\n", offenders.Distinct()));
    }

    [Test]
    public void TheGuard_SeesAMisplacedLabel()
    {
        var element = EveryElementType.Spawn(typeof(KitchenElement), "Проба");
        _menu!.Open(element);
        var panel = Panel();
        var label = _menu!.Rows.LabelledRows.First(r => r.label != null && r.label.gameObject.activeInHierarchy).label!;
        var rt = label.rectTransform;
        var home = rt.anchoredPosition;

        rt.anchoredPosition = home + new Vector2(10f, 0f);
        float moved = LeftEdge(panel, rt);
        rt.anchoredPosition = home;

        Assert.AreEqual(UIStyle.ToolPanelPad + 10f, moved, Epsilon,
            "измерение края обязано видеть сдвиг подписи на 10 px: иначе сторож колонок слеп");
    }

    [Test]
    public void EveryElementType_NoTextBelowThePanelMinimum_AndNoButtonWiderThanTheValueColumn()
    {
        var offenders = new List<string>();
        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);
            ExpandEverySection();
            var panel = Panel();

            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(false))
                if (text.fontSize < UIStyle.FontMin - 0.01f)
                    offenders.Add($"{Where(type.Name, text.name)}: кегль {text.fontSize} < {UIStyle.FontMin}");

            foreach (var button in panel.GetComponentsInChildren<Button>(false))
            {
                if (button.transform.IsChildOf(panel.FindNode("ContextMenuFooter"))) continue;
                if (button.transform.IsChildOf(panel.FindNode("CtxEdgeDiagram"))) continue;
                var rt = (RectTransform)button.transform;
                if (rt.rect.width > UIStyle.InspectorValueW + Epsilon && !InsideListRow(rt))
                    offenders.Add($"{Where(type.Name, button.name)}: кнопка шире колонки значений ({rt.rect.width:0.#} > {UIStyle.InspectorValueW})");
            }

            _menu!.Close();
            EveryElementType.ClearScene();
        }

        Assert.IsEmpty(offenders,
            "D3/D7: меньше 13 текст не бывает, а кнопка не шире колонки значений (кроме строк секции-списка):\n"
            + string.Join("\n", offenders.Distinct()));
    }

    private static bool InsideListRow(RectTransform node)
    {
        for (var t = node.parent; t != null; t = t.parent)
            if (t.name.StartsWith("Row_Ctx") && t.name.Substring(4).Contains("Row")) return true;
        return false;
    }
}
