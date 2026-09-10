using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Сторож против опечатки в разделе ведомости: до этого <see cref="SpecItem"/>
/// принимал раздел свободной строкой, и опечатка в ней тихо рождала новый раздел
/// вместо того, чтобы попасть в существующий или упасть тестом.
///
/// Проверка ДВУСТОРОННЯЯ, и вторая сторона — новая. Прямая («раздел выданной строки входит в
/// <see cref="SpecSections.All"/>») ловит опечатку в элементе, но слепа к опечатке в самой
/// <see cref="SpecSections"/>: переименуй там константу — и обе стороны переименуются вместе,
/// молча. Обратная («каждая константа из <see cref="SpecSections.All"/> кем-то выдаётся или
/// объявлена зарезервированной С ПРИЧИНОЙ») это видит, и заодно не даёт трём мёртвым именам
/// читаться следующим агентом как «поддерживаемые разделы».
///
/// Типы не спавнятся здесь заново: и раздел, и число строк уже посчитаны один раз в
/// <see cref="SpecificationSweep"/> — три прохода по всем ~38 типам стоили набору EditMode
/// 33 секунды сверх бюджета.</summary>
public class SpecSectionsTests
{
    [TearDown]
    public void TearDown() => EveryElementType.ClearScene();

    /// <summary>Раздел объявлен, но сегодня его не выдаёт ни один тип — и это ОСОЗНАННО, с
    /// причиной, а не забытое имя. Стены, фундамент и конструкции заводятся под часть 3
    /// карты развития (`docs/todo_evolution.md` → «Часть 3 — От кухни к симулятору постройки
    /// дома»): `FoundationElement` с лентой считает выемку, песок, щебень, бетон в м³,
    /// опалубку в м² и арматуру в кг, стена и перекрытие отчитаются своими строками. Константы
    /// стоят раньше своих элементов НАМЕРЕННО — чтобы раздел не был придуман заново строкой в
    /// новом классе.</summary>
    private static readonly Dictionary<string, string> ReservedForFutureSections =
        new Dictionary<string, string>
    {
        { SpecSections.Walls,
            "часть 3: кладка/каркас стены и перекрытия отчитаются погонными метрами и м² "
            + "своими SpecItem, когда появится счётчик стены" },
        { SpecSections.Foundation,
            "часть 3, этап 4 «Фундамент — лента»: FoundationQuantities — выемка, песок, "
            + "щебень, бетон (м³), опалубка (м²), арматура (кг)" },
        { SpecSections.Structures,
            "часть 3: группа каталога «Конструкции» — лестницы, перегородки, кровля; раздел "
            + "заведён вместе с группой, элементов под ним пока нет" },
    };

    /// <summary><see cref="SpecSections.All"/> ведётся руками параллельно константам, поэтому
    /// новая константа может не попасть в массив — и опечатка в её значении перестанет
    /// краснеть. Массив сверяется с рефлексией по самому типу.</summary>
    [Test]
    public void SpecSectionsAll_ListsEveryPublicConstOfTheType()
    {
        var consts = typeof(SpecSections)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        CollectionAssert.AreEquivalent(consts, SpecSections.All,
            "массив SpecSections.All разошёлся с константами самого типа: раздел, объявленный "
            + "константой, но не попавший в All, не проверяется ничем — опечатка в нём тихо "
            + "родит новый раздел");
    }

    [Test]
    public void EverySpecLineSection_ComesFromSpecSectionsAll()
    {
        var violations = new List<string>();

        foreach (var row in SpecificationSweep.Rows)
        foreach (var section in row.sections)
            if (!SpecSections.All.Contains(section))
                violations.Add($"{row.type.Name}: \"{section}\"");

        Assert.IsEmpty(violations,
            "эти строки ведомости несут раздел вне SpecSections.All — опечатка в разделе "
            + "тихо родит новый раздел вместо того, чтобы упасть здесь: "
            + string.Join(", ", violations));
    }

    /// <summary>Обратная сторона: раздел, который не выдаёт НИКТО, — либо опечатка внутри
    /// самой SpecSections, либо мёртвое имя, читающееся как поддерживаемый раздел.</summary>
    [Test]
    public void EverySectionInAll_IsProducedByADeclaredType_OrReservedWithAReason()
    {
        var produced = ProducedSections();
        var orphans = SpecSections.All
            .Where(s => !produced.Contains(s) && !ReservedForFutureSections.ContainsKey(s))
            .ToList();

        Assert.IsEmpty(orphans,
            "эти разделы объявлены в SpecSections.All, но их не выдаёт ни один объявленный тип "
            + "элемента. Либо это опечатка в самой SpecSections (обе стороны переименовались "
            + "вместе и прямая проверка её не видит), либо раздел заведён под будущее — тогда "
            + "он обязан стоять в ReservedForFutureSections С ПРИЧИНОЙ, а не молча: "
            + string.Join(", ", orphans));
    }

    /// <summary>Противоположный вход: зарезервированный раздел обязан быть настоящей
    /// константой раздела и НЕ выдаваться никем. Как только под него появится элемент,
    /// запись становится ложью — и тест это скажет.</summary>
    [Test]
    public void EveryReservedSection_IsStillDeclaredAndStillProducedByNobody()
    {
        var produced = ProducedSections();
        foreach (var pair in ReservedForFutureSections)
        {
            CollectionAssert.Contains(SpecSections.All, pair.Key,
                $"\"{pair.Key}\" числится зарезервированным разделом, но в SpecSections.All "
                + "такого нет — константу переименовали или убрали, а запись осталась мёртвой");
            Assert.IsNotEmpty(pair.Value, $"у резерва \"{pair.Key}\" пустая причина");
            CollectionAssert.DoesNotContain(produced, pair.Key,
                $"\"{pair.Key}\" числится зарезервированным «под будущее», но его уже выдаёт "
                + "объявленный тип элемента — резерв состоялся, запись пора убрать");
        }
    }

    private static HashSet<string> ProducedSections()
    {
        var produced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in SpecificationSweep.Rows)
        foreach (var section in row.sections)
            produced.Add(section);
        return produced;
    }
}
