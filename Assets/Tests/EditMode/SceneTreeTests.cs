using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// SceneTree.Build — модель дерева панели «Сцена»: порядок строк, глубины,
/// сворачивание групп, вложение прикреплённого фасада под ящик, «мёртвые» GroupId.
/// </summary>
public class SceneTreeTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GroupServiceInstance _svc = new GroupServiceInstance();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        // SceneTree.Build не зависит от статики GroupManager: группы передаются
        // параметром, принадлежность читается из GroupId элементов.
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

    private DrawerElement MakeDrawer(string name)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name, Vector3.zero);
        _spawned.Add(go);
        var d = go.GetComponent<DrawerElement>();
        PartRegistry.Register(d);
        return d;
    }

    private FacadeElement MakeFacade(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = new Vector3Int(400, 300, 18);
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private List<SceneTree.Node> Build(ISet<int>? collapsed = null) =>
        SceneTree.Build(PartRegistry.GetAll(), _svc.AllGroups(), collapsed);

    [Test]
    public void Build_EmptyScene_HasOnlyRoot()
    {
        var nodes = Build();
        Assert.AreEqual(1, nodes.Count);
        Assert.IsTrue(nodes[0].isRoot);
        Assert.AreEqual(0, nodes[0].depth);
    }

    [Test]
    public void Build_GroupWithMembers_ThenUngrouped()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        MakeElement("Free");
        var g = _svc.Link(new List<KitchenElement> { a, b })!;
        _svc.Rename(g, "Модуль");

        var nodes = Build();

        // Кухня → группа → A → B → Free
        Assert.AreEqual(5, nodes.Count);
        Assert.IsTrue(nodes[0].isRoot);
        Assert.AreSame(g, nodes[1].group);
        Assert.AreEqual(1, nodes[1].depth);
        Assert.AreEqual("A", nodes[2].element!.PartName);
        Assert.AreEqual(2, nodes[2].depth);
        Assert.AreEqual("B", nodes[3].element!.PartName);
        Assert.AreEqual("Free", nodes[4].element!.PartName);
        Assert.AreEqual(1, nodes[4].depth, "внегрупповой элемент — под корнем");
    }

    [Test]
    public void Build_CollapsedGroup_HidesMembers()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc.Link(new List<KitchenElement> { a, b })!;

        var nodes = Build(new HashSet<int> { g.id });

        Assert.AreEqual(2, nodes.Count, "члены свёрнутой группы не выводятся");
        Assert.IsTrue(nodes[1].collapsed);
        Assert.IsTrue(nodes[1].hasChildren, "стрелка разворачивания остаётся");
    }

    [Test]
    public void Build_AttachedFacade_NestsUnderDrawer_NotAtTopLevel()
    {
        var drawer = MakeDrawer("Ящик");
        var facade = MakeFacade("Фасад");
        drawer.AttachedFacadeName = "Фасад";

        var nodes = Build();

        // Кухня → Ящик → Фасад (фасад НЕ дублируется на верхнем уровне)
        Assert.AreEqual(3, nodes.Count);
        Assert.AreSame(drawer, nodes[1].element);
        Assert.AreEqual(1, nodes[1].depth);
        Assert.IsTrue(nodes[1].hasChildren);
        Assert.AreSame(facade, nodes[2].element);
        Assert.AreEqual(2, nodes[2].depth, "фасад — ребёнок ящика");
    }

    [Test]
    public void Build_DrawerInGroup_FacadeGoesDeeper()
    {
        var drawer = MakeDrawer("Ящик");
        var facade = MakeFacade("Фасад");
        var side = MakeElement("Боковина");
        drawer.AttachedFacadeName = "Фасад";
        var g = _svc.Link(new List<KitchenElement> { drawer, side })!;

        var nodes = Build();

        // Кухня → группа → Боковина(2) → Ящик(2) → Фасад(3)
        Assert.AreEqual(5, nodes.Count);
        Assert.AreSame(g, nodes[1].group);
        int drawerIdx = nodes.FindIndex(n => ReferenceEquals(n.element, drawer));
        Assert.AreEqual(2, nodes[drawerIdx].depth);
        Assert.AreSame(facade, nodes[drawerIdx + 1].element);
        Assert.AreEqual(3, nodes[drawerIdx + 1].depth);
    }

    [Test]
    public void Build_DeadGroupId_ElementShownAsUngrouped()
    {
        var e = MakeElement("Orphan");
        e.GroupId = 999; // группы с таким id не существует

        var nodes = Build();

        Assert.AreEqual(2, nodes.Count);
        Assert.AreSame(e, nodes[1].element);
        Assert.AreEqual(1, nodes[1].depth);
    }

    [Test]
    public void Build_EmptyGroup_ShownWithoutChildrenFlag()
    {
        _svc.Create("Пустая");

        var nodes = Build();

        Assert.AreEqual(2, nodes.Count);
        Assert.IsNotNull(nodes[1].group);
        Assert.IsFalse(nodes[1].hasChildren);
    }

    [Test]
    public void Build_MembersSortedByName()
    {
        var b = MakeElement("Б_полка");
        var a = MakeElement("А_полка");
        _svc.Link(new List<KitchenElement> { b, a });

        var nodes = Build();

        Assert.AreEqual("А_полка", nodes[2].element!.PartName);
        Assert.AreEqual("Б_полка", nodes[3].element!.PartName);
    }
}
