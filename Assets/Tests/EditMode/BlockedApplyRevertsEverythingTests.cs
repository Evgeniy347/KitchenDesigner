using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Дефект D3, который пользователь ловил руками. Применение, заблокированное
/// нарушением (`BlockOnViolation`), возвращало ТРИ поля из дюжины: `DimensionsMM`, позицию и
/// поворот. А выше по `ContextMenuUI.Apply` уже отработали редакторы типов, выбор второго
/// слота материала, зазоры, толщина кромки и `ApplyAfterPosition` — всё это оставалось в
/// сцене, панель показывала старую геометрию, и «отменить» отменяло не то, что применилось.
///
/// Правильный откат — не список полей, который кто-то вспомнил, а снимок: `UndoableProperties`
/// до применения плюс `CommandStack.EndCapture(commit: false)`, который откатывает КАЖДУЮ
/// команду, выполненную внутри захвата. Тесты сравнивают снимок целиком и считают записи
/// отмены, а не миллисекунды.
///
/// Одинокая деталь в пустой сцене сама по себе нарушение (не на что опереться) — именно так
/// оба теста включают блокировку без искусственных подпорок.</summary>
public class BlockedApplyRevertsEverythingTests
{
    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _blockBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = true;
        _root = new GameObject("BlockedApplyRoot");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        KitchenSettings.Instance.BlockOnViolation = _blockBefore;
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        if (_root != null) Object.DestroyImmediate(_root);
        PartRegistry.Clear();
    }

    private KitchenElement SpawnPart(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(600, 700, 18);
        PartRegistry.Register(el);
        return el;
    }

    private static List<string> SnapshotOf(KitchenElement el)
    {
        var bag = UndoableProperties.Capture(el);
        var parts = new List<string>();
        for (int i = 0; i < bag.Count; i++)
            parts.Add($"{bag.PropertyAt(i).Name}={bag.ValueAt(i)}");
        parts.Add($"dims={el.DimensionsMM}");
        parts.Add($"pos={el.transform.position:F4}");
        parts.Add($"rot={el.transform.rotation.eulerAngles:F2}");
        return parts;
    }

    private static string Diff(List<string> before, List<string> after)
    {
        var lines = new List<string>();
        int n = Mathf.Min(before.Count, after.Count);
        for (int i = 0; i < n; i++)
            if (before[i] != after[i]) lines.Add($"{before[i]} → {after[i]}");
        return string.Join("; ", lines);
    }

    private ContextMenuUI BuildMenu()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();
        ctx.Build(_root!.transform);
        return ctx;
    }

    [Test]
    public void BlockedApply_LeavesNoEditInTheScene_AndNoUndoRecord()
    {
        var el = SpawnPart("Деталь");
        var ctx = BuildMenu();
        ctx.Open(el);
        CommandStack.Clear();

        var before = SnapshotOf(el);

        ctx.SetNameFieldTextForTests("Переименованная");
        ctx.SetWidthFieldTextForTests("900");
        ctx.SimulateApplyForTests();

        var after = SnapshotOf(el);

        Assert.AreEqual(600, el.DimensionsMM.x,
            "предусловие: блокировка обязана была сработать и откатить геометрию — иначе тест "
            + "проверяет обычное применение");
        Assert.AreEqual(before, after,
            "заблокированное применение не имеет права оставить в сцене НИ ОДНОЙ правки. "
            + "Осталось: " + Diff(before, after));
        Assert.AreEqual(0, CommandStack.UndoCount,
            "ничего не применилось — значит и отменять нечего; запись в стеке означала бы, "
            + "что отмена вернёт состояние, которого пользователь никогда не видел");
    }

    /// <summary>Положительный контроль: без блокировки те же самые правки обязаны примениться
    /// и лечь в стек одним шагом. Иначе зелёный выше значил бы «панель не работает вообще».</summary>
    [Test]
    public void SameEditWithoutBlocking_IsAppliedAndUndoable()
    {
        var el = SpawnPart("Деталь");
        KitchenSettings.Instance.BlockOnViolation = false;
        var ctx = BuildMenu();
        ctx.Open(el);
        CommandStack.Clear();

        ctx.SetNameFieldTextForTests("Переименованная");
        ctx.SetWidthFieldTextForTests("900");
        ctx.SimulateApplyForTests();

        Assert.AreEqual(900, el.DimensionsMM.x, "без блокировки ширина обязана примениться");
        Assert.AreEqual("Переименованная", el.PartName, "без блокировки имя обязано примениться");
        Assert.AreEqual(1, CommandStack.UndoCount, "одна правка — ровно один шаг отмены");
    }

    /// <summary>Тот же откат в перетаскивании: `ElementMover.RevertMoveSet` возвращал позицию и
    /// поворот, но не размеры, хотя жест их меняет (посадка, подгонка пролёта трубы).</summary>
    [Test]
    public void BlockedDrag_RevertsDimensionsToo_NotOnlyPositionAndRotation()
    {
        var el = SpawnPart("Деталь");
        var moverGo = new GameObject("ElementMover");
        _spawned.Add(moverGo);
        var mover = moverGo.AddComponent<ElementMover>();

        mover.BeginDragOn(el);
        el.DimensionsMM = new Vector3Int(900, 700, 18);
        el.transform.position = new Vector3(2f, 0f, 0f);
        mover.FinishDragNow();

        Assert.AreEqual(new Vector3Int(600, 700, 18), el.DimensionsMM,
            "жест отменён по нарушению — размеры обязаны вернуться вместе с позицией, "
            + "иначе деталь остаётся раздутой без единой записи отмены");
        Assert.AreEqual(0f, el.transform.position.x, 1e-4f,
            "предусловие: позиция откатывается (её откат был и до правки)");
        Assert.AreEqual(0, CommandStack.UndoCount, "отменённый жест ничего не пишет в стек");
    }
}
