using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;

/// <summary>
/// Реестры, ветвящиеся ПО ТИПУ элемента, проверенные ЗАПУСКОМ, а не грепом.
///
/// ElementTypeCompletenessTests читает исходники и утверждает, что тип НАЗВАН в
/// каждом реестре. Этого мало, и обычный стол это доказал: он был назван в реестре
/// дублирования, сторож месяцами зеленел, а сама ветка отдавала не то — копия
/// теряла LegsMaterialId и LegInsetMM. «Тип зарегистрирован» и «регистрация делает
/// то, что обещает» — разные утверждения, и греп про второе не знает ничего.
///
/// Здесь проверяется второе: каждый тип реально создаётся фабрикой, реально едет
/// через файл проекта и реально сверяется по значениям. Набор экземпляров лежит в
/// EveryElementType и сверяется с объявлениями классов, поэтому новый тип сначала
/// уронит сам список (CONVENTIONS.md → «A field list written out more than twice
/// gets a parity test»).
/// </summary>
public class ElementTypeRegistryExerciseTests
{
    private const string TopDecorId = "test-decor-top";
    private const string LegsDecorId = "test-decor-legs";

    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        MaterialCatalog.Register(new MaterialDef(TopDecorId, "Тест столешницы", "ЛДСП", Color.red));
        MaterialCatalog.Register(new MaterialDef(LegsDecorId, "Тест ножек", "ЛДСП", Color.blue));
        EveryElementType.ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        EveryElementType.ClearScene();
        MaterialCatalog.Reset();
        MaterialManager.ClearCache();
        CommandStack.Clear();
        GroupManager.Clear();
        _globals?.Restore();
    }

    /// <summary>Через настоящий файл проекта, а не через ElementCapture напрямую:
    /// поле, которое захвачено, но не доехало до JSON, теряется ровно здесь.</summary>
    private static KitchenElement RestoreThroughFile(KitchenElement source)
    {
        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(new[] { source }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, "проект обязан читаться обратно из JSON");

        EveryElementType.ClearScene();

        var created = SaveLoadManager.RestoreScene(data!);
        Assert.AreEqual(1, created.Count, "восстановиться должен ровно один элемент");
        var el = created[0].GetComponent<KitchenElement>();
        Assert.IsNotNull(el, "восстановленный объект обязан нести KitchenElement");
        return el;
    }

    /// <summary>Значение, заведомо отличное от текущего. Сеттер вправе склампить
    /// его обратно — сверяемся со СНИМКОМ элемента после записи, а не с тем, что
    /// просили записать.</summary>
    private static object? DifferentValue(PropertyInfo prop, object? current)
    {
        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (t.IsEnum)
            return Enum.GetValues(t).Cast<object>().FirstOrDefault(v => !Equals(v, current)) ?? current;
        if (t == typeof(bool)) return !(bool)(current ?? false);
        if (t == typeof(int)) return (int)(current ?? 0) + 7;
        if (t == typeof(float)) return (float)(current ?? 0f) + 1.5f;
        if (t == typeof(string)) return (current as string ?? "") + "X";
        if (t == typeof(Vector3Int))
        {
            var v = (Vector3Int)(current ?? Vector3Int.zero);
            return new Vector3Int(v.x + 11, v.y + 13, v.z + 3);
        }
        return null;
    }

    private static readonly string[] GapProperties =
    {
        nameof(KitchenElement.GapLeft), nameof(KitchenElement.GapRight),
        nameof(KitchenElement.GapTop), nameof(KitchenElement.GapBottom),
        nameof(KitchenElement.GapFront), nameof(KitchenElement.GapBack),
    };

    /// <summary>Зазоры у элемента, который их не поддерживает, ElementCapture пишет
    /// нулями НАМЕРЕННО, а восстановление их не трогает. Условие спрашивает сам
    /// элемент, а не список типов, — поэтому оно не устареет.</summary>
    private static bool Applicable(KitchenElement el, PropertyInfo prop) =>
        el.SupportsGaps || Array.IndexOf(GapProperties, prop.Name) < 0;

    /// <summary>Главная проверка: КАЖДОЕ свойство, которое пользователь может
    /// изменить (а это ровно множество [Undoable]), обязано пережить сохранение и
    /// загрузку у КАЖДОГО типа. Пропуск здесь — не стилевая придирка: проект
    /// открывается тихо и не тем, чем был закрыт. Так был найден габарит
    /// светильника: ElementCapture его писал, а ветка isLightSource возвращала
    /// заводские 150 мм.
    ///
    /// Список свойств берётся рефлексией по атрибуту, список типов — рефлексией по
    /// сборке: новое свойство попадает под проверку в тот же момент, когда получает
    /// [Undoable], а новый тип — когда появляется класс.</summary>
    [Test]
    public void Restore_EveryUndoableProperty_ComesBackFromTheFile()
    {
        var lost = new List<string>();
        var unsupported = new List<string>();
        int compared = 0;
        int inert = 0;

        foreach (var type in EveryElementType.Declared())
        {
            var props = UndoableProperties.For(type);
            if (props.Length == 0) continue;

            EveryElementType.ClearScene();
            var source = EveryElementType.Spawn(type, "El" + type.Name);

            var factoryDefault = props.Select(p => p.GetValue(source)).ToArray();
            foreach (var prop in props)
            {
                var candidate = DifferentValue(prop, prop.GetValue(source));
                if (candidate == null) { unsupported.Add(type.Name + "." + prop.Name); continue; }
                try { prop.SetValue(source, candidate); }
                catch (Exception e) { lost.Add(type.Name + "." + prop.Name + " (запись упала: " + e.Message + ")"); }
            }

            var expected = props.Select(p => p.GetValue(source)).ToArray();
            var applicable = props.Select(p => Applicable(source, p)).ToArray();

            var restored = RestoreThroughFile(source);
            Assert.AreEqual(type, restored.GetType(),
                "загрузка вернула другой тип — это уже потеря данных, а не одного поля");

            for (int i = 0; i < props.Length; i++)
            {
                if (!applicable[i]) continue;
                if (Equals(expected[i], factoryDefault[i])) { inert++; continue; }
                if (Equals(expected[i], props[i].GetValue(restored))) compared++;
                else lost.Add($"{type.Name}.{props[i].Name}: сохранили {Show(expected[i])}, "
                              + $"вернулось {Show(props[i].GetValue(restored))}");
            }
        }

        Assert.IsEmpty(unsupported,
            "тест не умеет придумать другое значение для этих свойств — допишите DifferentValue, "
            + "иначе они молча выпадают из проверки: " + string.Join(", ", unsupported));
        Assert.IsEmpty(lost,
            "свойство не пережило сохранение и загрузку. Ветка ElementRestorers для этого типа есть "
            + "(её сторожит греп), но она отдаёт не всё — проект пользователя открывается не тем, "
            + "чем был закрыт:\n    " + string.Join("\n    ", lost));
        Assert.Greater(compared, 0,
            $"ни одно свойство не сравнено (инертных {inert}) — тест не может провалиться, значит бесполезен");
    }

    /// <summary>Тот же дефект, что был пойман в дублировании, но на пути
    /// сохранения: у IHasTwoDecorSlots ДВА слота декора, а MaterialId у стола — лишь
    /// псевдоним столешницы, поэтому ветка, восстанавливающая «материал», молча
    /// теряет ножки. Слоты не помечены [Undoable] и в проверку выше не попадают.</summary>
    [Test]
    public void Restore_EveryTabletopType_KeepsBothDecorSlots()
    {
        var declared = EveryElementType.Declared()
            .Where(t => typeof(IHasTwoDecorSlots).IsAssignableFrom(t)).ToList();
        Assert.IsNotEmpty(declared,
            "рефлексия не нашла ни одного IHasTwoDecorSlots — сканер смотрит не в ту сборку, и проверка "
            + "ниже зелёная, но не проверяет ничего");

        var lost = new List<string>();
        foreach (var type in declared)
        {
            EveryElementType.ClearScene();
            var source = EveryElementType.Spawn(type, "Top" + type.Name);
            var slots = (IHasTwoDecorSlots)source;
            MaterialManager.ApplyTabletop(slots, MaterialCatalog.Get(TopDecorId));
            MaterialManager.ApplyLegs(slots, MaterialCatalog.Get(LegsDecorId));
            Assert.AreEqual(TopDecorId, slots.TabletopMaterialId,
                "предусловие: декор столешницы вообще назначился");
            Assert.AreEqual(LegsDecorId, slots.LegsMaterialId,
                "предусловие: декор ножек вообще назначился");

            var copy = (IHasTwoDecorSlots)RestoreThroughFile(source);
            if (copy.TabletopMaterialId != TopDecorId)
                lost.Add(type.Name + ".TabletopMaterialId = " + copy.TabletopMaterialId);
            if (copy.LegsMaterialId != LegsDecorId)
                lost.Add(type.Name + ".LegsMaterialId = " + copy.LegsMaterialId);
        }

        Assert.IsEmpty(lost,
            "слот декора не пережил сохранение: ветка ElementRestorers не позвала "
            + "RestoreTabletopMaterials, и мебель открывается перекрашенной — "
            + string.Join("; ", lost));
    }

    /// <summary>ElementSelector.TypeOf — лестница с замыкающим «board». Греп видит
    /// только то, что имя класса в файле УПОМЯНУТО; он не видит ни порядка веток, ни
    /// того, какую строку ветка возвращает. Подтип, оказавшийся ПОСЛЕ своей базы,
    /// отвечает именем базы и попадает в чужую массовую выборку.</summary>
    [Test]
    public void TypeOf_EveryElementType_AnswersItsOwnName_NotTheBoardFallback()
    {
        var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            var el = EveryElementType.Spawn(type, "Sel" + type.Name);
            var answer = ElementSelector.TypeOf(el);
            if (!byName.ContainsKey(answer)) byName[answer] = new List<string>();
            byName[answer].Add(type.Name);
        }

        var boarded = byName.ContainsKey("board")
            ? byName["board"].Where(n => n != nameof(KitchenElement)).ToList()
            : new List<string>();
        Assert.IsEmpty(boarded,
            "тип провалился в замыкающий «board»: он не откажет, а притворится доской и попадёт "
            + "в выборку all_boards вместе с настоящими деталями — " + string.Join(", ", boarded));

        var shared = byName.Where(p => p.Value.Count > 1)
            .Select(p => p.Key + " ← " + string.Join(" + ", p.Value)).ToList();
        Assert.IsEmpty(shared,
            "два типа отвечают одним именем: селектор type: не может их различить, а ветка подтипа "
            + "стоит ПОСЛЕ базы и потому не выполняется — " + string.Join("; ", shared));
    }

    /// <summary>Грепу по пустому списку нечего найти — он зеленеет, ничего не
    /// проверив. Здесь список экземпляров сверяется с объявлениями классов: новый
    /// тип элемента обязан сначала уронить ЭТОТ тест, а не тихо пройти мимо
    /// проверок выше и мимо McpConvertElementsReproTests.</summary>
    [Test]
    public void EveryElementTypeInTheAssembly_HasAFactoryCallHere()
    {
        var declared = EveryElementType.Declared();
        Assert.GreaterOrEqual(declared.Count, 16,
            "рефлексия нашла подозрительно мало типов — сканер смотрит не в ту сборку");
        CollectionAssert.Contains(declared, typeof(KitchenElement),
            "база — это «доска», и она тоже проходит через все реестры");

        CollectionAssert.AreEquivalent(declared, EveryElementType.Makers.Select(m => m.type).ToList(),
            "список EveryElementType.Makers разошёлся с типами элементов в сборке ядра: пока новый "
            + "тип не заведён там, он не проверяется ни на восстановление, ни на TypeOf, ни на "
            + "отказ конвертации");
    }

    private static string Show(object? v) => v?.ToString() ?? "null";
}
