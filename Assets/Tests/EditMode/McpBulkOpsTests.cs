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
    public void Move_ShiftsSelectionByMillimetres()
    {
        var b = Make("shelf", new Vector3(0f, 0.2f, 0f), new Vector3Int(600, 400, 18));

        var resp = _handler!.Handle(MakeReq("move", new { selector = "shelf", dx = 100f }));
        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(0.1f, b.transform.position.x, 0.001f);
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
}
