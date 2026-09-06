using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using Newtonsoft.Json.Linq;

public class FacadeMcpTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private McpCommandHandler? _handler;

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
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest
        {
            id = "test",
            method = method,
            Params = JObject.Parse(json)
        };
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void GetElements_Facade_ReturnsTypeAndDimensions()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "F" } }));
        Assert.AreEqual("result", resp.type);

        var json = JObject.FromObject(resp.data!);
        Assert.AreEqual(1, json["count"]!.Value<int>());
        var elements = json["elements"] as JArray;
        Assert.IsNotNull(elements);
        Assert.AreEqual(1, elements!.Count);
        var el = elements[0]!;
        Assert.AreEqual("FacadeElement", el["type"]!.Value<string>());
        Assert.AreEqual(400, el["dimX"]!.Value<int>());
        Assert.AreEqual(300, el["dimY"]!.Value<int>());
        Assert.AreEqual(18, el["dimZ"]!.Value<int>());
    }

    [Test]
    public void GetViolations_FacadeWithObstruction_ReturnsFaceObstruction()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeElement("Obstacle", new Vector3Int(200, 200, 18), new Vector3(0f, 0f, 0.038f));

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));
        var json = JObject.FromObject(resp.data!);
        var violations = json["violations"] as JArray;

        bool found = false;
        foreach (var v in violations!)
        {
            if (v["name"]!.Value<string>() != "F") continue;
            var obs = v["faceObstructions"] as JArray;
            if (obs != null && obs.Count > 0)
            {
                Assert.AreEqual("Obstacle", obs[0]!["neighbor"]!.Value<string>());
                found = true;
            }
        }
        Assert.IsTrue(found, "facade F should have faceObstructions in violations");
    }

    [Test]
    public void GetViolations_InwardFacade_ReturnsViolation()
    {
        var box = MakeElement("Box", new Vector3Int(600, 400, 500), Vector3.zero);
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, -0.3f));
        GroupManager.Link(new List<KitchenElement> { box, f });

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));
        var json = JObject.FromObject(resp.data!);
        var violations = json["violations"] as JArray;

        bool found = false;
        foreach (var v in violations!)
        {
            if (v["name"]!.Value<string>() != "F") continue;
            found = true;
            Assert.IsTrue(v["faceInward"]!.Value<bool>());
        }
        Assert.IsTrue(found, "facade F should appear in violations");
    }

    [Test]
    public void CreateElements_Facade_ReturnsEnvelopeWithFacadeInfo()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[]
            {
                new { name = "Door", type = "facade", width = 400, height = 300, depth = 18, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f }
            }
        }));

        var json = JObject.FromObject(resp.data!);
        Assert.IsTrue(json["ok"]!.Value<bool>());
        var elements = json["elements"] as JArray;
        Assert.IsNotNull(elements);
        Assert.AreEqual(1, elements!.Count);
        Assert.AreEqual("FacadeElement", elements[0]!["type"]!.Value<string>());
        Assert.IsNotNull(elements[0]!["facadeMode"]);
        Assert.IsNotNull(json["sceneViolationCount"]);
        var door = GameObject.Find("Door");
        if (door != null) _spawned.Add(door);
    }

    [Test]
    public void EditElements_RotateFacade_ReturnsRotY()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F", rot_y = 90f } }
        }));

        var json = JObject.FromObject(resp.data!);
        Assert.IsTrue(json["ok"]!.Value<bool>());
        Assert.IsTrue(json["applied"]!.Value<bool>());
        var results = json["results"] as JArray;
        Assert.IsNotNull(results);
        Assert.AreEqual(1, results!.Count);
        Assert.AreEqual(90f, results[0]!["rotY"]!.Value<float>(), 0.01f);

        var info = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "F" } }));
        var infoJson = JObject.FromObject(info.data!);
        var infoElements = infoJson["elements"] as JArray;
        Assert.AreEqual(90f, infoElements![0]!["rotY"]!.Value<float>(), 0.01f);
    }

    [Test]
    public void EditElements_SetFacadeMode_ReportsOpeningCollision_InViolations()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeElement("Obstacle", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, 0.3f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F", mode = "drawer_out" } }
        }));
        var json = JObject.FromObject(resp.data!);
        Assert.IsTrue(json["ok"]!.Value<bool>());

        var results = json["results"] as JArray;
        Assert.IsNotNull(results);
        var result = results![0]!;

        var viol = result["violations"] as JArray;
        Assert.IsNotNull(viol);
        bool found = false;
        foreach (var v in viol!)
        {
            if (v["kind"]!.Value<string>() != "opening_collision") continue;
            found = true;
            Assert.AreEqual("Obstacle", v["neighbor"]!.Value<string>());
            Assert.AreEqual("drawer_out", v["openingMode"]!.Value<string>());
        }
        Assert.IsTrue(found, "opening_collision must be reported in the element violations");
    }
}
