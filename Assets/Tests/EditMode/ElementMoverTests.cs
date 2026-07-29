using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ElementMoverTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        _spawned.Add(go);
        return e;
    }

    private KitchenElement MakeWithDims(Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void ApplyDelta_MovesAllMembersByDelta()
    {
        var a = Make(new Vector3(0, 0, 0));
        var b = Make(new Vector3(1, 0, 0));
        var members = new List<KitchenElement> { a, b };
        var starts = new List<Vector3> { a.transform.position, b.transform.position };

        ElementMover.ApplyDelta(members, starts, new Vector3(2, 0, 3));

        Assert.AreEqual(new Vector3(2, 0, 3), a.transform.position);
        Assert.AreEqual(new Vector3(3, 0, 3), b.transform.position);
    }

    [Test]
    public void ApplyDelta_ZeroDelta_RestoresStarts()
    {
        var a = Make(new Vector3(5, 1, 2));
        a.transform.position = new Vector3(9, 9, 9);
        var members = new List<KitchenElement> { a };
        var starts = new List<Vector3> { new Vector3(5, 1, 2) };

        ElementMover.ApplyDelta(members, starts, Vector3.zero);

        Assert.AreEqual(new Vector3(5, 1, 2), a.transform.position);
    }

    [Test]
    public void ApplyDelta_NullMemberInList_SkipsNull()
    {
        var a = Make(new Vector3(3, 0, 0));
        var members = new List<KitchenElement> { a, null! };
        var starts = new List<Vector3> { new Vector3(3, 0, 0), new Vector3(5, 0, 0) };

        ElementMover.ApplyDelta(members, starts, new Vector3(1, 0, 0));

        Assert.AreEqual(new Vector3(4, 0, 0), a.transform.position);
    }

    [Test]
    public void ApplyDelta_EmptyLists_DoesNotCrash()
    {
        var members = new List<KitchenElement>();
        var starts = new List<Vector3>();

        Assert.DoesNotThrow(() => ElementMover.ApplyDelta(members, starts, new Vector3(10, 10, 10)));
    }

    [Test]
    public void ApplyDelta_LargeDelta_AppliesCorrectly()
    {
        var a = Make(new Vector3(0, 0, 0));
        var members = new List<KitchenElement> { a };
        var starts = new List<Vector3> { a.transform.position };

        ElementMover.ApplyDelta(members, starts, new Vector3(50, -30, 20));

        Assert.AreEqual(new Vector3(50, -30, 20), a.transform.position);
    }

    [Test]
    public void ApplyDelta_NegativeDelta_AppliesCorrectly()
    {
        var a = Make(new Vector3(5, 5, 5));
        var b = Make(new Vector3(1, 1, 1));
        var members = new List<KitchenElement> { a, b };
        var starts = new List<Vector3> { a.transform.position, b.transform.position };

        ElementMover.ApplyDelta(members, starts, new Vector3(-3, -2, -1));

        Assert.AreEqual(new Vector3(2, 3, 4), a.transform.position);
        Assert.AreEqual(new Vector3(-2, -1, 0), b.transform.position);
    }

    [Test]
    public void ApplyDelta_SingleMember_AppliesCorrectly()
    {
        var a = Make(new Vector3(1, 2, 3));
        var members = new List<KitchenElement> { a };
        var starts = new List<Vector3> { new Vector3(1, 2, 3) };

        ElementMover.ApplyDelta(members, starts, new Vector3(0.5f, 0, 0));

        Assert.AreEqual(new Vector3(1.5f, 2, 3), a.transform.position);
    }

    [Test]
    public void GrabStart_NullElement_ReturnsZero()
    {
        var result = ElementMover.GrabStart(null!);

        Assert.AreEqual(Vector3.zero, result);
    }

    [Test]
    public void GrabStart_NormalElement_ReturnsTransformPosition()
    {
        var e = Make(new Vector3(3, 4, 5));

        var result = ElementMover.GrabStart(e);

        Assert.AreEqual(new Vector3(3, 4, 5), result);
    }

    [Test]
    public void GrabStart_ElementAfterMove_ReturnsCurrentPosition()
    {
        var e = Make(new Vector3(0, 0, 0));
        e.transform.position = new Vector3(7, 8, 9);

        var result = ElementMover.GrabStart(e);

        Assert.AreEqual(new Vector3(7, 8, 9), result);
    }

    [Test]
    public void GrabStart_WallElementLowered_RestoresFullAndReturnsFullPosition()
    {
        var go = new GameObject("Wall");
        go.transform.position = new Vector3(1, 2, 3);
        go.transform.localScale = new Vector3(1, 5, 1);
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = new Vector3Int(100, 2500, 3000);
        var wall = go.AddComponent<Wall>();
        _spawned.Add(go);

        wall.SetLowered(true, 0.5f);
        Assert.IsTrue(wall.IsLowered);

        var result = ElementMover.GrabStart(e);

        Assert.IsFalse(wall.IsLowered);
        Assert.AreEqual(2f, result.y, 0.001f);
        Assert.AreEqual(2f, go.transform.position.y, 0.001f);
    }

    [Test]
    public void GrabStart_WallElementNotLowered_ReturnsCurrentPosition()
    {
        var go = new GameObject("Wall");
        go.transform.position = new Vector3(1, 2, 3);
        go.transform.localScale = new Vector3(1, 5, 1);
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = new Vector3Int(100, 2500, 3000);
        go.AddComponent<Wall>();
        _spawned.Add(go);

        var result = ElementMover.GrabStart(e);

        Assert.AreEqual(new Vector3(1, 2, 3), result);
    }

    [Test]
    public void MoveCommand_Execute_MovesToTargetPosition()
    {
        var e = Make(new Vector3(0, 0, 0));
        var cmd = new MoveCommand(e, Vector3.zero, new Vector3(5, 1, 2), Quaternion.identity, Quaternion.identity);

        cmd.Execute();

        Assert.AreEqual(new Vector3(5, 1, 2), e.transform.position);
    }

    [Test]
    public void MoveCommand_Undo_RestoresPositionAndRotation()
    {
        var e = Make(new Vector3(0, 0, 0));
        var startRot = Quaternion.Euler(0, 0, 0);
        var movedRot = Quaternion.Euler(0, 90, 0);
        var cmd = new MoveCommand(e, Vector3.zero, new Vector3(2, 0, 0), startRot, movedRot);

        cmd.Execute();
        Assert.AreEqual(new Vector3(2, 0, 0), e.transform.position);
        Assert.AreEqual(movedRot.eulerAngles, e.transform.rotation.eulerAngles);

        cmd.Undo();
        Assert.AreEqual(Vector3.zero, e.transform.position);
        Assert.AreEqual(startRot.eulerAngles, e.transform.rotation.eulerAngles);
    }

    [Test]
    public void MoveCommand_ExecuteUndoReexecute_CycleConsistent()
    {
        var e = Make(new Vector3(1, 2, 3));
        var before = e.transform.position;
        var after = new Vector3(10, 20, 30);
        var cmd = new MoveCommand(e, before, after, Quaternion.identity, Quaternion.identity);

        cmd.Execute();
        Assert.AreEqual(after, e.transform.position);

        cmd.Undo();
        Assert.AreEqual(before, e.transform.position);

        cmd.Execute();
        Assert.AreEqual(after, e.transform.position);
    }

    [Test]
    public void MoveCommand_NullElement_DoesNotThrowOnExecute()
    {
        var cmd = new MoveCommand(null!, Vector3.zero, Vector3.one, Quaternion.identity, Quaternion.identity);

        Assert.DoesNotThrow(() => cmd.Execute());
    }

    [Test]
    public void MoveCommand_NullElement_DoesNotThrowOnUndo()
    {
        var cmd = new MoveCommand(null!, Vector3.zero, Vector3.one, Quaternion.identity, Quaternion.identity);

        Assert.DoesNotThrow(() => cmd.Undo());
    }

    [Test]
    public void CompositeCommand_UndoRestoresAllMembers()
    {
        var a = Make(new Vector3(0, 0, 0));
        var b = Make(new Vector3(1, 0, 0));
        var cmd = new CompositeCommand("test", new List<IUndoCommand>
        {
            new MoveCommand(a, a.transform.position, new Vector3(2, 0, 0), Quaternion.identity, Quaternion.identity),
            new MoveCommand(b, b.transform.position, new Vector3(3, 0, 0), Quaternion.identity, Quaternion.identity),
        });

        cmd.Execute();
        Assert.AreEqual(new Vector3(2, 0, 0), a.transform.position);
        Assert.AreEqual(new Vector3(3, 0, 0), b.transform.position);

        cmd.Undo();
        Assert.AreEqual(new Vector3(0, 0, 0), a.transform.position);
        Assert.AreEqual(new Vector3(1, 0, 0), b.transform.position);
    }

    [Test]
    public void CompositeCommand_SingleCommand_WorksLikeMove()
    {
        var e = Make(new Vector3(4, 5, 6));
        var cmd = new CompositeCommand("single", new List<IUndoCommand>
        {
            new MoveCommand(e, new Vector3(4, 5, 6), new Vector3(7, 8, 9), Quaternion.identity, Quaternion.identity),
        });

        cmd.Execute();
        Assert.AreEqual(new Vector3(7, 8, 9), e.transform.position);

        cmd.Undo();
        Assert.AreEqual(new Vector3(4, 5, 6), e.transform.position);
    }

    [Test]
    public void CompositeCommand_EmptyList_ExecuteDoesNotCrash()
    {
        var cmd = new CompositeCommand("empty", new List<IUndoCommand>());

        Assert.DoesNotThrow(() => cmd.Execute());
    }

    [Test]
    public void CompositeCommand_EmptyList_UndoDoesNotCrash()
    {
        var cmd = new CompositeCommand("empty", new List<IUndoCommand>());

        Assert.DoesNotThrow(() => cmd.Undo());
    }

    [Test]
    public void CompositeCommand_ThreeCommands_UndoRestoresAllInReverse()
    {
        var a = Make(new Vector3(0, 0, 0));
        var b = Make(new Vector3(10, 0, 0));
        var c = Make(new Vector3(20, 0, 0));
        var cmd = new CompositeCommand("three", new List<IUndoCommand>
        {
            new MoveCommand(a, new Vector3(0, 0, 0), new Vector3(0, 1, 0), Quaternion.identity, Quaternion.identity),
            new MoveCommand(b, new Vector3(10, 0, 0), new Vector3(10, 2, 0), Quaternion.identity, Quaternion.identity),
            new MoveCommand(c, new Vector3(20, 0, 0), new Vector3(20, 3, 0), Quaternion.identity, Quaternion.identity),
        });

        cmd.Execute();
        Assert.AreEqual(new Vector3(0, 1, 0), a.transform.position);
        Assert.AreEqual(new Vector3(10, 2, 0), b.transform.position);
        Assert.AreEqual(new Vector3(20, 3, 0), c.transform.position);

        cmd.Undo();
        Assert.AreEqual(new Vector3(0, 0, 0), a.transform.position);
        Assert.AreEqual(new Vector3(10, 0, 0), b.transform.position);
        Assert.AreEqual(new Vector3(20, 0, 0), c.transform.position);
    }

    [Test]
    public void CompositeCommand_MixedMoveResize_UndoRestoresDimensionsAndPosition()
    {
        var a = MakeWithDims(new Vector3(0, 0, 0), new Vector3Int(800, 400, 18));
        var cmd = new CompositeCommand("mixed", new List<IUndoCommand>
        {
            new MoveCommand(a, Vector3.zero, new Vector3(2, 0, 0), Quaternion.identity, Quaternion.identity),
            new ResizeCommand(a, new Vector3Int(800, 400, 18), new Vector3Int(600, 300, 18),
                Vector3.zero, new Vector3(2, 0, 0), Quaternion.identity, Quaternion.identity),
        });

        cmd.Execute();
        Assert.AreEqual(new Vector3(2, 0, 0), a.transform.position);
        Assert.AreEqual(new Vector3Int(600, 300, 18), a.DimensionsMM);

        cmd.Undo();
        Assert.AreEqual(Vector3.zero, a.transform.position);
        Assert.AreEqual(new Vector3Int(800, 400, 18), a.DimensionsMM);
    }

    [Test]
    public void IsDragging_Initially_False()
    {
        Assert.IsFalse(ElementMover.IsDragging);
    }

    [Test]
    public void IsMoving_NullElement_ReturnsFalse()
    {
        Assert.IsFalse(ElementMover.IsMoving(null!));
    }

    [Test]
    public void IsMoving_Element_InitiallyReturnsFalse()
    {
        var e = Make(new Vector3(0, 0, 0));

        Assert.IsFalse(ElementMover.IsMoving(e));
    }
}
