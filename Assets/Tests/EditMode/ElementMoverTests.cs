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
        a.transform.position = new Vector3(9, 9, 9); // «утащили»
        var members = new List<KitchenElement> { a };
        var starts = new List<Vector3> { new Vector3(5, 1, 2) };

        ElementMover.ApplyDelta(members, starts, Vector3.zero);

        Assert.AreEqual(new Vector3(5, 1, 2), a.transform.position);
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
}
