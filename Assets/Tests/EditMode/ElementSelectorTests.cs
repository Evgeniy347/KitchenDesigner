using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;

public class ElementSelectorTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in PartRegistry.GetAll())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    private KitchenElement Make(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void NameGlob_MatchesPrefix()
    {
        Make("B4_side_L", new Vector3Int(540, 720, 18));
        Make("B4_bottom", new Vector3Int(564, 540, 16));
        Make("A2_door", new Vector3Int(600, 700, 18));

        var r = ElementSelector.Match("B4_*");
        Assert.AreEqual(2, r.Count);
        Assert.IsTrue(r.TrueForAll(e => e.PartName.StartsWith("B4_")));
    }

    [Test]
    public void NameSubstring_WhenNoWildcard()
    {
        Make("B4_side_L", new Vector3Int(540, 720, 18));
        Make("A2_door", new Vector3Int(600, 700, 18));

        var r = ElementSelector.Match("door");
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual("A2_door", r[0].PartName);
    }

    [Test]
    public void ThicknessCompare_MatchesDimZ()
    {
        Make("a", new Vector3Int(600, 400, 18));
        Make("b", new Vector3Int(600, 400, 16));
        Make("c", new Vector3Int(600, 400, 18));

        Assert.AreEqual(2, ElementSelector.Match("thickness==18").Count);
        Assert.AreEqual(1, ElementSelector.Match("thickness==16").Count);
        Assert.AreEqual(3, ElementSelector.Match("thickness<=18").Count);
        Assert.AreEqual(1, ElementSelector.Match("thickness<18").Count);
    }

    [Test]
    public void TypeBoard_And_TypeFloor_And_BasePlateExcluded()
    {
        Make("board1", new Vector3Int(600, 400, 18));

        var floorGo = new GameObject("floor1");
        var floor = floorGo.AddComponent<FloorElement>();
        floor.PartName = "floor1";
        floor.DimensionsMM = new Vector3Int(3000, 18, 3000);
        PartRegistry.Register(floor);
        _spawned.Add(floorGo);

        var plate = BasePlate.Create();
        PartRegistry.Register(plate.Element);
        _spawned.Add(plate.gameObject);

        var boards = ElementSelector.Match("type:board");
        Assert.AreEqual(1, boards.Count);
        Assert.AreEqual("board1", boards[0].PartName);

        Assert.AreEqual(1, ElementSelector.Match("type:floor").Count);

        // BasePlate (якорь) не попадает ни в одну выборку.
        var all = ElementSelector.Match("*");
        Assert.IsFalse(all.Exists(e => e.GetComponent<BasePlate>() != null));
        Assert.AreEqual(2, all.Count, "board1 + floor1 (без BasePlate)");
    }

    [Test]
    public void Module_ByGroupName()
    {
        var a = Make("B4_side_L", new Vector3Int(540, 720, 18));
        var b = Make("B4_bottom", new Vector3Int(564, 540, 16));
        Make("A2_door", new Vector3Int(600, 700, 18));

        var g = GroupManager.Create("B4");
        GroupManager.AddTo(g, a);
        GroupManager.AddTo(g, b);

        var r = ElementSelector.Match("module:B4");
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual(3, ElementSelector.Match("*").Count);
        Assert.AreEqual(2, ElementSelector.Match("all_modules").Count);
    }

    [Test]
    public void AndCombination_NameAndThickness()
    {
        Make("B4_side_L", new Vector3Int(540, 720, 18));
        Make("B4_bottom", new Vector3Int(564, 540, 16));

        var r = ElementSelector.Match("B4_* thickness==18");
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual("B4_side_L", r[0].PartName);
    }
}
