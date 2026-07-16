using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// IGroupService / GroupServiceInstance: пустые группы, перенос элементов между
/// группами, событие Changed, распространение подвижности, совместимость фасада
/// GroupManager.
/// </summary>
public class GroupServiceTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GroupServiceInstance? _svc;

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear(); // fallback-инстанс фасада — в исходное состояние
        _svc = new GroupServiceInstance();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    private KitchenElement MakeElement(string name)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void Create_EmptyGroup_HasNameAndNoMembers()
    {
        var g = _svc!.Create("Модуль А3");

        Assert.AreEqual("Модуль А3", g.name);
        Assert.IsEmpty(_svc.MembersOf(g));
        CollectionAssert.Contains(new List<LinkGroup>(_svc.AllGroups()), g);
    }

    [Test]
    public void AddTo_MovesElementIntoGroup_AndAppliesGroupMovability()
    {
        var g = _svc!.Create("G");
        _svc.SetMovable(g, false);
        var e = MakeElement("A");
        Assert.IsTrue(e.Movable);

        _svc.AddTo(g, e);

        Assert.AreEqual(g.id, e.GroupId);
        Assert.IsFalse(e.Movable, "подвижность группы распространяется на нового участника");
        Assert.AreEqual(1, _svc.MembersOf(g).Count);
    }

    [Test]
    public void MoveTo_TransfersElementBetweenGroups()
    {
        var g1 = _svc!.Create("G1");
        var g2 = _svc.Create("G2");
        var e = MakeElement("A");
        _svc.AddTo(g1, e);

        _svc.MoveTo(e, g2);

        Assert.AreEqual(g2.id, e.GroupId);
        Assert.IsEmpty(_svc.MembersOf(g1));
        Assert.AreEqual(1, _svc.MembersOf(g2).Count);
    }

    [Test]
    public void MoveTo_Null_RemovesFromGroup()
    {
        var g = _svc!.Create("G");
        var e = MakeElement("A");
        _svc.AddTo(g, e);

        _svc.MoveTo(e, null);

        Assert.AreEqual(0, e.GroupId);
        Assert.IsEmpty(_svc.MembersOf(g));
    }

    [Test]
    public void Changed_FiresOnEveryMutation()
    {
        int fired = 0;
        _svc!.Changed += () => fired++;

        var g = _svc.Create("G");           // 1
        var e = MakeElement("A");
        _svc.AddTo(g, e);                   // 2
        _svc.Rename(g, "G2");               // 3
        _svc.SetMovable(g, false);          // 4
        _svc.RemoveFrom(e);                 // 5
        _svc.Unlink(g);                     // 6

        Assert.AreEqual(6, fired);
    }

    [Test]
    public void Changed_DoesNotFire_OnNoOps()
    {
        var g = _svc!.Create("G");
        var e = MakeElement("A");
        _svc.AddTo(g, e);

        int fired = 0;
        _svc.Changed += () => fired++;

        _svc.AddTo(g, e);            // уже в группе
        _svc.RemoveFrom(MakeElement("B")); // не в группе
        _svc.Rename(g, "G");         // то же имя

        Assert.AreEqual(0, fired);
    }

    [Test]
    public void Unlink_ReleasesMembers()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b });

        _svc.Unlink(g!);

        Assert.AreEqual(0, a.GroupId);
        Assert.AreEqual(0, b.GroupId);
        Assert.IsEmpty(new List<LinkGroup>(_svc.AllGroups()));
    }

    [Test]
    public void Facade_GroupManager_DelegatesToInstance()
    {
        var g = GroupManager.Create("ViaFacade");
        var e = MakeElement("A");
        GroupManager.AddTo(g, e);

        Assert.AreEqual(g.id, e.GroupId);
        Assert.AreEqual(1, GroupManager.MembersOf(g).Count);
        Assert.AreSame(g, GroupManager.GroupOf(e));

        GroupManager.MoveTo(e, null);
        Assert.AreEqual(0, e.GroupId);
    }

    [Test]
    public void Register_RestoresGroup_AndKeepsIdCounterAhead()
    {
        var g = _svc!.Register(10, "Из сейва", movable: false);
        Assert.AreEqual(10, g.id);

        var next = _svc.Create("Новая");
        Assert.Greater(next.id, 10, "счётчик id должен уйти дальше восстановленных");
    }
}
