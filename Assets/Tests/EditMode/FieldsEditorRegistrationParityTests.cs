using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

/// <summary>Сторож docs/TODO.md пункт 5: список `*FieldsEditor` в конструкторе
/// ContextMenuUI и набор конкретных классов на диске — таблица возможностей и
/// её двойник в контракте (STRUCTURE.md), и они уже расходились: табуретка
/// получила класс редактора, но не строку в массиве `_editors`, и поле молча
/// не появилось в панели. Обе стороны сверяются отражением, а не руками, так
/// что расхождение красит тест, а не остаётся до следующей жалобы.</summary>
public class FieldsEditorRegistrationParityTests
{
    private GameObject? _go;
    private ContextMenuUI? _ctx;

    [SetUp]
    public void Setup()
    {
        _go = new GameObject("FieldsEditorParityProbe");
        _ctx = _go.AddComponent<ContextMenuUI>();
    }

    [TearDown]
    public void Teardown()
    {
        if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
    }

    private static List<Type> ConcreteEditorTypesOnDisk() =>
        typeof(ElementFieldsEditor).Assembly.GetTypes()
            .Where(t => typeof(ElementFieldsEditor).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    [Test]
    public void EveryFieldsEditorClassOnDisk_IsRegisteredInContextMenuUI()
    {
        var onDisk = ConcreteEditorTypesOnDisk();
        Assert.GreaterOrEqual(onDisk.Count, 20,
            "сканер типов нашёл подозрительно мало конкретных *FieldsEditor — вероятно, "
            + "смотрит не в ту сборку, и сверка ниже зеленеет на пустом");

        var registered = _ctx!.Editors.Select(e => e.GetType()).ToList();
        Assert.IsNotEmpty(registered,
            "ContextMenuUI.Editors пуст — панель не покажет ни одно специфическое поле "
            + "ни для одного типа элемента");

        var onlyOnDisk = onDisk.Except(registered).Select(t => t.Name).ToList();
        var onlyRegistered = registered.Except(onDisk).Select(t => t.Name).ToList();

        CollectionAssert.AreEquivalent(onDisk, registered,
            "класс *FieldsEditor на диске и массив _editors в конструкторе ContextMenuUI "
            + "разошлись. Каждый КОНКРЕТНЫЙ (не abstract) наследник ElementFieldsEditor "
            + "обязан быть в _editors, и каждая запись _editors обязана существовать на диске. "
            + "Чинить: добавить/убрать запись в _editors (ContextMenuUI.cs, конструктор). "
            + "только на диске (не зарегистрирован): [" + string.Join(", ", onlyOnDisk) + "]; "
            + "только зарегистрирован (класса больше нет): [" + string.Join(", ", onlyRegistered) + "]");
    }
}
