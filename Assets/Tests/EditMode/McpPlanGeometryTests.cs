using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

public class McpPlanGeometryTests
{
    private McpCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear(); CommandStack.Clear(); ProjectInstructions.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var e in PartRegistry.GetAll())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear(); CommandStack.Clear(); ProjectInstructions.Reset();
    }

    private static McpRequest Req(string method, object data) => new McpRequest
    {
        id = "t", method = method,
        Params = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data))
    };

    [Test]
    public void CreateWalls_DerivesCenterLengthRotationAndKindFromEndpoints()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200\nbearing_wall_material: concrete";
        var response = _handler.Handle(Req("create_walls", new
        {
            origin_x_mm = 1000, origin_z_mm = -500, base_y_mm = 100,
            segments = new[] { new { name = "W1", from_x = 0, from_z = 0, to_x = 3000, to_z = 4000, kind = "bearing", height = 2700 } }
        }));

        Assert.AreEqual("result", response.type);
        var wall = PartRegistry.GetAll().Find(e => e.PartName == "W1")!;
        Assert.AreEqual(new Vector3Int(5000, 2700, 200), wall.DimensionsMM);
        Assert.AreEqual(2.5f, wall.transform.position.x, 0.0001f);
        Assert.AreEqual(1.45f, wall.transform.position.y, 0.0001f);
        Assert.AreEqual(1.5f, wall.transform.position.z, 0.0001f);
        Assert.AreEqual("bearing", wall.GetComponent<Wall>().Kind);
        Assert.AreEqual("concrete", wall.MaterialId);
        Vector3 localEndDirection = wall.transform.rotation * Vector3.right;
        Assert.AreEqual(0.6f, localEndDirection.x, 0.001f);
        Assert.AreEqual(0.8f, localEndDirection.z, 0.001f);
    }

    [Test]
    public void CreateWalls_MissingThicknessRejectsWholeBatch()
    {
        var response = _handler.Handle(Req("create_walls", new
        {
            segments = new[] { new { name = "W1", from_x = 0, from_z = 0, to_x = 1000, to_z = 0, kind = "bearing", height = 2700 } }
        }));
        Assert.AreEqual("error", response.type);
        Assert.AreEqual(0, PartRegistry.GetAll().Count);
    }

    [Test]
    public void CreateWalls_RepeatedNameUpdatesWithoutDuplicate_AndUndoRestores()
    {
        ProjectInstructions.Text = "partition_wall_thickness_mm: 100";
        object First(int length) => new
        {
            segments = new[] { new { name = "W", from_x = 0, from_z = 0, to_x = length, to_z = 0, kind = "partition", height = 2500 } }
        };
        _handler.Handle(Req("create_walls", First(2000)));
        _handler.Handle(Req("create_walls", First(3000)));
        Assert.AreEqual(1, PartRegistry.GetAll().Count);
        Assert.AreEqual(3000, PartRegistry.GetAll()[0].DimensionsMM.x);
        CommandStack.Undo();
        Assert.AreEqual(2000, PartRegistry.GetAll()[0].DimensionsMM.x);
    }

    [Test]
    public void CreateWalls_SharedCornerGetsMiteredEnds()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200";
        var response = _handler.Handle(Req("create_walls", new
        {
            segments = new[]
            {
                new { name = "South", from_x = 0, from_z = 0, to_x = 1000, to_z = 0, kind = "bearing", height = 2700 },
                new { name = "East", from_x = 1000, from_z = 0, to_x = 1000, to_z = 1000, kind = "bearing", height = 2700 }
            }
        }));
        Assert.AreEqual("result", response.type);
        var south = PartRegistry.GetAll().Find(e => e.PartName == "South")!.GetComponent<Wall>();
        Assert.AreEqual(0.4f, south.EndShape.endFront, 0.0001f);
        Assert.AreEqual(0.6f, south.EndShape.endBack, 0.0001f);
        Assert.AreEqual(0.6f, south.GetComponent<MeshFilter>().sharedMesh.bounds.max.x, 0.0001f);
    }

    [Test]
    public void CreateFloor_BuildsConcavePolygonAndIsIdempotent()
    {
        var poly = new[] { new { x = 0, z = 0 }, new { x = 3000, z = 0 },
            new { x = 3000, z = 1000 }, new { x = 1000, z = 1000 },
            new { x = 1000, z = 3000 }, new { x = 0, z = 3000 } };
        var response = _handler.Handle(Req("create_floor", new
        { name = "Floor1", origin_x_mm = 500, origin_z_mm = 700, top_y_mm = 0, thickness_mm = 120, poly }));
        Assert.AreEqual("result", response.type);
        var floor = (FloorElement)PartRegistry.GetAll()[0];
        Assert.AreEqual(new Vector3Int(3000, 120, 3000), floor.DimensionsMM);
        Assert.AreEqual(2f, floor.transform.position.x, 0.0001f);
        Assert.AreEqual(-0.06f, floor.transform.position.y, 0.0001f);
        Assert.AreEqual(2.2f, floor.transform.position.z, 0.0001f);
        Assert.AreEqual(6, floor.PolygonLocalMm.Count);
        Assert.Greater(floor.GetComponent<MeshFilter>().sharedMesh.triangles.Length, 0);

        _handler.Handle(Req("create_floor", new
        { name = "Floor1", thickness_mm = 100, poly }));
        Assert.AreEqual(1, PartRegistry.GetAll().Count);
        Assert.AreEqual(100, floor.DimensionsMM.y);
    }

    [Test]
    public void FloorPolygon_RoundTripsThroughElementData()
    {
        var go = ElementFactory.CreateFloor(new Vector3Int(2000, 100, 1000), "Poly", Vector3.zero);
        var floor = go.GetComponent<FloorElement>();
        floor.SetPolygonLocalMm(new[] { new Vector2Int(-1000, -500), new Vector2Int(1000, -500), new Vector2Int(0, 500) });
        var json = JsonUtility.ToJson(ElementData.FromElement(floor));
        var restored = JsonUtility.FromJson<ElementData>(json);
        Assert.AreEqual(3, restored.FloorPolygon().Count);
        Assert.AreEqual(new Vector2Int(0, 500), restored.FloorPolygon()[2]);
    }
}
