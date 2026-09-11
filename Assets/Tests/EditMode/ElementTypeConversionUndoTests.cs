using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>
/// Круг «конверсия → отмена → повтор» для строки «Тип» панели свойств, ПОПАРНО по всем
/// четырём целевым типам.
///
/// Почему попарно и почему сравнивается весь снимок, а не перечень полей: конверсия
/// уничтожает компонент и собирает новый, а <c>ElementConverter.Convert</c> переносит
/// только то, что в нём выписано руками. Поле, которое забыли перенести, теряется МОЛЧА —
/// ни исключения, ни лога, ни красного теста. Поэтому сторожем здесь работает не список
/// свойств, а сличение ВСЕГО снимка <c>ElementCapture.FromElement</c> (того же, что едет в
/// файл проекта) до конверсии и после отмены: любое новое поле формата попадает под
/// проверку само, без правки этого файла.
///
/// Пара «отмена» и «повтор» несут разные вопросы. Отмена обязана вернуть ИСХОДНОЕ
/// состояние целиком; повтор — вернуть ровно тот результат, который дала первая конверсия,
/// а не «какой-нибудь элемент нужного типа».
///
/// Подопытный адресуется <c>GameObject</c>-ом: ссылка на <c>KitchenElement</c> после
/// конверсии мертва, и тест обязан перечитывать компонент ровно так же, как это делает
/// сама <c>ConvertElementCommand</c>.
/// </summary>
public class ElementTypeConversionUndoTests
{
    /// <summary>Четыре структурных типа и их фабричные заготовки. Тип берётся у общего
    /// перебора <c>EveryElementType</c> — второго списка фабричных вызовов здесь не
    /// заводится, — а согласие этой пары с самим конвертером стережёт
    /// <see cref="TargetTypeOfTheSnapshot_AgreesWithTheTypeOfTheElement"/>.</summary>
    private static readonly (ElementConverter.TargetType target, Type type)[] Structural =
    {
        (ElementConverter.TargetType.Part, typeof(KitchenElement)),
        (ElementConverter.TargetType.Facade, typeof(FacadeElement)),
        (ElementConverter.TargetType.AssembledFacade, typeof(AssembledFacadeElement)),
        (ElementConverter.TargetType.RadialShelf, typeof(RadialShelfElement)),
    };

    private static IEnumerable<(ElementConverter.TargetType from, ElementConverter.TargetType to)> Pairs()
    {
        foreach (var (from, _) in Structural)
            foreach (var (to, _) in Structural)
                if (from != to)
                    yield return (from, to);
    }

    private bool _ignoreLogsBefore;

    [SetUp]
    public void SetUp()
    {
        // Выделение красит деталь, а сборка мусора материалов в EditMode шумит
        // «leaked objects» — тот же приём, что в PipePanelChoiceUndoGuardTests.
        _ignoreLogsBefore = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        CommandStack.Clear();
        EveryElementType.ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        EveryElementType.ClearScene();
        LogAssert.ignoreFailingMessages = _ignoreLogsBefore;
    }

    private static KitchenElement Spawn(ElementConverter.TargetType target, string name)
    {
        foreach (var (t, type) in Structural)
            if (t == target)
                return EveryElementType.Spawn(type, name);
        throw new ArgumentOutOfRangeException(nameof(target), target, "не структурный тип");
    }

    /// <summary>Каждое поле уводится с умолчания: значение, совпавшее с заводским, не
    /// отличить от потерянного (`agents/TEST-DESIGN.md` → «Две противоположные проверки»).
    /// Что именно тип умеет носить, спрашивается у него самого, а не угадывается.</summary>
    private static void Dress(KitchenElement element, string hostName)
    {
        element.Movable = false;
        element.Transparent = true;
        element.GroupId = 7;
        element.AttachedToName = hostName;

        if (element.SupportsGaps)
        {
            element.GapLeft = 3;
            element.GapRight = 4;
            element.GapTop = 5;
            element.GapBottom = 6;
            element.GapFront = 7;
            element.GapBack = 8;
        }

        if (element.SupportsGrooves)
        {
            element.SetGrooves(new[]
            {
                new GrooveSpec(GrooveKind.Blind, GrooveSide.Left),
                new GrooveSpec(GrooveKind.Through, GrooveSide.Bottom),
            });
            element.EdgeBandingEnabled = true;
            element.EdgeThicknessMM = 2f;
            element.EdgeForcedMask = 3;
            element.EdgeSuppressedMask = 4;
        }

        if (element is RadialShelfElement radial)
            radial.CornerRadius = 151;

        if (element is AssembledFacadeElement assembled)
        {
            assembled.Fill = AssembledFill.Open;
            assembled.GrooveCount = 3;
        }

        if (element is FacadeElement facade)
        {
            facade.Mode = DoorMode.HingeBackRight;
            facade.SetOpen(true);
        }
    }

    private static KitchenElement Live(GameObject go)
    {
        Assert.IsTrue(go != null, "конверсия не вправе уничтожать сам объект сцены");
        var element = go.GetComponent<KitchenElement>();
        Assert.IsTrue(element != null,
            "после конверсии на объекте обязан висеть KitchenElement нового типа");
        return element!;
    }

    private static List<string> Diff(ElementData expected, ElementData actual)
    {
        var differences = new List<string>();
        foreach (var field in typeof(ElementData).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var a = Describe(field.GetValue(expected));
            var b = Describe(field.GetValue(actual));
            if (!string.Equals(a, b, StringComparison.Ordinal))
                differences.Add($"{field.Name}: было «{a}», стало «{b}»");
        }
        return differences;
    }

    private static string Describe(object? value)
    {
        if (value == null) return "null";
        if (value is Array array)
        {
            var parts = new List<string>(array.Length);
            foreach (var item in array) parts.Add(Describe(item));
            return "[" + string.Join(", ", parts) + "]";
        }
        if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return JsonUtility.ToJson(value);
    }

    /// <summary>Сенсор, ради которого весь набор: снимок формата до конверсии и после
    /// отмены обязан совпасть ПОЛЕ В ПОЛЕ. Красное называет поле, а не «что-то не так».</summary>
    [Test]
    public void EveryOrderedPairOfTypes_SurvivesConversionAndUndo_FieldForField()
    {
        var failures = new List<string>();

        foreach (var (from, to) in Pairs())
        {
            EveryElementType.ClearScene();
            CommandStack.Clear();

            var host = EveryElementType.Spawn(typeof(KitchenElement), "HOST");
            var element = Spawn(from, "SUBJECT");
            Dress(element, host.PartName);
            var subject = element.gameObject;
            var before = ElementCapture.FromElement(element);

            var converted = ConvertElementCommand.Run(element, to, null);
            if (converted == null)
            {
                failures.Add($"{from} → {to}: команда конверсии не создалась вовсе");
                continue;
            }

            CommandStack.Undo();

            var restored = Live(subject);
            if (ElementConverter.GetElementType(restored) != from)
            {
                failures.Add($"{from} → {to}: отмена оставила тип "
                             + $"{ElementConverter.GetElementType(restored)}");
                continue;
            }

            foreach (var line in Diff(before, ElementCapture.FromElement(restored)))
                failures.Add($"{from} → {to}: {line}");
        }

        Assert.IsEmpty(failures,
            "\nОтмена смены типа обязана вернуть элемент целиком: ElementConverter.Convert "
            + "переносит между типами только выписанные в нём поля, остальное доливается из "
            + "снимка ElementCapture.FromElement в ConvertedElementRestore. Поле в списке ниже "
            + "не переносится и не доливается — оно теряется молча.\n"
            + string.Join("\n", failures));
    }

    /// <summary>Повтор обязан вернуть ТОТ ЖЕ результат, что дала первая конверсия, а не
    /// просто элемент нужного типа: разница между ними — ровно те поля, которые долив
    /// восстановил «не туда».</summary>
    [Test]
    public void EveryOrderedPairOfTypes_RedoReproducesTheFirstConversion_FieldForField()
    {
        var failures = new List<string>();

        foreach (var (from, to) in Pairs())
        {
            EveryElementType.ClearScene();
            CommandStack.Clear();

            var host = EveryElementType.Spawn(typeof(KitchenElement), "HOST");
            var element = Spawn(from, "SUBJECT");
            Dress(element, host.PartName);
            var subject = element.gameObject;

            var converted = ConvertElementCommand.Run(element, to, null);
            if (converted == null)
            {
                failures.Add($"{from} → {to}: команда конверсии не создалась вовсе");
                continue;
            }

            var afterFirst = ElementCapture.FromElement(converted);

            CommandStack.Undo();
            CommandStack.Redo();

            var redone = Live(subject);
            if (ElementConverter.GetElementType(redone) != to)
            {
                failures.Add($"{from} → {to}: повтор оставил тип "
                             + $"{ElementConverter.GetElementType(redone)}");
                continue;
            }

            foreach (var line in Diff(afterFirst, ElementCapture.FromElement(redone)))
                failures.Add($"{from} → {to}: {line}");
        }

        Assert.IsEmpty(failures,
            "\n«Повторить» обязано воспроизвести первую конверсию дословно.\n"
            + string.Join("\n", failures));
    }

    /// <summary>Сторож самого сенсора: если <see cref="Dress"/> перестанет уводить поля с
    /// умолчаний, сличение снимков станет сравнением двух заводских элементов и не сможет
    /// провалиться никогда.</summary>
    [Test]
    public void TheDressedSubject_DiffersFromAFactoryOne_ForEveryStructuralType()
    {
        var silent = new List<string>();

        foreach (var (target, _) in Structural)
        {
            EveryElementType.ClearScene();
            var host = EveryElementType.Spawn(typeof(KitchenElement), "HOST");
            var plain = ElementCapture.FromElement(Spawn(target, "PLAIN"));
            var dressed = Spawn(target, "DRESSED");
            Dress(dressed, host.PartName);

            var differences = Diff(plain, ElementCapture.FromElement(dressed));
            differences.RemoveAll(line => line.StartsWith("name:", StringComparison.Ordinal));
            if (differences.Count < 5)
                silent.Add($"{target}: отличий от заводского всего {differences.Count} "
                           + $"({string.Join("; ", differences)})");
        }

        Assert.IsEmpty(silent,
            "\nПодопытный обязан отличаться от заводского многими полями — иначе сличение "
            + "снимков ничего не стережёт.\n" + string.Join("\n", silent));
    }

    [Test]
    public void Conversion_IsExactlyOneUndoStep()
    {
        var element = EveryElementType.Spawn(typeof(KitchenElement), "SUBJECT");
        CommandStack.Clear();

        ConvertElementCommand.Run(element, ElementConverter.TargetType.Facade, null);

        Assert.AreEqual(1, CommandStack.UndoCount,
            "смена типа — один шаг отмены, а не ноль и не два");
    }

    [Test]
    public void Conversion_KeepsThePartNameAndTheLinksThatPointAtItByName()
    {
        var host = EveryElementType.Spawn(typeof(KitchenElement), "HOST");
        var child = EveryElementType.Spawn(typeof(KitchenElement), "CHILD");
        child.AttachedToName = host.PartName;
        var subject = host.gameObject;

        var converted = ConvertElementCommand.Run(host, ElementConverter.TargetType.Facade, null);

        Assert.IsTrue(converted != null, "конверсия обязана вернуть новый компонент");
        Assert.AreEqual("HOST", converted!.PartName,
            "имя детали — ключ привязок AttachedToName: конверсия не вправе его менять");
        Assert.AreEqual("HOST", child.AttachedToName,
            "привязка соседа по имени обязана пережить конверсию хозяина");

        CommandStack.Undo();

        Assert.AreEqual("HOST", Live(subject).PartName, "отмена обязана вернуть и имя");
        Assert.AreEqual("HOST", child.AttachedToName, "отмена не вправе рвать привязку соседа");
    }

    [Test]
    public void Conversion_LeavesTheRegistryHoldingTheNewComponentOnly()
    {
        var element = EveryElementType.Spawn(typeof(KitchenElement), "SUBJECT");
        int before = PartRegistry.GetAll().Count;

        var converted = ConvertElementCommand.Run(element, ElementConverter.TargetType.RadialShelf, null);

        Assert.IsTrue(converted != null, "конверсия обязана вернуть новый компонент");
        Assert.AreEqual(before, PartRegistry.GetAll().Count,
            "конверсия не добавляет и не теряет деталь — она заменяет компонент");
        CollectionAssert.Contains(PartRegistry.GetAll(), converted!,
            "в реестре обязан оказаться НОВЫЙ компонент: мёртвая ссылка там — источник "
            + "MissingReferenceException у следующего читателя");
    }

    /// <summary>Внешнее: выделение держит ссылку на компонент, а он после конверсии мёртв.
    /// Снимать его и ставить заново обязан сам шаг, иначе выделенной остаётся пустота.</summary>
    [Test]
    public void Conversion_MovesTheSelectionOntoTheNewComponent()
    {
        var holder = new GameObject("SelectionForConversion");
        var selection = holder.AddComponent<SelectionManager>();
        var selectionBefore = SelectionManager.Instance;
        SelectionManager.Instance = selection;
        try
        {
            var element = EveryElementType.Spawn(typeof(KitchenElement), "SUBJECT");
            var subject = element.gameObject;
            selection.Select(element);

            var converted = ConvertElementCommand.Run(element, ElementConverter.TargetType.Facade, null);

            Assert.IsTrue(converted != null);
            Assert.IsTrue(selection.IsSelected(converted!),
                "после конверсии выделенным обязан быть новый компонент");

            CommandStack.Undo();

            Assert.IsTrue(selection.IsSelected(Live(subject)),
                "после отмены выделенным обязан быть вернувшийся компонент");
        }
        finally
        {
            selection.DeselectAll();
            SelectionManager.Instance = selectionBefore;
            UnityEngine.Object.DestroyImmediate(holder);
        }
    }

    /// <summary>Окно свойств держит собственный <c>_target</c>, и ядро о нём не знает —
    /// поэтому команда зовёт обратный вызов на КАЖДОМ шаге, а не только при первой
    /// конверсии. Без этого после Ctrl+Z панель показывает мёртвый элемент.</summary>
    [Test]
    public void EveryStepOfTheConversion_HandsTheNewComponentToTheCaller()
    {
        var element = EveryElementType.Spawn(typeof(KitchenElement), "SUBJECT");
        var subject = element.gameObject;
        var handed = new List<KitchenElement>();

        ConvertElementCommand.Run(element, ElementConverter.TargetType.AssembledFacade, handed.Add);
        CommandStack.Undo();
        CommandStack.Redo();

        Assert.AreEqual(3, handed.Count,
            "конверсия, отмена и повтор — три шага, и каждый обязан назвать новый компонент");
        foreach (var given in handed)
            Assert.AreSame(subject, given.gameObject,
                "шаг обязан отдавать компонент того же объекта сцены");
        Assert.IsTrue(handed[0] is AssembledFacadeElement, "первый шаг — конверсия");
        Assert.AreEqual(typeof(KitchenElement), handed[1].GetType(), "второй шаг — отмена");
        Assert.IsTrue(handed[2] is AssembledFacadeElement, "третий шаг — повтор");
    }

    /// <summary>Пара таблиц об одном и том же (тип живого элемента и тип его снимка)
    /// обязана сверяться тестом: отмена определяет, во что конвертировать обратно, именно
    /// по снимку.</summary>
    [Test]
    public void TargetTypeOfTheSnapshot_AgreesWithTheTypeOfTheElement()
    {
        foreach (var (target, type) in Structural)
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "SUBJECT");
            Assert.AreEqual(target, ElementConverter.GetElementType(element),
                "пара «целевой тип → класс» в тесте разошлась с конвертером: " + type.Name);
            Assert.AreEqual(target, ElementConverter.TargetTypeOf(ElementCapture.FromElement(element)),
                "тип, прочитанный со снимка, разошёлся с типом элемента: " + type.Name);
        }
    }

    /// <summary>Шаг обязан лечь в <c>undoHistory</c> файла проекта: иначе смена типа
    /// исчезает из хроники при сохранении, и «откуда взялась эта деталь» больше не
    /// проследить.</summary>
    [Test]
    public void ConversionStep_SurvivesTheProjectFile()
    {
        var element = EveryElementType.Spawn(typeof(KitchenElement), "SUBJECT");
        Dress(element, "");
        var subject = element.gameObject;
        var before = ElementCapture.FromElement(element);
        CommandStack.Clear();

        ConvertElementCommand.Run(element, ElementConverter.TargetType.RadialShelf, null);

        var scene = PartRegistry.GetAll();
        var records = CommandStack.ExportUndo(e => scene.IndexOf(e));
        Assert.AreEqual(1, records.Count, "шаг конверсии обязан экспортироваться");
        Assert.AreEqual("convert", records[0].type);

        var json = JsonUtility.ToJson(records[0]);
        var reread = JsonUtility.FromJson<CommandRecord>(json);
        var revived = CommandSerialization.FromRecord(reread, i => scene[i]);
        Assert.IsNotNull(revived,
            "запись convert обязана собираться обратно в команду — иначе шаг, доехавший до "
            + "файла, при открытии проекта молча исчезает из хроники");

        revived!.Undo();

        var restored = Live(subject);
        Assert.AreEqual(ElementConverter.TargetType.Part,
            ElementConverter.GetElementType(restored),
            "отмена, поднятая из файла, обязана работать так же, как живая");
        Assert.IsEmpty(Diff(before, ElementCapture.FromElement(restored)),
            "снимок, доехавший до файла, обязан возвращать элемент целиком");
    }
}
