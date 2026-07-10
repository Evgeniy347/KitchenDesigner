using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class GroupTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // В EditMode Awake у AddComponent не вызывается, поэтому регистрируем элемент
    // в BoardRegistry вручную — в рантайме это делает KitchenElement.Awake.
    private KitchenElement Make(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        BoardRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        GroupManager.Clear();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            BoardRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    [Test]
    public void Link_AssignsSharedGroupId()
    {
        var a = Make("A", Vector3.zero);
        var b = Make("B", new Vector3(1, 0, 0));

        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        Assert.IsNotNull(g);
        Assert.AreNotEqual(0, g.id);
        Assert.AreEqual(g.id, a.GroupId);
        Assert.AreEqual(g.id, b.GroupId);
        Assert.AreSame(g, GroupManager.GroupOf(a));
        Assert.AreSame(g, GroupManager.GroupOf(b));
    }

    [Test]
    public void Link_SingleElement_ReturnsNull()
    {
        var a = Make("A", Vector3.zero);

        var g = GroupManager.Link(new List<KitchenElement> { a });

        Assert.IsNull(g, "группа из одного элемента не создаётся");
        Assert.AreEqual(0, a.GroupId);
    }

    [Test]
    public void Unlink_ClearsGroupId()
    {
        var a = Make("A", Vector3.zero);
        var b = Make("B", new Vector3(1, 0, 0));
        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        GroupManager.Unlink(g);

        Assert.AreEqual(0, a.GroupId);
        Assert.AreEqual(0, b.GroupId);
        Assert.IsNull(GroupManager.GroupOf(a));
    }

    [Test]
    public void MembersOf_ReturnsAllLinked()
    {
        var a = Make("A", Vector3.zero);
        var b = Make("B", new Vector3(1, 0, 0));
        Make("C_unlinked", new Vector3(2, 0, 0));
        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        var members = GroupManager.MembersOf(g);

        Assert.AreEqual(2, members.Count);
        Assert.Contains(a, members);
        Assert.Contains(b, members);
    }

    [Test]
    public void SetMovable_PropagatesToMembers()
    {
        var a = Make("A", Vector3.zero);
        var b = Make("B", new Vector3(1, 0, 0));
        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        GroupManager.SetMovable(g, false);

        Assert.IsFalse(g.movable);
        Assert.IsFalse(a.Movable);
        Assert.IsFalse(b.Movable);

        GroupManager.SetMovable(g, true);
        Assert.IsTrue(a.Movable);
        Assert.IsTrue(b.Movable);
    }

    [Test]
    public void Groups_SurviveSerializeRestore()
    {
        var a = Make("A", Vector3.zero);
        var b = Make("B", new Vector3(0.8f, 0, 0));
        var g = GroupManager.Link(new List<KitchenElement> { a, b });
        g.name = "Шкаф";
        GroupManager.SetMovable(g, false);
        int originalId = g.id;

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(BoardRegistry.GetAll()));

        // Чистим сцену и реестр групп — как при загрузке проекта.
        GroupManager.Clear();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        var restored = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json));
        _spawned.AddRange(restored);
        // Регистрируем восстановленные элементы (в рантайме это делает Awake).
        foreach (var go in restored) BoardRegistry.Register(go.GetComponent<KitchenElement>());

        var ra = restored[0].GetComponent<KitchenElement>();
        var rg = GroupManager.GroupOf(ra);

        Assert.IsNotNull(rg, "группа восстановлена");
        Assert.AreEqual(originalId, rg.id);
        Assert.AreEqual("Шкаф", rg.name);
        Assert.IsFalse(rg.movable);
        Assert.AreEqual(2, GroupManager.MembersOf(rg).Count);
    }
}
