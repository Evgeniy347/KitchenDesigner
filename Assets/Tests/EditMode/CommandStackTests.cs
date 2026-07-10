using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class CommandStackTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private class FakeCommand : IUndoCommand
    {
        public static int ExecCount;
        public static int UndoCount;
        public string Description => "Fake";
        public void Execute() => ExecCount++;
        public void Undo() => UndoCount++;
    }

    private KitchenElement Make(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void Setup()
    {
        CommandStack.Clear();
        FakeCommand.ExecCount = 0;
        FakeCommand.UndoCount = 0;
    }

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
    }

    [Test]
    public void Execute_RunsCommand_EnablesUndo_ClearsRedo()
    {
        Assert.IsFalse(CommandStack.CanUndo);
        CommandStack.Execute(new FakeCommand());
        Assert.AreEqual(1, FakeCommand.ExecCount);
        Assert.IsTrue(CommandStack.CanUndo);
        Assert.IsFalse(CommandStack.CanRedo);
        Assert.AreEqual("Fake", CommandStack.PeekUndoDescription());
    }

    [Test]
    public void Undo_RevertsCommand_EnablesRedo()
    {
        CommandStack.Execute(new FakeCommand());
        CommandStack.Undo();
        Assert.AreEqual(1, FakeCommand.UndoCount);
        Assert.IsFalse(CommandStack.CanUndo);
        Assert.IsTrue(CommandStack.CanRedo);
    }

    [Test]
    public void Redo_ReappliesCommand()
    {
        CommandStack.Execute(new FakeCommand());
        CommandStack.Undo();
        CommandStack.Redo();
        Assert.AreEqual(2, FakeCommand.ExecCount); // первичный + redo
        Assert.IsTrue(CommandStack.CanUndo);
        Assert.IsFalse(CommandStack.CanRedo);
    }

    [Test]
    public void Execute_AfterUndo_ClearsRedoStack()
    {
        CommandStack.Execute(new FakeCommand());
        CommandStack.Undo();
        Assert.IsTrue(CommandStack.CanRedo);
        CommandStack.Execute(new FakeCommand());
        Assert.IsFalse(CommandStack.CanRedo, "новая команда сбрасывает redo");
    }

    [Test]
    public void Execute_BeyondMaxUndo_TrimsOldest()
    {
        for (int i = 0; i < 25; i++) CommandStack.Execute(new FakeCommand());
        int undone = 0;
        while (CommandStack.CanUndo) { CommandStack.Undo(); undone++; }
        Assert.AreEqual(20, undone, "стек ограничен 20 командами");
    }

    [Test]
    public void Clear_ResetsBothStacks()
    {
        CommandStack.Execute(new FakeCommand());
        CommandStack.Undo();
        CommandStack.Clear();
        Assert.IsFalse(CommandStack.CanUndo);
        Assert.IsFalse(CommandStack.CanRedo);
        Assert.AreEqual("", CommandStack.PeekUndoDescription());
    }

    [Test]
    public void Undo_Redo_EmptyStacks_NoThrow()
    {
        Assert.DoesNotThrow(() => CommandStack.Undo());
        Assert.DoesNotThrow(() => CommandStack.Redo());
    }

    [Test]
    public void MoveCommand_ExecuteUndo_MovesAndReverts()
    {
        var e = Make("M", Vector3.zero);
        var before = new Vector3(0, 0, 0);
        var after = new Vector3(1, 0, 2);
        var cmd = new MoveCommand(e, before, after, Quaternion.identity, Quaternion.Euler(0, 90, 0));
        cmd.Execute();
        Assert.AreEqual(after, e.transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 90, 0).eulerAngles.y, e.transform.rotation.eulerAngles.y, 0.01f);
        cmd.Undo();
        Assert.AreEqual(before, e.transform.position);
    }

    [Test]
    public void CreateCommand_Undo_DeactivatesAndUnregisters()
    {
        var e = Make("C", Vector3.zero);
        var cmd = new CreateCommand(e.gameObject);
        cmd.Execute();
        Assert.IsTrue(e.gameObject.activeSelf);
        Assert.IsTrue(PartRegistry.GetAll().Contains(e));
        cmd.Undo();
        Assert.IsFalse(e.gameObject.activeSelf);
        Assert.IsFalse(PartRegistry.GetAll().Contains(e));
    }

    [Test]
    public void DeleteCommand_Execute_Removes_Undo_Restores()
    {
        var e = Make("D", Vector3.zero);
        var cmd = new DeleteCommand(e.gameObject);
        cmd.Execute();
        Assert.IsFalse(e.gameObject.activeSelf);
        Assert.IsFalse(PartRegistry.GetAll().Contains(e));
        cmd.Undo();
        Assert.IsTrue(e.gameObject.activeSelf);
        Assert.IsTrue(PartRegistry.GetAll().Contains(e));
    }

    [Test]
    public void ResizeCommand_ExecuteUndo_ChangesDimensions()
    {
        var e = Make("R", Vector3.zero);
        var cmd = new ResizeCommand(e,
            new Vector3Int(800, 400, 18), new Vector3Int(600, 600, 18),
            Vector3.zero, new Vector3(0.5f, 0, 0),
            Quaternion.identity, Quaternion.identity);
        cmd.Execute();
        Assert.AreEqual(new Vector3Int(600, 600, 18), e.DimensionsMM);
        Assert.AreEqual(0.5f, e.transform.position.x, 0.0001f);
        cmd.Undo();
        Assert.AreEqual(new Vector3Int(800, 400, 18), e.DimensionsMM);
        Assert.AreEqual(0f, e.transform.position.x, 0.0001f);
    }

    [Test]
    public void CompositeCommand_ExecuteUndo_AffectsAllMembers()
    {
        var a = Make("A", Vector3.zero);
        var b = Make("B", new Vector3(1, 0, 0));
        var cmds = new List<IUndoCommand>
        {
            new MoveCommand(a, Vector3.zero, new Vector3(0, 1, 0), Quaternion.identity, Quaternion.identity),
            new MoveCommand(b, new Vector3(1, 0, 0), new Vector3(1, 1, 0), Quaternion.identity, Quaternion.identity),
        };
        var composite = new CompositeCommand("Move group", cmds);
        composite.Execute();
        Assert.AreEqual(1f, a.transform.position.y, 0.0001f);
        Assert.AreEqual(1f, b.transform.position.y, 0.0001f);
        composite.Undo();
        Assert.AreEqual(0f, a.transform.position.y, 0.0001f);
        Assert.AreEqual(0f, b.transform.position.y, 0.0001f);
    }
}
