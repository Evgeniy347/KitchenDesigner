using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

public class McpPlanGeometryTests : McpTestFixture
{
    [SetUp]
    public void Setup()
    {
        CommandStack.Clear(); ProjectInstructions.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear(); ProjectInstructions.Reset();
    }

    [Test]
    public void CreateWalls_DerivesCenterLengthRotationAndKindFromEndpoints()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200\nbearing_wall_material: concrete";
        var response = _handler!.Handle(MakeReq("create_walls", new
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
        var response = _handler!.Handle(MakeReq("create_walls", new
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
        _handler!.Handle(MakeReq("create_walls", First(2000)));
        _handler!.Handle(MakeReq("create_walls", First(3000)));
        Assert.AreEqual(1, PartRegistry.GetAll().Count);
        Assert.AreEqual(3000, PartRegistry.GetAll()[0].DimensionsMM.x);
        CommandStack.Undo();
        Assert.AreEqual(2000, PartRegistry.GetAll()[0].DimensionsMM.x);
    }

    [Test]
    public void CreateWalls_SharedCornerGetsMiteredEnds()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200";
        var response = _handler!.Handle(MakeReq("create_walls", new
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
        var response = _handler!.Handle(MakeReq("create_floor", new
        { name = "Floor1", origin_x_mm = 500, origin_z_mm = 700, top_y_mm = 0, thickness_mm = 120, poly }));
        Assert.AreEqual("result", response.type);
        var floor = (FloorElement)PartRegistry.GetAll()[0];
        Assert.AreEqual(new Vector3Int(3000, 120, 3000), floor.DimensionsMM);
        Assert.AreEqual(2f, floor.transform.position.x, 0.0001f);
        Assert.AreEqual(-0.06f, floor.transform.position.y, 0.0001f);
        Assert.AreEqual(2.2f, floor.transform.position.z, 0.0001f);
        Assert.AreEqual(6, floor.PolygonLocalMm.Count);
        Assert.Greater(floor.GetComponent<MeshFilter>().sharedMesh.triangles.Length, 0);

        _handler!.Handle(MakeReq("create_floor", new
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
        var json = JsonUtility.ToJson(ElementCapture.FromElement(floor));
        var restored = JsonUtility.FromJson<ElementData>(json);
        Assert.AreEqual(3, restored.FloorPolygon().Count);
        Assert.AreEqual(new Vector2Int(0, 500), restored.FloorPolygon()[2]);
    }

    [Test]
    public void AddOpening_PositionsByWallStart_AttachesAndCutsMesh()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200";
        _handler!.Handle(MakeReq("create_walls", new
        {
            segments = new[] { new { name = "WallA", from_x = 0, from_z = 0, to_x = 4000, to_z = 0, kind = "bearing", height = 2700 } }
        }));
        var wall = PartRegistry.GetAll().Find(e => e.PartName == "WallA")!.GetComponent<Wall>();
        int beforeTriangles = wall.GetComponent<MeshFilter>().sharedMesh.triangles.Length;

        var response = _handler!.Handle(MakeReq("add_opening", new
        { name = "Win1", wall = "WallA", kind = "window", offset_mm = 1000, width = 1200, height = 1400, sill_mm = 800 }));
        Assert.AreEqual("result", response.type);
        var window = (WindowElement)PartRegistry.GetAll().Find(e => e.PartName == "Win1")!;
        Assert.AreEqual("WallA", window.AttachedWallName);
        var local = Quaternion.Inverse(wall.transform.rotation) * (window.transform.position - wall.FullPosition);
        Assert.AreEqual(-0.4f, local.x, 0.0001f);
        Assert.AreEqual(0.15f, local.y, 0.0001f);
        Assert.Greater(wall.GetComponent<MeshFilter>().sharedMesh.triangles.Length, beforeTriangles);

        CommandStack.Undo();
        Assert.IsNull(PartRegistry.GetAll().Find(e => e.PartName == "Win1"));
        Assert.AreEqual(0, wall.AttachedWindows.Count);
        Assert.AreEqual(beforeTriangles, wall.GetComponent<MeshFilter>().sharedMesh.triangles.Length);
    }

    [Test]
    public void AddOpening_RepeatedNameUpdatesWithoutDuplicate()
    {
        ProjectInstructions.Text = "partition_wall_thickness_mm: 100";
        _handler!.Handle(MakeReq("create_walls", new
        {
            segments = new[] { new { name = "W", from_x = 0, from_z = 0, to_x = 3000, to_z = 0, kind = "partition", height = 2500 } }
        }));
        object Opening(int offset) => new
        { name = "D", wall = "W", kind = "door", offset_mm = offset, width = 900, height = 2100, sill_mm = 0 };
        _handler!.Handle(MakeReq("add_opening", Opening(100)));
        _handler!.Handle(MakeReq("add_opening", Opening(500)));
        Assert.AreEqual(2, PartRegistry.GetAll().Count);
        var door = (DoorElement)PartRegistry.GetAll().Find(e => e.PartName == "D")!;
        Assert.AreEqual(0.95f, door.transform.position.x, 0.0001f);
        CommandStack.Undo();
        Assert.AreEqual(0.55f, door.transform.position.x, 0.0001f);
    }

    [Test]
    public void AddOpening_OutOfBoundsRejectsWithoutCreation()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200";
        _handler!.Handle(MakeReq("create_walls", new
        {
            segments = new[] { new { name = "W", from_x = 0, from_z = 0, to_x = 1000, to_z = 0, kind = "bearing", height = 2500 } }
        }));
        var response = _handler!.Handle(MakeReq("add_opening", new
        { name = "Bad", wall = "W", kind = "window", offset_mm = 500, width = 800, height = 1000, sill_mm = 800 }));
        Assert.AreEqual("error", response.type);
        Assert.AreEqual(1, PartRegistry.GetAll().Count);
    }
}
