using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>История отмены/повтора должна сохраняться в проект и работать после
/// загрузки (кнопки «вперёд/назад» оживают на восстановленных объектах).
/// Структура сериализации покрывается снапшотами (Snapshot.Match).</summary>
public class CommandHistoryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private string CaptureJson(IEnumerable<KitchenElement> elements)
    {
        return SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(elements));
    }

    [SetUp]
    public void Setup() => CommandStack.Clear();

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, string msg)
    {
        Assert.AreEqual(expected.x, actual.x, 0.001f, msg + " (x)");
        Assert.AreEqual(expected.y, actual.y, 0.001f, msg + " (y)");
        Assert.AreEqual(expected.z, actual.z, 0.001f, msg + " (z)");
    }

    // --- Сериализация одной команды (без файлов/сцены) ---

    [Test]
    public void MoveCommand_ToRecord_AndBack_UndoRedoWork()
    {
        var e = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var before = new Vector3(0, 0.2f, 0);
        var after = new Vector3(1, 0.2f, 0);
        var cmd = new MoveCommand(e, before, after, Quaternion.identity, Quaternion.identity);

        var rec = ((ISerializableCommand)cmd).ToRecord(_ => 0);
        Assert.IsNotNull(rec);
        Assert.AreEqual("move", rec.type);
        Assert.AreEqual(0, rec.elementIndex);

        var rebuilt = CommandSerialization.FromRecord(rec, i => e);
        Assert.IsNotNull(rebuilt);
        rebuilt.Undo();
        AssertVec(before, e.transform.position, "undo возвращает before");
        rebuilt.Execute();
        AssertVec(after, e.transform.position, "redo возвращает after");
    }

    [Test]
    public void Command_WithUnresolvableElement_IsSkipped()
    {
        var e = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var cmd = new MoveCommand(e, Vector3.zero, Vector3.one, Quaternion.identity, Quaternion.identity);

        var rec = ((ISerializableCommand)cmd).ToRecord(_ => -1);
        Assert.IsNull(rec);
    }

    // --- Полный круг: история переживает Serialize/Deserialize и работает на
    //     заново созданных объектах ---

    [Test]
    public void MoveHistory_SurvivesSaveLoad_UndoRedoAfterLoad()
    {
        var pos0 = new Vector3(0, 0.2f, 0);
        var after = new Vector3(0.5f, 0.2f, 0);
        var e = Make("HBoard", new Vector3Int(800, 400, 18), pos0);

        CommandStack.Execute(new MoveCommand(e, pos0, after, Quaternion.identity, Quaternion.identity));
        AssertVec(after, e.transform.position, "после команды объект сдвинут");
        Assert.IsTrue(CommandStack.CanUndo);

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(new[] { e }));
        Assert.IsTrue(json.Contains("undoHistory"), "история попала в JSON");

        PartRegistry.Unregister(e);
        Object.DestroyImmediate(e.gameObject);
        _spawned.Clear();
        CommandStack.Clear();
        Assert.IsFalse(CommandStack.CanUndo);

        var data = SaveLoadManager.Deserialize(json);
        var created = SaveLoadManager.RestoreScene(data);
        foreach (var go in created) _spawned.Add(go);
        Assert.AreEqual(1, created.Count);
        var restored = created[0].GetComponent<KitchenElement>();
        AssertVec(after, restored.transform.position, "загружено в актуальном состоянии");

        Assert.IsTrue(CommandStack.CanUndo, "undo доступен после загрузки");
        CommandStack.Undo();
        AssertVec(pos0, restored.transform.position, "undo откатывает к исходной позиции");

        Assert.IsTrue(CommandStack.CanRedo);
        CommandStack.Redo();
        AssertVec(after, restored.transform.position, "redo возвращает сдвиг");
    }

    [Test]
    public void ResizeHistory_SurvivesSaveLoad_UndoRedoAfterLoad()
    {
        var pos = new Vector3(0, 0.2f, 0);
        var dimsBefore = new Vector3Int(800, 400, 18);
        var dimsAfter = new Vector3Int(1200, 400, 18);
        var e = Make("RBoard", dimsBefore, pos);

        CommandStack.Execute(new ResizeCommand(e, dimsBefore, dimsAfter, pos, pos,
            Quaternion.identity, Quaternion.identity));
        Assert.AreEqual(dimsAfter, e.DimensionsMM);

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(new[] { e }));

        PartRegistry.Unregister(e);
        Object.DestroyImmediate(e.gameObject);
        _spawned.Clear();
        CommandStack.Clear();

        var created = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json));
        foreach (var go in created) _spawned.Add(go);
        var restored = created[0].GetComponent<KitchenElement>();
        Assert.AreEqual(dimsAfter, restored.DimensionsMM, "загружено в увеличенном размере");

        Assert.IsTrue(CommandStack.CanUndo);
        CommandStack.Undo();
        Assert.AreEqual(dimsBefore, restored.DimensionsMM, "undo возвращает исходный размер");
        CommandStack.Redo();
        Assert.AreEqual(dimsAfter, restored.DimensionsMM, "redo возвращает увеличенный размер");
    }

    [Test]
    public void LoadingProject_WithoutHistory_ClearsStaleCommands()
    {
        var e = Make("Old", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));
        CommandStack.Execute(new MoveCommand(e, Vector3.zero, Vector3.one,
            Quaternion.identity, Quaternion.identity));
        Assert.IsTrue(CommandStack.CanUndo);

        var data = new ProjectData(new[] { ElementData.FromElement(e) });
        PartRegistry.Unregister(e);
        Object.DestroyImmediate(e.gameObject);
        _spawned.Clear();

        var created = SaveLoadManager.RestoreScene(data);
        foreach (var go in created) _spawned.Add(go);

        Assert.IsFalse(CommandStack.CanUndo, "история очищена при загрузке проекта без истории");
    }

    // --- Снапшоты: структура сериализации истории ---

    [Test]
    public void RoundTrip_FlatComposite_Survives()
    {
        var a = Make("A_RT", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B_RT", new Vector3Int(600, 400, 18), new Vector3(1, 0, 0));
        CommandStack.Execute(new CompositeCommand("flat", new List<IUndoCommand>
        {
            new MoveCommand(a, Vector3.zero, new Vector3(0, 0.5f, 0),
                Quaternion.identity, Quaternion.identity),
            new MoveCommand(b, new Vector3(1, 0, 0), new Vector3(1, 0.5f, 0),
                Quaternion.identity, Quaternion.identity),
        }));

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { a, b }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data);
        Assert.AreEqual(1, data.undoHistory.Length,
            "плоский composite переживает round-trip");
    }

    [Test]
    public void RoundTrip_3LevelComposite_Survives()
    {
        var e = Make("E_RT", new Vector3Int(800, 400, 18), Vector3.zero);

        var leaf = new MoveCommand(e, Vector3.zero, Vector3.one,
            Quaternion.identity, Quaternion.identity);
        var level2 = new CompositeCommand("L2", new List<IUndoCommand> { leaf });
        var level1 = new CompositeCommand("L1", new List<IUndoCommand> { level2 });
        var level0 = new CompositeCommand("L0", new List<IUndoCommand> { level1 });
        CommandStack.Execute(level0);

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { e }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data);
        Assert.AreEqual(1, data.undoHistory.Length,
            "вложенный composite схлопывается в одну плоскую запись");
        Assert.AreEqual(1, data.undoHistory[0].children.Length,
            "единственный лист сохранён");
        Assert.AreEqual("move", data.undoHistory[0].children[0].type);
    }

    [Test]
    public void Snapshot_UndoHistory_CompositeTwoMoves()
    {
        var a = Make("Board_A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("Board_B", new Vector3Int(600, 400, 18), new Vector3(1, 0, 0));
        CommandStack.Execute(new CompositeCommand("group", new List<IUndoCommand>
        {
            new MoveCommand(a, Vector3.zero, new Vector3(0, 0.5f, 0),
                Quaternion.identity, Quaternion.identity),
            new MoveCommand(b, new Vector3(1, 0, 0), new Vector3(1, 0.5f, 0),
                Quaternion.identity, Quaternion.identity),
        }));
        Snapshot.Match(CaptureJson(new[] { a, b }), "undo_composite_two_moves");
    }

    [Test]
    public void DeepChain50_FlattensToSingleLeaf()
    {
        var e = Make("Board_D", new Vector3Int(800, 400, 18), Vector3.zero);

        // 50 вложенных composites, каждый оборачивает предыдущий (один лист внизу).
        // Плоская сериализация: цепочка схлопывается в ОДИН composite с одним листом,
        // глубина JSON — константа (никакого лимита JsonUtility 10).
        IUndoCommand leaf = new MoveCommand(e, Vector3.zero, Vector3.one,
            Quaternion.identity, Quaternion.identity);
        for (int i = 0; i < 50; i++)
            leaf = new CompositeCommand($"L{i}", new List<IUndoCommand> { leaf });

        CommandStack.Execute(leaf);

        var data = SaveLoadManager.Deserialize(CaptureJson(new[] { e }));
        Assert.AreEqual(1, data.undoHistory.Length, "одна запись верхнего уровня");
        var rec = data.undoHistory[0];
        Assert.AreEqual("composite", rec.type);
        Assert.AreEqual(1, rec.children.Length, "лист собран на один уровень");
        Assert.AreEqual("move", rec.children[0].type);
        Assert.IsTrue(IsFlat(rec), "children не вложены глубже одного уровня");
    }

    [Test]
    public void DeepWithSiblings50_FlattensAllLeaves()
    {
        var e = Make("Board_S", new Vector3Int(800, 400, 18), Vector3.zero);

        // Каждый уровень: Composite([MoveCommand, nextComposite]) — 50 листьев,
        // разбросанных по 50 уровням вложенности. Плоская сериализация собирает
        // ВСЕ 50 листьев в один уровень children, ничего не теряя.
        IUndoCommand current = null;
        for (int i = 49; i >= 0; i--)
        {
            var from = new Vector3(i * 0.001f, 0, 0);
            var to   = new Vector3((i + 1) * 0.001f, 0, 0);
            var move = new MoveCommand(e, from, to,
                Quaternion.identity, Quaternion.identity);
            var siblings = new List<IUndoCommand> { move };
            if (current != null) siblings.Add(current);
            current = new CompositeCommand($"L{i}", siblings);
        }

        CommandStack.Execute(current);

        var data = SaveLoadManager.Deserialize(CaptureJson(new[] { e }));
        Assert.AreEqual(1, data.undoHistory.Length);
        var rec = data.undoHistory[0];
        Assert.AreEqual("composite", rec.type);
        Assert.AreEqual(50, rec.children.Length, "все 50 листьев сохранены (без обрезки)");
        Assert.IsTrue(IsFlat(rec), "children не вложены глубже одного уровня");
    }

    /// <summary>composite-запись плоская: её дети — только листья (без вложенных children).</summary>
    private static bool IsFlat(CommandRecord composite)
    {
        if (composite.children == null) return true;
        foreach (var c in composite.children)
            if (c.children != null && c.children.Length > 0) return false;
        return true;
    }

    [Test]
    public void Snapshot_UndoHistory_ResizeCommand()
    {
        var e = Make("Board_R", new Vector3Int(800, 400, 18), Vector3.zero);
        CommandStack.Execute(new ResizeCommand(e,
            new Vector3Int(800, 400, 18), new Vector3Int(1200, 600, 18),
            Vector3.zero, new Vector3(0.2f, 0.1f, 0),
            Quaternion.identity, Quaternion.identity));
        Snapshot.Match(CaptureJson(new[] { e }), "undo_resize_command");
    }

    // --- Нагрузочные: важна целостность, а не структура JSON ---

    [Test]
    public void LargeFlatHistory_1000Commands_SerializeOk()
    {
        var e = Make("L", new Vector3Int(800, 400, 18), Vector3.zero);

        for (int i = 0; i < 1000; i++)
        {
            var from = new Vector3(i * 0.001f, 0.2f, 0);
            var to   = new Vector3(i * 0.001f + 0.1f, 0.2f, 0);
            CommandStack.Execute(new MoveCommand(e, from, to,
                Quaternion.identity, Quaternion.identity));
        }
        Assert.AreEqual(1000, CommandStack.UndoCount);

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { e }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.AreEqual(1000, data.undoHistory.Length,
            "все 1000 команд пережили сериализацию");

        var created = SaveLoadManager.RestoreScene(data);
        Assert.IsTrue(CommandStack.CanUndo);
        CommandStack.Undo();
        Assert.IsTrue(CommandStack.CanUndo);
    }

    [Test]
    public void RoundTrip_DeepComposite_FlattensAndUndoWorks()
    {
        var pos0 = new Vector3(0, 0.2f, 0);
        var e = Make("Board_RT", new Vector3Int(800, 400, 18), pos0);

        // 50-уровневая вложенная группа: композит из 50 перемещений одного объекта,
        // разложенных по 50 уровням. После плоской сериализации все 50 листьев
        // сохраняются на одном уровне, а undo композита атомарно откатывает всё.
        IUndoCommand current = null;
        Vector3 firstFrom = pos0;
        for (int i = 49; i >= 0; i--)
        {
            var from = i == 0 ? pos0 : new Vector3(i * 0.001f, 0, 0);
            var to   = new Vector3((i + 1) * 0.001f, 0, 0);
            if (i == 0) firstFrom = from;
            var move = new MoveCommand(e, from, to,
                Quaternion.identity, Quaternion.identity);
            var siblings = new List<IUndoCommand> { move };
            if (current != null) siblings.Add(current);
            current = new CompositeCommand($"L{i}", siblings);
        }
        CommandStack.Execute(current);

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { e }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, "ProjectData десериализуется");

        // Одна запись верхнего уровня с 50 плоскими листьями — ничего не потеряно.
        Assert.AreEqual(1, data.undoHistory.Length);
        Assert.AreEqual(50, data.undoHistory[0].children.Length,
            "все 50 перемещений сохранены при плоской сериализации");

        // История оживает на заново созданном объекте, undo работает.
        PartRegistry.Unregister(e);
        Object.DestroyImmediate(e.gameObject);
        _spawned.Clear();
        CommandStack.Clear();

        var created = SaveLoadManager.RestoreScene(data);
        foreach (var go in created) _spawned.Add(go);
        var restored = created[0].GetComponent<KitchenElement>();

        Assert.IsTrue(CommandStack.CanUndo, "undo доступен после загрузки");
        CommandStack.Undo();
        AssertVec(firstFrom, restored.transform.position,
            "undo композита откатывает объект к начальной позиции");
    }

    [Test]
    public void LargeHistory_WithGroupMoves_SerializeOk()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(1, 0, 0));

        for (int i = 0; i < 500; i++)
        {
            var dx = i * 0.01f;
            CommandStack.Execute(new MoveCommand(a,
                new Vector3(dx, 0, 0), new Vector3(dx + 0.01f, 0, 0),
                Quaternion.identity, Quaternion.identity));

            var cmds = new List<IUndoCommand>
            {
                new MoveCommand(a,
                    new Vector3(dx + 0.01f, 0, 0), new Vector3(dx + 0.02f, 0, 0),
                    Quaternion.identity, Quaternion.identity),
                new MoveCommand(b,
                    new Vector3(1 + dx, 0, 0), new Vector3(1 + dx + 0.01f, 0, 0),
                    Quaternion.identity, Quaternion.identity),
            };
            CommandStack.Execute(new CompositeCommand($"group {i}", cmds));
        }
        Assert.AreEqual(1000, CommandStack.UndoCount);

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { a, b }));
        var data = SaveLoadManager.Deserialize(json);
        Assert.AreEqual(1000, data.undoHistory.Length,
            "1000 команд (включая composite) пережили сериализацию");

        var created = SaveLoadManager.RestoreScene(data);
        Assert.IsTrue(CommandStack.CanUndo);
    }
}
