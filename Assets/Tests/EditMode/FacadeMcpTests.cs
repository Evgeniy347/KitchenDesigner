using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using Newtonsoft.Json.Linq;

public class FacadeMcpTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private McpCommandHandler _handler;

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
    public void GetElementInfo_Facade_ReturnsFaceNormalAndNoObstructions()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "F" }));
        Assert.AreEqual("result", resp.type);

        var json = JObject.FromObject(resp.data);
        Assert.AreEqual(0f, json["faceNormalX"].Value<float>(), 1e-5f);
        Assert.AreEqual(0f, json["faceNormalY"].Value<float>(), 1e-5f);
        Assert.AreEqual(-1f, json["faceNormalZ"].Value<float>(), 1e-5f);
        Assert.IsFalse(json["faceInward"].Value<bool>());
        Assert.IsEmpty(json["faceObstructions"]);
        Assert.IsEmpty(json["openingViolations"]);
    }

    [Test]
    public void GetElementInfo_FacadeWithObstruction_ReturnsFaceObstruction()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeElement("Obstacle", new Vector3Int(200, 200, 18), new Vector3(0f, 0f, -0.038f));

        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "F" }));
        var json = JObject.FromObject(resp.data);
        var obs = json["faceObstructions"] as JArray;
        Assert.AreEqual(1, obs.Count);
        Assert.AreEqual("Obstacle", obs[0]["neighbor"].Value<string>());
    }

    [Test]
    public void GetViolations_InwardFacade_ReturnsViolation()
    {
        var box = MakeElement("Box", new Vector3Int(600, 400, 500), Vector3.zero);
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, -0.3f));
        f.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        GroupManager.Link(new List<KitchenElement> { box, f });

        var resp = _handler.Handle(MakeReq("get_violations", new { }));
        var json = JObject.FromObject(resp.data);
        var violations = json["violations"] as JArray;

        bool found = false;
        foreach (var v in violations)
        {
            if (v["name"].Value<string>() != "F") continue;
            found = true;
            Assert.IsTrue(v["faceInward"].Value<bool>());
        }
        Assert.IsTrue(found, "facade F should appear in violations");
    }

    [Test]
    public void CreateElement_Facade_ReturnsFaceValidationFields()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "Door", x = 0f, y = 0f, z = 0f,
            width = 400, height = 300, depth = 18, is_facade = true
        }));

        var json = JObject.FromObject(resp.data);
        Assert.IsTrue(json["is_facade"].Value<bool>());
        Assert.IsNotNull(json["faceNormal"]);
        Assert.IsNotNull(json["faceObstructions"]);
        Assert.IsNotNull(json["openingViolations"]);
        var door = GameObject.Find("Door");
        if (door != null) _spawned.Add(door);
    }

    [Test]
    public void RotateElement_Facade_ReturnsFaceValidationFields()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("rotate_element", new { name = "F", y = 90f }));

        var json = JObject.FromObject(resp.data);
        Assert.IsNotNull(json["faceNormal"]);
        Assert.AreEqual(-1f, json["faceNormal"]["x"].Value<float>(), 1e-3f);
        Assert.IsNotNull(json["faceObstructions"]);
        Assert.IsNotNull(json["openingViolations"]);
    }

    [Test]
    public void SetFacadeMode_ReturnsOpeningViolations()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeElement("Obstacle", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, -0.3f));

        var resp = _handler.Handle(MakeReq("set_facade_mode", new { name = "F", mode = "drawer_out" }));
        var json = JObject.FromObject(resp.data);
        Assert.AreEqual("drawer_out", json["mode"].Value<string>());
        var viol = json["openingViolations"] as JArray;
        Assert.AreEqual(1, viol.Count);
        Assert.AreEqual("Obstacle", viol[0]["neighbor"].Value<string>());
    }
}
