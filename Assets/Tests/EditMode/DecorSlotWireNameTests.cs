using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>
/// Имена ПОЛЕЙ обоих слотов декора — внешний формат, а не имя в C#.
///
/// Круг «сохранение → загрузка» на каждом носителе уже держат
/// <c>ElementTypeRegistryExerciseTests.Restore_EveryTabletopType_KeepsBothDecorSlots</c>
/// и двадцать семь эталонов <c>*.verified.json</c>; повторять их здесь незачем.
/// Не прикрыт был другой конец той же границы — провод MCP. Круговой тест его
/// не ловит в принципе: он пишет и читает ОДНИМ И ТЕМ ЖЕ полем, поэтому
/// переименование обеих сторон разом оставляет его зелёным, а внешний агент,
/// продолжающий слать <c>tabletop_material</c>, молча промахивается мимо поля —
/// <c>JsonUtility</c> не находит его и подставляет умолчание вместо ошибки.
///
/// Поэтому проверка идёт не по списку классов, а по СБОРКЕ: любое публичное
/// поле экземпляра, в имени которого есть и «слот», и «material», обязано
/// называться одним из четырёх разрешённых способов. Список классов, выписанный
/// руками, разошёлся бы — контрактных <c>*Info</c> уже девять штук
/// (CONVENTIONS.md → «A field list written out more than twice gets a parity
/// test»).
/// </summary>
public class DecorSlotWireNameTests
{
    private static readonly string[] Allowed =
    {
        "tabletopMaterialId",
        "legsMaterialId",
        "tabletop_material",
        "legs_material",
    };

    private static List<FieldInfo> DecorSlotFields()
    {
        var assemblies = new[] { typeof(KitchenElement).Assembly, typeof(ElementData).Assembly }
            .Distinct();
        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsInterface)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            .Where(f => LooksLikeADecorSlot(f.Name))
            .ToList();
    }

    private static bool LooksLikeADecorSlot(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("material")
            && (lower.Contains("tabletop") || lower.Contains("legs"));
    }

    [Test]
    public void EveryPublicDecorSlotField_KeepsOneOfTheFourWireNames()
    {
        var wrong = DecorSlotFields()
            .Where(f => !Allowed.Contains(f.Name))
            .Select(f => f.DeclaringType!.Name + "." + f.Name)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.IsEmpty(wrong,
            "имя публичного поля слота декора уехало из внешнего формата: файл сохранения "
            + "и провод MCP находят поле ПО ИМЕНИ, и переименованное просто не находится — "
            + "ранее сохранённый проект открывается серым, а агент промахивается мимо поля, "
            + "и оба раза молча. Разрешены только " + string.Join(", ", Allowed) + "; "
            + "нарушители: " + string.Join(", ", wrong));
    }

    [Test]
    public void TheScan_SeesBothWireSurfaces_AndIsNotVacuouslyGreen()
    {
        var found = DecorSlotFields();

        Assert.GreaterOrEqual(found.Count, 20,
            "сканер обязан видеть оба провода разом — поле сохранения в ElementData и "
            + "пары в контрактных *Info; меньше двадцати означает, что он смотрит не в ту "
            + "сборку и проверка выше зелёная, не проверив ничего");
        CollectionAssert.Contains(found.Select(f => f.DeclaringType!.Name).Distinct().ToList(),
            nameof(ElementData),
            "формат файла обязан быть в выборке");
        CollectionAssert.Contains(found.Select(f => f.DeclaringType!.Name).Distinct().ToList(),
            nameof(EditOp),
            "провод записи через edit_elements обязан быть в выборке");
    }

    [Test]
    public void EditOp_KeepsBothDecorSlots_UnderTheNamesTheAgentSends()
    {
        Assert.IsNotNull(typeof(EditOp).GetField("tabletop_material"),
            "имя параметра edit_elements — публичный контракт: агент шлёт tabletop_material, "
            + "и переименование поля в C# меняет провод");
        Assert.IsNotNull(typeof(EditOp).GetField("legs_material"),
            "то же самое для второго слота");
    }

    [Test]
    public void ElementData_KeepsBothDecorSlots_UnderTheNamesTheSaveFileHolds()
    {
        Assert.IsNotNull(typeof(ElementData).GetField("tabletopMaterialId"),
            "имя поля в файле сохранения переживает любое переименование в C#: проекты, "
            + "записанные до него, обязаны открываться");
        Assert.IsNotNull(typeof(ElementData).GetField("legsMaterialId"),
            "то же самое для второго слота");
    }
}
