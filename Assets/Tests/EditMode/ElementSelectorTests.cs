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

    private T MakeTyped<T>(string name) where T : KitchenElement
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var e = go.AddComponent<T>();
        e.PartName = name;
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void EmptySelector_MatchesEverything()
    {
        Make("a", new Vector3Int(600, 400, 18));
        Make("b", new Vector3Int(600, 400, 16));

        Assert.AreEqual(2, ElementSelector.Match(null).Count, "ни одной клаузы — ни одного ограничения");
        Assert.AreEqual(2, ElementSelector.Match("").Count);
        Assert.AreEqual(2, ElementSelector.Match("   ").Count);
    }

    [Test]
    public void Star_All_AndAllElements_AreOneAndTheSameUnrestrictedSelector()
    {
        Make("a", new Vector3Int(600, 400, 18));
        Make("b", new Vector3Int(600, 400, 16));

        Assert.AreEqual(2, ElementSelector.Match("*").Count);
        Assert.AreEqual(2, ElementSelector.Match("all").Count);
        Assert.AreEqual(2, ElementSelector.Match("ALL_ELEMENTS").Count, "токены разбираются регистронезависимо");
    }

    [Test]
    public void AllBoards_TakesOnlyPlainBoards()
    {
        Make("board1", new Vector3Int(600, 400, 18));
        MakeTypedAndRegister<FloorElement>("floor1");

        var boards = ElementSelector.Match("all_boards");
        Assert.AreEqual(1, boards.Count, "all_boards — синоним type:board, пол в него не входит");
        Assert.AreEqual("board1", boards[0].PartName);
    }

    [Test]
    public void Group_IsAnAliasOfModule()
    {
        var a = Make("B4_side_L", new Vector3Int(540, 720, 18));
        Make("A2_door", new Vector3Int(600, 700, 18));
        GroupManager.AddTo(GroupManager.Create("B4"), a);

        Assert.AreEqual(1, ElementSelector.Match("group:B4").Count,
            "group: и module: — одна и та же клауза; агенты пишут и так, и так");
        Assert.AreEqual(1, ElementSelector.Match("module:B4").Count);
    }

    [Test]
    public void BareToken_AndNamePrefix_MeanTheSameThing()
    {
        Make("B4_side_L", new Vector3Int(540, 720, 18));
        Make("A2_door", new Vector3Int(600, 700, 18));

        Assert.AreEqual(1, ElementSelector.Match("B4_*").Count, "голый токен — это имя");
        Assert.AreEqual(1, ElementSelector.Match("name:B4_*").Count);
    }

    [Test]
    public void EveryComparisonOperator_IsUnderstood()
    {
        Make("a", new Vector3Int(500, 400, 18));
        Make("b", new Vector3Int(600, 400, 18));
        Make("c", new Vector3Int(700, 400, 18));

        Assert.AreEqual(1, ElementSelector.Match("width==600").Count);
        Assert.AreEqual(1, ElementSelector.Match("width=600").Count, "одиночное = читается как ==");
        Assert.AreEqual(2, ElementSelector.Match("width!=600").Count);
        Assert.AreEqual(2, ElementSelector.Match("width>=600").Count);
        Assert.AreEqual(2, ElementSelector.Match("width<=600").Count);
        Assert.AreEqual(1, ElementSelector.Match("width>600").Count);
        Assert.AreEqual(1, ElementSelector.Match("width<600").Count);
        Assert.AreEqual(3, ElementSelector.Match("height==400").Count);
    }

    [Test]
    public void ThicknessAndDepth_AreTheSameDimension()
    {
        Make("a", new Vector3Int(600, 400, 18));
        Make("b", new Vector3Int(600, 400, 16));

        Assert.AreEqual(ElementSelector.Match("depth==18").Count,
            ElementSelector.Match("thickness==18").Count,
            "толщина детали — это dimZ: селектор обязан отвечать на оба слова одинаково");
    }

    [Test]
    public void InactiveElement_IsNotSelected()
    {
        var a = Make("a", new Vector3Int(600, 400, 18));
        Make("b", new Vector3Int(600, 400, 18));
        a.gameObject.SetActive(false);

        var r = ElementSelector.Match("*");
        Assert.AreEqual(1, r.Count, "выключенный объект — не содержимое сцены, массовая правка его не трогает");
        Assert.AreEqual("b", r[0].PartName);
    }

    [Test]
    public void TypeOf_SubclassAnswersBeforeItsBase()
    {
        Assert.AreEqual("assembled_facade", ElementSelector.TypeOf(MakeTyped<AssembledFacadeElement>("af")),
            "сборный фасад — наследник FacadeElement: проверь базу раньше, и он станет обычным facade");
        Assert.AreEqual("facade", ElementSelector.TypeOf(MakeTyped<FacadeElement>("f")));
    }

    private T MakeTypedAndRegister<T>(string name) where T : KitchenElement
    {
        var e = MakeTyped<T>(name);
        PartRegistry.Register(e);
        return e;
    }
}
