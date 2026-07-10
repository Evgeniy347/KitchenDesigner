using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>История отмены/повтора должна сохраняться в проект и работать после
/// загрузки (кнопки «вперёд/назад» оживают на восстановленных объектах).</summary>
public class CommandHistoryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        BoardRegistry.Register(e);
        _spawned.Add(go);
        return e;
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
            BoardRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        // Объекты, созданные RestoreScene.
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

        // Объект не попал в сохранение → индекс -1 → запись не создаётся.
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

        // Имитация перезапуска: убрать сцену и историю.
        BoardRegistry.Unregister(e);
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

        // Кнопка «назад» работает на восстановленном объекте.
        Assert.IsTrue(CommandStack.CanUndo, "undo доступен после загрузки");
        CommandStack.Undo();
        AssertVec(pos0, restored.transform.position, "undo откатывает к исходной позиции");

        // Кнопка «вперёд» работает.
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

        BoardRegistry.Unregister(e);
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
        // Старый проект без истории не должен оставлять команды, ссылающиеся на
        // уничтоженные объекты.
        var e = Make("Old", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));
        CommandStack.Execute(new MoveCommand(e, Vector3.zero, Vector3.one,
            Quaternion.identity, Quaternion.identity));
        Assert.IsTrue(CommandStack.CanUndo);

        var data = new ProjectData(new[] { ElementData.FromElement(e) }); // без undoHistory
        BoardRegistry.Unregister(e);
        Object.DestroyImmediate(e.gameObject);
        _spawned.Clear();

        var created = SaveLoadManager.RestoreScene(data);
        foreach (var go in created) _spawned.Add(go);

        Assert.IsFalse(CommandStack.CanUndo, "история очищена при загрузке проекта без истории");
    }
}
