using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

public class McpBulkOpsTests
{
    private McpCommandHandler? _handler;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ProjectRooms.Reset();
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
        CommandStack.Clear();
        ProjectRooms.Reset();
    }

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest { id = "t", method = method, Params = Newtonsoft.Json.Linq.JObject.Parse(json) };
    }

    private KitchenElement Make(string name, Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        e.Movable = true;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void SetAttr_Thickness_ChangesAllMatched()
    {
        var b1 = Make("b1", Vector3.zero, new Vector3Int(600, 400, 18));
        var b2 = Make("b2", Vector3.zero, new Vector3Int(600, 400, 18));
        var b3 = Make("b3", Vector3.zero, new Vector3Int(600, 400, 16));

        var resp = _handler!.Handle(MakeReq("set_attr", new { selector = "all_boards thickness==18", thickness = 16 }));
        Assert.AreEqual("result", resp.type);

        Assert.AreEqual(16, b1.DimensionsMM.z);
        Assert.AreEqual(16, b2.DimensionsMM.z);
        Assert.AreEqual(16, b3.DimensionsMM.z, "уже был 16 — без изменений");
    }

    [Test]
    public void SetAttr_GeometryMaterialAndLockUndoTogether()
    {
        var board = Make("board", Vector3.zero, new Vector3Int(600, 400, 18));
        var response = _handler!.Handle(MakeReq("set_attr", new
        { selector = "board", thickness = 16, material = "concrete", locked = true }));
        Assert.AreEqual("result", response.type);
        Assert.AreEqual(16, board.DimensionsMM.z);
        Assert.AreEqual("concrete", board.MaterialId);
        Assert.IsFalse(board.Movable);
        CommandStack.Undo();
        Assert.AreEqual(18, board.DimensionsMM.z);
        Assert.AreEqual(MaterialCatalog.DefaultId, board.MaterialId);
        Assert.IsTrue(board.Movable);
    }

    [Test]
    public void Move_ShiftsSelectionByMillimetres()
    {
        var b = Make("shelf", new Vector3(0f, 0.2f, 0f), new Vector3Int(600, 400, 18));

        var resp = _handler!.Handle(MakeReq("move", new { selector = "shelf", dx = 100f }));
        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(0.1f, b.transform.position.x, 0.001f);
    }

    [Test]
    public void Move_CarriesAnAttachedPartAlong_TheSameWayEditElementsDoes()
    {
        Make("Host", Vector3.zero, new Vector3Int(600, 400, 18));
        var child = Make("Child", new Vector3(0f, 0.3f, 0f), new Vector3Int(100, 100, 18));

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Child", attached_to_name = "Host" } }
        }));
        var resp = _handler.Handle(MakeReq("move", new { selector = "Host", dx = 100f }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(0.1f, child.transform.position.x, 0.001f,
            "массовый move обязан тащить прикреплённые детали так же, как edit_elements: "
            + "иначе один и тот же перенос через два инструмента даёт разную сцену, "
            + "и сборка разъезжается именно на батчевом пути");
    }

    [Test]
    public void ResizeModule_WidensGroup_ServerComputesBoards()
    {
        var sideL = Make("side_L", new Vector3(0f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var sideR = Make("side_R", new Vector3(0.582f, 0.36f, 0f), new Vector3Int(18, 720, 540));
        var bottom = Make("bottom", new Vector3(0.291f, 0.05f, 0f), new Vector3Int(564, 32, 540));

        var g = GroupManager.Create("Cab");
        GroupManager.AddTo(g, sideL);
        GroupManager.AddTo(g, sideR);
        GroupManager.AddTo(g, bottom);

        var resp = _handler!.Handle(MakeReq("resize_module", new { module = "Cab", axis = "x", delta_mm = 100f }));
        Assert.AreEqual("result", resp.type);

        Assert.AreEqual(0f, sideL.transform.position.x, 0.001f, "ближняя боковина на месте");
        Assert.AreEqual(0.682f, sideR.transform.position.x, 0.001f, "дальняя боковина +100мм");
        Assert.AreEqual(664, bottom.DimensionsMM.x, "дно растянуто +100мм");
        Assert.AreEqual(0.341f, bottom.transform.position.x, 0.001f, "центр дна +50мм");
    }

    [Test]
    public void GetSceneTree_IgnoresDeletedElements()
    {
        Make("kept", Vector3.zero, new Vector3Int(600, 400, 18));
        var gone = Make("gone", Vector3.zero, new Vector3Int(600, 400, 18));
        // Deletion deactivates instead of destroying, so undo can bring it back.
        gone.gameObject.SetActive(false);

        var resp = _handler!.Handle(MakeReq("get_scene_tree", new { }));
        var jo = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(resp.data));

        Assert.AreEqual(1, (int)jo["elementCount"]!, "удалённый элемент не считается");
        var loose = ((Newtonsoft.Json.Linq.JArray)jo["loose"]!).ToObject<List<string>>()!;
        CollectionAssert.DoesNotContain(loose, "gone");
        CollectionAssert.Contains(loose, "kept");
    }

    [Test]
    public void GetCompact_AnchorIsMinimumWorldCorner_EvenWhenRotated()
    {
        // A 900 mm board turned 90 deg: its world footprint runs along Z, so the
        // anchor must be the minimum Z corner regardless of which local corner
        // the rotation happens to send there.
        var e = Make("turned", new Vector3(1f, 0f, 2f), new Vector3Int(900, 1200, 100));
        e.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        var resp = _handler!.Handle(MakeReq("get", new { names = new[] { "turned" } }));
        Assert.AreEqual("result", resp.type);
        var jo = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(resp.data));
        var anchor = ((Newtonsoft.Json.Linq.JArray)jo["elements"]![0]!["anchorMm"]!).ToObject<List<int>>()!;

        Assert.AreEqual(950, anchor[0], 1, "x: центр 1000 минус половина толщины 50");
        Assert.AreEqual(1550, anchor[1], 1, "z: центр 2000 минус половина длины 450");
    }

    [Test]
    public void GetSceneTree_ReportsModulesLooseAndCounts()
    {
        var a = Make("B4_side_L", Vector3.zero, new Vector3Int(18, 720, 540));
        var b = Make("B4_bottom", Vector3.zero, new Vector3Int(564, 32, 540));
        Make("shelf", Vector3.zero, new Vector3Int(600, 400, 18));

        var g = GroupManager.Create("B4");
        GroupManager.AddTo(g, a);
        GroupManager.AddTo(g, b);

        var resp = _handler!.Handle(MakeReq("get_scene_tree", new { }));
        Assert.AreEqual("result", resp.type);
        var jo = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(resp.data));

        Assert.AreEqual(3, (int)jo["elementCount"]!);
        Assert.AreEqual(1, (int)jo["moduleCount"]!);
        var mod = jo["modules"]![0]!;
        Assert.AreEqual("B4", mod["name"]!.ToString());
        Assert.AreEqual(2, (int)mod["memberCount"]!);
        var loose = (Newtonsoft.Json.Linq.JArray)jo["loose"]!;
        Assert.IsTrue(loose.ToObject<List<string>>()!.Contains("shelf"));
    }

    [Test]
    public void Group_DeclaresExactMembershipWidthAxis_AndIsUndoable()
    {
        var a = Make("A", Vector3.zero, new Vector3Int(100, 100, 100));
        var b = Make("B", Vector3.zero, new Vector3Int(100, 100, 100));
        var c = Make("C", Vector3.zero, new Vector3Int(100, 100, 100));
        var first = _handler!.Handle(MakeReq("group", new
        { id = "Cab", names = new[] { "A", "B" }, width_axis = "z" }));
        Assert.AreEqual("result", first.type);
        var g = GroupManager.GroupOf(a)!;
        Assert.AreEqual("z", g.widthAxis);
        Assert.AreEqual(g.id, b.GroupId);

        _handler.Handle(MakeReq("group", new
        { id = "Cab", names = new[] { "B", "C" }, width_axis = "x" }));
        Assert.AreEqual(0, a.GroupId, "declaration replaces membership");
        Assert.AreEqual(g.id, c.GroupId);
        Assert.AreEqual("x", g.widthAxis);
        Assert.AreEqual(1, new List<LinkGroup>(GroupManager.AllGroups()).Count);

        CommandStack.Undo();
        Assert.AreEqual(g.id, a.GroupId);
        Assert.AreEqual(0, c.GroupId);
        Assert.AreEqual("z", g.widthAxis);
    }

    [Test]
    public void Group_NewDeclarationUndoRemovesGroupAndRestoresPriorMembership()
    {
        var a = Make("A", Vector3.zero, new Vector3Int(100, 100, 100));
        var old = GroupManager.Create("Old"); GroupManager.AddTo(old, a);
        _handler!.Handle(MakeReq("group", new
        { id = "New", names = new[] { "A" }, width_axis = "y" }));
        Assert.AreEqual("New", GroupManager.GroupOf(a)!.name);
        CommandStack.Undo();
        Assert.AreEqual("Old", GroupManager.GroupOf(a)!.name);
        Assert.AreEqual(1, new List<LinkGroup>(GroupManager.AllGroups()).Count);
    }

    [Test]
    public void ResizeModule_OmittedAxisUsesStoredWidthAxis()
    {
        var near = Make("near", new Vector3(0f, 0f, 0f), new Vector3Int(100, 100, 18));
        var far = Make("far", new Vector3(0f, 0f, 0.5f), new Vector3Int(100, 100, 18));
        var span = Make("span", new Vector3(0f, 0f, 0.25f), new Vector3Int(100, 100, 500));
        _handler!.Handle(MakeReq("group", new
        { id = "DepthCab", names = new[] { "near", "far", "span" }, width_axis = "z" }));

        var response = _handler.Handle(MakeReq("resize_module", new
        { module = "DepthCab", delta_mm = 100f }));
        Assert.AreEqual("result", response.type);
        Assert.AreEqual(0.6f, far.transform.position.z, 0.001f);
        Assert.AreEqual(600, span.DimensionsMM.z);
    }

    [Test]
    public void Align_SelectorMovesWholeMatchedModuleToTargetFace()
    {
        var a = Make("Cab_A", new Vector3(0f, 0f, 0f), new Vector3Int(100, 100, 100));
        var b = Make("Cab_B", new Vector3(0.5f, 0f, 0f), new Vector3Int(100, 100, 100));
        Make("Wall", new Vector3(2f, 0f, 0f), new Vector3Int(200, 1000, 1000));
        _handler!.Handle(MakeReq("group", new
        { id = "Cab", names = new[] { "Cab_A", "Cab_B" }, width_axis = "x" }));

        var response = _handler.Handle(MakeReq("align", new
        { selector = "module:Cab", target = "Wall", face = "right", gap_mm = 0f }));
        Assert.AreEqual("result", response.type);
        Assert.AreEqual(1.35f, a.transform.position.x, 0.001f);
        Assert.AreEqual(1.85f, b.transform.position.x, 0.001f);
        CommandStack.Undo();
        Assert.AreEqual(0f, a.transform.position.x, 0.001f);
        Assert.AreEqual(0.5f, b.transform.position.x, 0.001f);
    }

    [Test]
    public void GetCompact_ReturnsCornerMmAndOnlyRequestedFields()
    {
        Make("Board", new Vector3(1f, 0.2f, 2f), new Vector3Int(600, 400, 18));
        var response = _handler!.Handle(MakeReq("get", new
        { names = new[] { "Board" }, fields = new[] { "name", "anchorMm" } }));
        Assert.AreEqual("result", response.type);
        var jo = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(response.data));
        var row = jo["elements"]![0]!;
        Assert.AreEqual("Board", row["name"]!.ToString());
        CollectionAssert.AreEqual(new[] { 700, 1991 }, row["anchorMm"]!.ToObject<int[]>());
        Assert.IsNull(row["sizeMm"]);
    }

    [Test]
    public void GetSceneTree_NestsModulesAndFurnitureIntoPersistedRooms()
    {
        var member = Make("Cab_A", new Vector3(1f, 0f, 1f), new Vector3Int(100, 100, 100));
        Make("Chair", new Vector3(2f, 0f, 2f), new Vector3Int(100, 100, 100));
        var group = GroupManager.Create("Cab"); GroupManager.AddTo(group, member);
        ProjectRooms.Set(new[] { new RoomData
        {
            id = "Kitchen", floor = "Kitchen_floor", walls = new[] { "W1" },
            polygonXZ = new[] { 0, 0, 3000, 0, 3000, 3000, 0, 3000 }
        } });
        var response = _handler!.Handle(MakeReq("get_scene_tree", new { }));
        var jo = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(response.data));
        Assert.AreEqual(1, (int)jo["roomCount"]!);
        CollectionAssert.Contains(jo["rooms"]![0]!["modules"]!.ToObject<string[]>(), "Cab");
        CollectionAssert.Contains(jo["rooms"]![0]!["furniture"]!.ToObject<string[]>(), "Chair");
    }
}
