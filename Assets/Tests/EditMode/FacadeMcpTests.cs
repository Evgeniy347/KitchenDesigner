using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using Newtonsoft.Json.Linq;

public class FacadeMcpTests : McpTestFixture
{
    [SetUp]
    public void Setup()
    {
        GroupManager.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        GroupManager.Clear();
    }

    [Test]
    public void GetElements_Facade_ReturnsTypeAndDimensions()
    {
        var f = MakePrimitiveFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "F" } }));
        Assert.AreEqual("result", resp.type);

        var json = JObject.FromObject(resp.data!);
        Assert.AreEqual(1, json["count"]!.Value<int>());
        var elements = json["elements"] as JArray;
        Assert.IsNotNull(elements);
        Assert.AreEqual(1, elements!.Count);
        var el = elements[0]!;
        Assert.AreEqual("FacadeElement", el["type"]!.Value<string>());
        Assert.AreEqual(400, el["dimXMm"]!.Value<int>());
        Assert.AreEqual(300, el["dimYMm"]!.Value<int>());
        Assert.AreEqual(18, el["dimZMm"]!.Value<int>());
    }

    [Test]
    public void GetViolations_FacadeWithObstruction_ReturnsFaceObstruction()
    {
        var f = MakePrimitiveFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        MakePrimitiveElement("Obstacle", new Vector3Int(200, 200, 18), new Vector3(0f, 0f, 0.038f));

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
        var box = MakePrimitiveElement("Box", new Vector3Int(600, 400, 500), Vector3.zero);
        var f = MakePrimitiveFacade("F", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, -0.3f));
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
    public void CreateElements_Facade_ReturnsEnvelopeWithItsPlacement_AndFacadeInfoStaysInGetElements()
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
        var placements = json["placements"] as JArray;
        Assert.IsNotNull(placements);
        Assert.AreEqual(1, placements!.Count);
        Assert.AreEqual("Door", placements[0]!["name"]!.Value<string>());
        Assert.IsNotNull(json["sceneViolationDelta"]);
        var info = InfoJsonOf("Door");
        Assert.AreEqual("FacadeElement", info["type"]!.Value<string>());
        Assert.IsNotNull(info["facadeMode"], "тип и режим фасада читаются полным get_elements, а не мутацией");
        var door = GameObject.Find("Door");
        if (door != null) _spawned.Add(door);
    }

    [Test]
    public void EditElements_RotateFacade_ReturnsRotY()
    {
        var f = MakePrimitiveFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F", rot_y = 90f } }
        }));

        var json = JObject.FromObject(resp.data!);
        Assert.IsTrue(json["ok"]!.Value<bool>());
        Assert.IsTrue(json["applied"]!.Value<bool>());
        var placements = json["placements"] as JArray;
        Assert.IsNotNull(placements);
        Assert.AreEqual(1, placements!.Count);
        var world = InfoJsonOf("F");
        var footprint = placements[0]!["footprintMm"]!.ToObject<float[]>()!;
        Assert.AreEqual(world["worldDimXMm"]!.Value<float>(), footprint[0], 0.6f, "след по X совпадает с worldDimXMm");
        Assert.AreEqual(world["worldDimZMm"]!.Value<float>(), footprint[2], 0.6f, "след по Z совпадает с worldDimZMm");
        Assert.Less(footprint[0], footprint[2],
            "после rot_y=90 фасад 400x300x18 лежит длинной стороной вдоль Z: ответ называет МИРОВОЙ след, а не локальные габариты");

        var info = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "F" } }));
        var infoJson = JObject.FromObject(info.data!);
        var infoElements = infoJson["elements"] as JArray;
        Assert.AreEqual(90f, infoElements![0]!["rotYDeg"]!.Value<float>(), 0.01f);
    }

    [Test]
    public void EditElements_SetFacadeMode_ReportsOpeningCollision_InViolations()
    {
        var f = MakePrimitiveFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        MakePrimitiveElement("Obstacle", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, 0.3f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F", mode = "drawer_out" } }
        }));
        var json = JObject.FromObject(resp.data!);
        Assert.IsTrue(json["ok"]!.Value<bool>());

        var issues = PlacementOf(resp)["issues"] as JArray;
        Assert.IsNotNull(issues);
        CollectionAssert.Contains(issues!.Select(i => i.Value<string>()).ToArray(), "opening_collision Obstacle",
            "opening_collision must be reported in the placement issues, naming the neighbour");
    }
}
