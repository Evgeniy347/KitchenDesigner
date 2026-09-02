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
/// через файл проекта и реально сверяется по значениям. Набор экземпляров не
/// записан руками — он сверяется рефлексией по сборке ядра, поэтому новый тип
/// сначала уронит сам список (CONVENTIONS.md → «A field list written out more than
/// twice gets a parity test»).
/// </summary>
public class ElementTypeRegistryExerciseTests
{
    private const string TopDecorId = "test-decor-top";
    private const string LegsDecorId = "test-decor-legs";

    /// <summary>Долг, а не послабление правила: храповик по образцу
    /// ElementTypeCompletenessTests.KnownGaps. Запись обязана ВОСПРОИЗВОДИТЬСЯ —
    /// закрытая дыра, пережившая свой долг, молча освободила бы следующее поле,
    /// которое в неё попадёт.</summary>
    private static readonly (string type, string property, string why)[] KnownGaps =
    {
        (nameof(LightSourceElement), nameof(KitchenElement.DimensionsMM),
         "размер светильника пользователь правит в панели (FixedSize.IsFixed про лампу говорит "
         + "«нет», LightFieldsEditor поля не запирает), ElementCapture его пишет, а ветка "
         + "isLightSource зовёт CreateLightSource, который жёстко ставит LampSpec.DEFAULT_SIZE_MM, "
         + "и ApplyShared габарит не трогает. Лечится строкой в RestoreLamp, но это правка "
         + "поведения и отдельное решение"),
    };

    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        MaterialCatalog.Register(new MaterialDef(TopDecorId, "Тест столешницы", "ЛДСП", Color.red));
        MaterialCatalog.Register(new MaterialDef(LegsDecorId, "Тест ножек", "ЛДСП", Color.blue));
        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        MaterialCatalog.Reset();
        MaterialManager.ClearCache();
        CommandStack.Clear();
        GroupManager.Clear();
        _globals?.Restore();
    }

    private static void ClearScene()
    {
        foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenElement>())
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    /// <summary>Каждый тип — своим фабричным вызовом. Аргументы взяты НЕ заводские
    /// там, где фабрика их принимает: значение, совпавшее с умолчанием, не отличить
    /// от полностью потерянного.</summary>
    private static readonly (Type type, Func<string, GameObject> make)[] Makers =
    {
        (typeof(KitchenElement), n => ElementFactory.CreatePart(new Vector3Int(823, 417, 19), n, Vector3.zero)),
        (typeof(FacadeElement), n => ElementFactory.CreateFacade(new Vector3Int(447, 713, 19), n, Vector3.zero)),
        (typeof(AssembledFacadeElement), n => ElementFactory.CreateAssembledFacade(new Vector3Int(451, 719, 19), n, Vector3.zero, AssembledFill.Glass)),
        (typeof(PanelElement), n => ElementFactory.Instance.CreatePanel(new Vector3Int(613, 409, 4), n, Vector3.zero)),
        (typeof(RadialShelfElement), n => ElementFactory.CreateRadialShelf(607, 411, 19, 137, n, Vector3.zero)),
        (typeof(DrawerElement), n => ElementFactory.CreateDrawer(DrawerType.C, 450, DrawerColor.White, 407, n, Vector3.zero)),
        (typeof(TableElement), n => ElementFactory.CreateTable(new Vector3Int(1207, 753, 703), n, Vector3.zero)),
        (typeof(RadiusTableElement), n => ElementFactory.CreateRadiusTable(new Vector3Int(1213, 757, 709), n, Vector3.zero)),
        (typeof(StoolElement), n => ElementFactory.CreateStool(new Vector3Int(363, 453, 367), 23, n, Vector3.zero)),
        (typeof(ChairElement), n => ElementFactory.CreateChair(new Vector3Int(453, 903, 457), 27, 463, n, Vector3.zero)),
        (typeof(SofaElement), n => ElementFactory.CreateSofa(new Vector3Int(1807, 803, 903), 31, 427, n, Vector3.zero)),
        (typeof(PouffeElement), n => ElementFactory.CreatePouffe(new Vector3Int(407, 423, 403), 37, 83, n, Vector3.zero)),
        (typeof(BedElement), n => ElementFactory.CreateBed(new Vector3Int(1607, 503, 2003), false, false, n, Vector3.zero)),
        (typeof(PillarElement), n => ElementFactory.CreatePillar(713, n, Vector3.zero, 87)),
        (typeof(ScrewLegElement), n => ElementFactory.CreateScrewLeg(n, Vector3.zero)),
        (typeof(FloorElement), n => ElementFactory.CreateFloor(new Vector3Int(3007, 23, 3011), n, Vector3.zero)),
        (typeof(LightSourceElement), n => ElementFactory.CreateLightSource(n, Vector3.zero)),
        (typeof(SinkElement), n => ElementFactory.CreateSink(n, Vector3.zero)),
        (typeof(CooktopElement), n => ElementFactory.CreateCooktop(n, Vector3.zero)),
        (typeof(OvenElement), n => ElementFactory.CreateOven(n, Vector3.zero)),
        (typeof(DishwasherElement), n => ElementFactory.CreateDishwasher(n, Vector3.zero)),
        (typeof(WindowElement), n => ElementFactory.CreateWindow(new Vector3Int(907, 1213, 103), n, Vector3.zero, GlassTint.Tinted, 63)),
        (typeof(DoorElement), n => ElementFactory.CreateDoor(new Vector3Int(903, 2007, 107), n, Vector3.zero, DoorSashType.Blind)),
    };

    private static List<Type> DeclaredElementTypes() =>
        typeof(KitchenElement).Assembly.GetTypes()
            .Where(t => typeof(KitchenElement).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static KitchenElement Spawn(Type type, string name)
    {
        var make = Makers.First(m => m.type == type).make;
        var go = make(name);
        Assert.IsNotNull(go, "фабрика вернула null для " + type.Name);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el, "фабрика обязана вернуть объект с KitchenElement: " + type.Name);
        Assert.AreEqual(type, el!.GetType(),
            "фабричный вызов для " + type.Name + " собрал другой тип — список Makers разошёлся с фабрикой");
        return el!;
    }

    /// <summary>Через настоящий файл проекта, а не через ElementCapture напрямую:
    /// поле, которое захвачено, но не доехало до JSON, теряется ровно здесь.</summary>
    private static KitchenElement RestoreThroughFile(KitchenElement source)
    {
        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(new[] { source }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, "проект обязан читаться обратно из JSON");

        ClearScene();

        var created = SaveLoadManager.RestoreScene(data!);
        Assert.AreEqual(1, created.Count, "восстановиться должен ровно один элемент");
        var el = created[0].GetComponent<KitchenElement>();
        Assert.IsNotNull(el, "восстановленный объект обязан нести KitchenElement");
        return el!;
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

    /// <summary>Одна строка отчёта о потере: «Тип.Свойство».</summary>
    private static string Key(Type type, PropertyInfo prop) => type.Name + "." + prop.Name;

    /// <summary>Прогон, общий для боевого теста и для проверки самого храповика:
    /// вернуть множество свойств, НЕ переживших сохранение, и число реально
    /// сравненных.</summary>
    private static (HashSet<string> lost, List<string> unsupported, int compared, int inert) SweepRestore()
    {
        var lost = new HashSet<string>(StringComparer.Ordinal);
        var unsupported = new List<string>();
        int compared = 0;
        int inert = 0;

        foreach (var type in DeclaredElementTypes())
        {
            var props = UndoableProperties.For(type);
            if (props.Length == 0) continue;

            ClearScene();
            var source = Spawn(type, "El" + type.Name);

            var factoryDefault = props.Select(p => p.GetValue(source)).ToArray();
            foreach (var prop in props)
            {
                var candidate = DifferentValue(prop, prop.GetValue(source));
                if (candidate == null) { unsupported.Add(Key(type, prop)); continue; }
                try { prop.SetValue(source, candidate); }
                catch (Exception e) { lost.Add(Key(type, prop) + " (запись упала: " + e.Message + ")"); }
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
                else lost.Add(Key(type, props[i]));
            }
        }

        return (lost, unsupported, compared, inert);
    }

    /// <summary>Главная проверка: КАЖДОЕ свойство, которое пользователь может
    /// изменить (а это ровно множество [Undoable]), обязано пережить сохранение и
    /// загрузку у КАЖДОГО типа. Пропуск здесь — не стилевая придирка: проект
    /// открывается тихо и не тем, чем был закрыт.
    ///
    /// Список свойств берётся рефлексией по атрибуту, список типов — рефлексией по
    /// сборке: новое свойство попадает под проверку в тот же момент, когда получает
    /// [Undoable], а новый тип — когда появляется класс.</summary>
    [Test]
    public void Restore_EveryUndoableProperty_ComesBackFromTheFile()
    {
        var (lost, unsupported, compared, inert) = SweepRestore();
        var known = KnownGaps.Select(g => g.type + "." + g.property).ToHashSet(StringComparer.Ordinal);

        var appeared = lost.Except(known).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.IsEmpty(unsupported,
            "тест не умеет придумать другое значение для этих свойств — допишите DifferentValue, "
            + "иначе они молча выпадают из проверки: " + string.Join(", ", unsupported));
        Assert.IsEmpty(appeared,
            "свойство не пережило сохранение и загрузку. Ветка ElementRestorers для этого типа есть "
            + "(её сторожит греп), но она отдаёт не всё — проект пользователя открывается не тем, "
            + "чем был закрыт:\n    " + string.Join("\n    ", appeared)
            + "\nЗакрыть пропуск или, если это осознанный долг, завести запись в KnownGaps с "
            + "причиной — но НЕ ослаблять правило.");
        Assert.Greater(compared, 0,
            $"ни одно свойство не сравнено (инертных {inert}) — тест не может провалиться, значит бесполезен");
    }

    /// <summary>Храповик обязан течь только в одну сторону: запись, чья дыра
    /// закрыта, обязана ИСЧЕЗНУТЬ, иначе потолок переживает свой долг и молча
    /// освобождает следующее поле, которое туда попадёт.</summary>
    [Test]
    public void EveryRecordedGap_StillReproduces_AndExplainsWhy()
    {
        var (lost, _, _, _) = SweepRestore();
        var types = DeclaredElementTypes().Select(t => t.Name).ToList();
        var closed = new List<string>();

        foreach (var (type, property, why) in KnownGaps)
        {
            CollectionAssert.Contains(types, type,
                "долг числится за типом, которого больше нет: " + type);
            Assert.IsNotEmpty(why,
                "долг без причины через полгода не отличить от недосмотра: " + type + "." + property);
            if (!lost.Contains(type + "." + property)) closed.Add(type + "." + property);
        }

        Assert.IsEmpty(closed,
            "долг закрыт, а запись осталась: убрать из KnownGaps — " + string.Join(", ", closed));
    }

    /// <summary>Тот же дефект, что был пойман в дублировании, но на пути
    /// сохранения: у ITabletop ДВА слота декора, а MaterialId у стола — лишь
    /// псевдоним столешницы, поэтому ветка, восстанавливающая «материал», молча
    /// теряет ножки. Слоты не помечены [Undoable] и в проверку выше не попадают.</summary>
    [Test]
    public void Restore_EveryTabletopType_KeepsBothDecorSlots()
    {
        var declared = DeclaredElementTypes().Where(t => typeof(ITabletop).IsAssignableFrom(t)).ToList();
        Assert.IsNotEmpty(declared,
            "рефлексия не нашла ни одного ITabletop — сканер смотрит не в ту сборку, и проверка "
            + "ниже зелёная, но не проверяет ничего");

        var lost = new List<string>();
        foreach (var type in declared)
        {
            ClearScene();
            var source = Spawn(type, "Top" + type.Name);
            var slots = (ITabletop)source;
            MaterialManager.ApplyTabletop(slots, MaterialCatalog.Get(TopDecorId));
            MaterialManager.ApplyLegs(slots, MaterialCatalog.Get(LegsDecorId));
            Assert.AreEqual(TopDecorId, slots.TabletopMaterialId,
                "предусловие: декор столешницы вообще назначился");
            Assert.AreEqual(LegsDecorId, slots.LegsMaterialId,
                "предусловие: декор ножек вообще назначился");

            var copy = (ITabletop)RestoreThroughFile(source);
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

        foreach (var type in DeclaredElementTypes())
        {
            ClearScene();
            var el = Spawn(type, "Sel" + type.Name);
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
    /// тип элемента обязан сначала уронить ЭТОТ тест, а не тихо пройти мимо трёх
    /// проверок выше.</summary>
    [Test]
    public void EveryElementTypeInTheAssembly_HasAFactoryCallHere()
    {
        var declared = DeclaredElementTypes();
        Assert.GreaterOrEqual(declared.Count, 16,
            "рефлексия нашла подозрительно мало типов — сканер смотрит не в ту сборку");
        CollectionAssert.Contains(declared, typeof(KitchenElement),
            "база — это «доска», и она тоже проходит через все реестры");

        CollectionAssert.AreEquivalent(declared, Makers.Select(m => m.type).ToList(),
            "список Makers разошёлся с типами элементов в сборке ядра: пока новый тип не заведён "
            + "здесь, он не проверяется ни на восстановление, ни на TypeOf");
    }
}
