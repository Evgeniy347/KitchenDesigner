using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Договорённости команд отмены, которые раньше жили комментариями в
/// Assets/Scripts/Core/Commands. Каждый тест здесь — оплата за снятый
/// комментарий: он краснеет ровно тогда, когда нарушена причина, ради которой
/// комментарий был написан.</summary>
public class UndoCommandContractTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;

    /// <summary>Элемент со счётчиком записей: реестр помеченных свойств и
    /// Restore проверяются на нём, а не на боевом типе, потому что боевому
    /// сеттеру нечем сосчитать, сколько раз его позвали.</summary>
    private sealed class ProbeElement : KitchenElement
    {
        public int ZebraSets;
        private int _zebra;
        private int _alpha;
        private int _early;

        [Undoable(Order = 7)]
        public int ZebraProbe
        {
            get => _zebra;
            set { ZebraSets++; _zebra = value; }
        }

        [Undoable(Order = 7)]
        public int AlphaProbe
        {
            get => _alpha;
            set => _alpha = value;
        }

        [Undoable(Order = -1000)]
        public int EarlyProbe
        {
            get => _early;
            set => _early = value;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _guard = ProjectLoadStateGuard.Capture();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        _guard?.Restore();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private T Add<T>(string name, Vector3Int dims) where T : KitchenElement
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<T>();
        el.PartName = name;
        el.DimensionsMM = dims;
        PartRegistry.Register(el);
        return el;
    }

    private KitchenElement Board(string name) =>
        Add<KitchenElement>(name, new Vector3Int(600, 300, 18));

    private CooktopElement Cooktop() =>
        Add<CooktopElement>("Cooktop", new Vector3Int(
            CooktopElement.DEFAULT_WIDTH_MM,
            CooktopElement.DEFAULT_HEIGHT_MM,
            CooktopElement.DEFAULT_DEPTH_MM));

    private static int IndexOfProperty(ElementPropertyBag bag, string name)
    {
        for (int i = 0; i < bag.Count; i++)
            if (bag.PropertyAt(i).Name == name) return i;
        Assert.Fail("в снимке нет свойства " + name);
        return -1;
    }

    private class NoOpCommand : IUndoCommand
    {
        public string Description => "NoOp";
        public void Execute() { }
        public void Undo() { }
    }

    [Test]
    public void CommandStack_Execute_MarksTheSceneChanged_EvenWhenTheCommandMovesNothing()
    {
        int before = SceneRevision.Version;

        CommandStack.Execute(new NoOpCommand());

        Assert.AreNotEqual(before, SceneRevision.Version,
            "через стек проходит КАЖДАЯ правка пользователя (правило «undo на всё»), поэтому "
            + "ревизию сцены поднимает сам стек: правка, которая не трогает ни трансформ, ни "
            + "реестр — включённая кромка, смена декора — иначе осталась бы незамеченной");
    }

    [Test]
    public void CommandSerialization_FromRecord_SkipsACommandWhoseElementIsGone()
    {
        var board = Board("Polka");
        var move = new MoveCommand(board, Vector3.zero, Vector3.one,
            Quaternion.identity, Quaternion.identity);
        var record = ((ISerializableCommand)move).ToRecord(_ => 0);
        Assert.IsNotNull(record, "команда с живым элементом обязана попасть в историю");

        var rebuilt = CommandSerialization.FromRecord(record!, _ => null!);

        Assert.IsNull(rebuilt,
            "resolve не нашёл объект по индексу — команду нельзя восстанавливать: она "
            + "отменяла бы правку на пустоте и падала бы при первом Ctrl+Z после загрузки");
    }

    [Test]
    public void SetCooktopCutoutCommand_Snapshot_PutsWidthInX_AndDepthInY()
    {
        var cooktop = Cooktop();
        Assume.That(cooktop.CutoutWidthMM, Is.Not.EqualTo(cooktop.CutoutDepthMM),
            "вырез обязан быть неквадратным, иначе перепутанные оси тест не увидит");

        var snapshot = SetCooktopCutoutCommand.Snapshot(cooktop);

        Assert.AreEqual(cooktop.CutoutWidthMM, snapshot.x,
            "x снимка — ШИРИНА выреза");
        Assert.AreEqual(cooktop.CutoutDepthMM, snapshot.y,
            "y снимка — ГЛУБИНА выреза; перестановка осей молча повернула бы вырез при откате");
    }

    [Test]
    public void SetCooktopCutoutCommand_Undo_ReturnsWidthAndDepthTogether()
    {
        var cooktop = Cooktop();
        var before = SetCooktopCutoutCommand.Snapshot(cooktop);
        var command = new SetCooktopCutoutCommand(cooktop, before, new Vector2Int(400, 300));

        command.Execute();
        Assert.AreEqual(400, cooktop.CutoutWidthMM);
        Assert.AreEqual(300, cooktop.CutoutDepthMM);

        command.Undo();

        Assert.AreEqual(before.x, cooktop.CutoutWidthMM,
            "ширина и глубина выреза правятся из одного блока меню и обязаны откатываться "
            + "одним шагом — иначе «Отменить» вернуло бы половину");
        Assert.AreEqual(before.y, cooktop.CutoutDepthMM,
            "второй половиной того же одного шага");
    }

    [Test]
    public void EdgeBandingState_Of_ReadsTheStoredFlag_NotTheOneHiddenForANonSheetPart()
    {
        var part = Board("Polka");
        part.EdgeBandingEnabled = true;
        Assume.That(part.EdgeBandingEnabled, Is.True, "лист кромкуется");

        part.DimensionsMM = new Vector3Int(800, 400, 60);
        Assume.That(part.SupportsEdges, Is.False, "толстый блок листом уже не считается");
        Assume.That(part.EdgeBandingEnabled, Is.False,
            "геттер гасит галочку у нелистовой детали — именно поэтому снимок его не спрашивает");

        Assert.IsTrue(EdgeBandingState.Of(part).enabled,
            "снимок берётся из Data, а не через EdgeBandingEnabled: иначе перенос состояния "
            + "(конвертация типа, дублирование) молча сбрасывал бы галочку у детали, которая "
            + "просто временно перестала быть листом");
    }

    [Test]
    public void SetGroovesCommand_Undo_GoesThroughSetGrooves_SoADuplicateInTheSnapshotCollapses()
    {
        var board = Board("Polka");
        var groove = new GrooveSpec(GrooveKind.Through, GrooveSide.Left);
        var withDuplicate = new List<GrooveSpec> { groove, groove };

        var command = new SetListCommand<GrooveSpec>(
            "Grooves test", withDuplicate, new List<GrooveSpec>(), board.SetGrooves);
        command.Execute();
        Assert.AreEqual(0, board.Grooves.Count, "«после» — пустой набор");

        command.Undo();

        Assert.AreEqual(1, board.Grooves.Count,
            "обе стороны команды идут через SetGrooves, который дедуплицирует набор и "
            + "пересобирает меш; присвоение списка напрямую вернуло бы два одинаковых паза "
            + "и меш, который о них не знает");
        Assert.AreEqual(GrooveSide.Left, board.Grooves[0].side);
    }

    [Test]
    public void SetPropertiesCommand_TryCreate_ReturnsNull_WhenNothingChanged()
    {
        var board = Board("Polka");
        var before = UndoableProperties.Capture(board);
        var after = UndoableProperties.Capture(board);

        Assert.IsNull(SetPropertiesCommand.TryCreate(board, before, after),
            "команда на пустую разницу засорила бы историю шагами, которые ничего не меняют: "
            + "«Отменить» переставало бы отменять видимое действие");

        board.PartName = "Drugaya";
        var changed = UndoableProperties.Capture(board);
        Assert.IsNotNull(SetPropertiesCommand.TryCreate(board, before, changed),
            "а на реальную разницу команда обязана появиться");
    }

    [Test]
    public void UndoableProperties_For_CachesThePropertyListPerType()
    {
        var first = UndoableProperties.For(typeof(ProbeElement));
        var second = UndoableProperties.For(typeof(ProbeElement));

        Assert.AreSame(first, second,
            "отражение считается один раз на тип: снимок берётся на каждое применение "
            + "правки, и пересбор списка ушёл бы в кадр");
    }

    [Test]
    public void UndoableProperties_For_BreaksTiesByName_SoTwoSnapshotsAreComparable()
    {
        var names = UndoableProperties.For(typeof(ProbeElement)).Select(p => p.Name).ToList();

        Assert.Less(names.IndexOf(nameof(ProbeElement.AlphaProbe)),
            names.IndexOf(nameof(ProbeElement.ZebraProbe)),
            "при равном Order порядок задаёт имя: без второго ключа сортировки порядок "
            + "определяло бы отражение, и два снимка одного типа перестали бы совпадать "
            + "индекс в индекс");

        Assert.Less(names.IndexOf(nameof(ProbeElement.EarlyProbe)),
            names.IndexOf(nameof(KitchenElement.DimensionsMM)),
            "Order сильнее имени: EarlyProbe стоит раньше DimensionsMM только из-за Order");
    }

    [Test]
    public void UndoableProperties_Restore_WritesOnlyTheListedProperties()
    {
        var probe = Add<ProbeElement>("Probe", new Vector3Int(600, 300, 18));
        probe.AlphaProbe = 1;
        probe.ZebraProbe = 1;
        var before = UndoableProperties.Capture(probe);

        probe.AlphaProbe = 2;
        probe.ZebraProbe = 2;

        UndoableProperties.Restore(probe, before,
            new List<int> { IndexOfProperty(before, nameof(ProbeElement.AlphaProbe)) });

        Assert.AreEqual(1, probe.AlphaProbe, "названное свойство возвращается");
        Assert.AreEqual(2, probe.ZebraProbe,
            "остальные свойства снимка не трогаются: команда отменяет ровно ту правку, "
            + "которую записала, а не весь снимок");
    }

    [Test]
    public void UndoableProperties_Restore_SkipsASetterWhoseValueAlreadyMatches()
    {
        var probe = Add<ProbeElement>("Probe", new Vector3Int(600, 300, 18));
        probe.ZebraProbe = 7;
        var bag = UndoableProperties.Capture(probe);
        probe.ZebraSets = 0;

        UndoableProperties.Restore(probe, bag,
            new List<int> { IndexOfProperty(bag, nameof(ProbeElement.ZebraProbe)) });

        Assert.AreEqual(0, probe.ZebraSets,
            "значение уже совпадает — сеттер звать нельзя: лишний вызов тянет за собой "
            + "пересборку меша детали");

        probe.ZebraProbe = 9;
        probe.ZebraSets = 0;
        UndoableProperties.Restore(probe, bag,
            new List<int> { IndexOfProperty(bag, nameof(ProbeElement.ZebraProbe)) });
        Assert.AreEqual(1, probe.ZebraSets, "а при реальном отличии сеттер обязан сработать");
    }
}
