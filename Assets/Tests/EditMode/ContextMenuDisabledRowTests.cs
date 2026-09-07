using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Стражи правила «нередактируемая строка гаснет целиком»
/// (docs/UI-GUIDELINES.md → «Нередактируемая строка»).
///
/// Правило записано МЕХАНИЗМОМ, а не перечнем полей
/// (CONVENTIONS.md → «State a rule by its MECHANISM, not as a list of the cases
/// you happened to fix»): перебираются ВСЕ типы элементов из EveryElementType,
/// и любая строка, чей контрол оказался неинтерактивным, обязана иметь
/// погашенную подпись. Поэтому следующее нередактируемое поле попадёт под
/// проверку само — его не нужно вписывать ни в какой список.
///
/// Второй тест закрывает обход правила: неинтерактивный контрол, собранный
/// МИМО ContextMenuRowFactory, не имеет пары «подпись—контрол», и первый тест
/// его просто не увидел бы (CONVENTIONS.md → «Not only the guard — every READER
/// of a state must ask through one function»).
/// </summary>
public class ContextMenuDisabledRowTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;

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
        EveryElementType.ClearScene();
    }

    [Test]
    public void EveryElementType_DisabledRow_DimsItsLabelTogetherWithTheControl()
    {
        var offenders = new List<string>();

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);

            foreach (var (label, control) in _menu!.RowFactory.LabelledRows)
            {
                if (label == null || control == null) continue;
                if (!control.gameObject.activeInHierarchy) continue;
                if (UIRowEnabled.IsEnabled(control)) continue;
                if (label.color == UIStyle.TextDisabled) continue;
                offenders.Add($"{type.Name}: «{label.text}» — контрол погашен, подпись нет "
                    + $"(цвет {label.color})");
            }

            _menu!.Close();
            EveryElementType.ClearScene();
        }

        Assert.IsEmpty(offenders,
            "Правило: строка панели свойств гаснет ЦЕЛИКОМ — контрол и подпись. Пока подпись "
            + "остаётся яркой, человек не видит, что поле не правится, и раз за разом пробует в "
            + "него печатать. Красить руками ничего не надо: пара «подпись—контрол» уже "
            + "зарегистрирована в ContextMenuRowFactory, а ContextMenuUI зовёт "
            + "SyncEnabledState() при открытии панели и после каждого применения. Если строка "
            + "попала сюда — её контрол выключили ПОСЛЕ этого вызова: добавьте свой вызов "
            + "SyncEnabledState() или гасите строку через UIRowEnabled.SetRowEnabled(label, "
            + "control, false). Нарушители: " + string.Join(" | ", offenders));
    }

    [Test]
    public void EveryElementType_DisabledControl_IsAlwaysAKnownRow_SoItsLabelCanBeFound()
    {
        var orphans = new List<string>();

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);

            var known = new HashSet<Selectable>();
            foreach (var (_, control) in _menu!.RowFactory.LabelledRows)
                if (control != null) known.Add(control);

            foreach (var control in Panel().GetComponentsInChildren<Selectable>(false))
            {
                if (control is not TMP_InputField && control is not TMP_Dropdown) continue;
                if (UIRowEnabled.IsEnabled(control)) continue;
                if (known.Contains(control)) continue;
                orphans.Add($"{type.Name}: {control.name}");
            }

            _menu!.Close();
            EveryElementType.ClearScene();
        }

        Assert.IsEmpty(orphans,
            "Правило: у каждого поля и каждого выпадающего списка панели есть подпись, и она "
            + "гаснет вместе с ним. Контрол, собранный мимо ContextMenuRowFactory, такой пары не "
            + "имеет — его подпись не погаснет никогда, и первый страж этого даже не заметит. "
            + "Собирайте строку через Rows.NumberField/Dropdown/NamedDropdown, а если строка "
            + "действительно особенная — зарегистрируйте пару явно. Безнадзорные контролы: "
            + string.Join(" | ", orphans));
    }

    [Test]
    public void Oven_LockedSizeRows_DimTheirLabels()
    {
        var oven = EveryElementType.Spawn(typeof(OvenElement), "Духовка");
        _menu!.Open(oven);

        foreach (var row in new[] { "Ширина", "Высота", "Глубина" })
        {
            Assume.That(Field(row).interactable, Is.False,
                "габарит духовки фиксирован моделью — поле обязано быть выключено");
            Assert.AreEqual(UIStyle.TextDisabled, Label(row).color,
                $"подпись «{row}» у духовки обязана гаснуть вместе с полем: именно на ней "
                + "человек читает, что размер не правится");
        }
    }

    [Test]
    public void Board_EditableSizeRows_KeepTheirLabelsBright()
    {
        var board = EveryElementType.Spawn(typeof(KitchenElement), "Полка");
        _menu!.Open(board);

        foreach (var row in new[] { "Ширина", "Высота", "Глубина" })
        {
            Assume.That(Field(row).interactable, Is.True,
                "габариты обычной детали правятся — поле включено");
            Assert.AreEqual(UIStyle.Text, Label(row).color,
                $"подпись «{row}» у обычной детали остаётся яркой: иначе «погашено» перестаёт "
                + "что-либо значить (парный контроль к тесту про духовку)");
        }
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_InputField Field(string label) =>
        Panel().Find($"F_{label}")!.GetComponent<TMP_InputField>();

    private TMP_Text Label(string label) =>
        Panel().Find($"L_{label}")!.GetComponent<TMP_Text>();
}
