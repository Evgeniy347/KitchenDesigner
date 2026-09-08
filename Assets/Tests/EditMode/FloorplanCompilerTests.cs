using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class FloorplanCompilerTests
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear(); CommandStack.Clear(); ProjectInstructions.Reset();
        ProjectRooms.Reset(); ProjectFloorplans.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear(); CommandStack.Clear(); ProjectInstructions.Reset();
        ProjectRooms.Reset(); ProjectFloorplans.Reset();
    }

    private static ParamsFloorplanDeclaration TwoRooms() => new ParamsFloorplanDeclaration
    {
        id = "Plan",
        points = new[]
        {
            new FloorplanPoint { id="A", x=0, z=0 }, new FloorplanPoint { id="B", x=3000, z=0 },
            new FloorplanPoint { id="C", x=6000, z=0 }, new FloorplanPoint { id="D", x=0, z=3000 },
            new FloorplanPoint { id="E", x=3000, z=3000 }, new FloorplanPoint { id="F", x=6000, z=3000 }
        },
        rooms = new[]
        {
            new FloorplanRoom { id="Kitchen", poly=new[]{"A","B","E","D"}, kind="partition", height=2700 },
            new FloorplanRoom { id="Living", poly=new[]{"B","C","F","E"}, kind="partition", height=2700 }
        },
        openings = new[]
        {
            new FloorplanOpening { id="Door1", wall="wall_B_E", kind="door", offset_mm=500, width_mm=900, height_mm=2100 }
        }
    };

    [Test]
    public void Compile_RoomsReuseSharedWall_AndSvgIsDeterministic()
    {
        var compiled = FloorplanCompiler.Compile(TwoRooms());
        Assert.IsTrue(compiled.IsValid, string.Join(" | ", compiled.errors));
        Assert.AreEqual(7, compiled.walls.Count);
        Assert.AreEqual(2, compiled.floors.Count);
        Assert.AreEqual(2, compiled.rooms.Count);
        Assert.AreEqual("wall_B_E", compiled.rooms[0].walls[1]);
        Assert.AreEqual("wall_B_E", compiled.rooms[1].walls[3]);
        string svg = FloorplanCompiler.ToSvg(compiled);
        Assert.IsTrue(svg.StartsWith("<svg"));
        Assert.AreEqual(svg, FloorplanCompiler.ToSvg(compiled));
        Assert.AreEqual(1, svg.Split(new[] { "data-id=\"wall_B_E\"" }, System.StringSplitOptions.None).Length - 1);
        Assert.IsTrue(svg.Contains("data-id=\"Door1\""));
    }

    [Test]
    public void Compile_UnknownPointProducesValidationError()
    {
        var d = TwoRooms(); d.rooms[0].poly[0] = "Missing";
        var compiled = FloorplanCompiler.Compile(d);
        Assert.IsFalse(compiled.IsValid);
        StringAssert.Contains("unknown point 'Missing'", string.Join(" | ", compiled.errors));
    }

    [Test]
    public void PreviewFloorplan_ReturnsSvgWithoutMutatingScene()
    {
        PartRegistry.Clear();
        var declaration = TwoRooms();
        var request = new McpRequest
        {
            id = "p", method = "preview_floorplan",
            Params = JObject.Parse(JsonConvert.SerializeObject(declaration))
        };
        var response = new McpCommandHandler().Handle(request);
        Assert.AreEqual("result", response.type);
        var jo = JObject.Parse(JsonConvert.SerializeObject(response.data));
        Assert.AreEqual(7, (int)jo["walls"]!);
        StringAssert.StartsWith("<svg", jo["svg"]!.ToString());
        Assert.AreEqual(0, PartRegistry.GetAll().Count);
    }

    [Test]
    public void ProjectRooms_AreCapturedAndSerializedWithProject()
    {
        ProjectRooms.Set(new[] { new RoomData
        {
            id = "Kitchen", floor = "Kitchen_floor", walls = new[] { "W1" },
            openings = new[] { "Door1" }, polygonXZ = new[] { 0, 0, 1000, 0, 0, 1000 }
        } });
        var data = SaveLoadManager.CaptureScene(System.Array.Empty<KitchenElement>());
        var restored = SaveLoadManager.Deserialize(SaveLoadManager.Serialize(data))!;
        Assert.AreEqual("Kitchen", restored.rooms[0].id);
        CollectionAssert.AreEqual(new[] { 0, 0, 1000, 0, 0, 1000 }, restored.rooms[0].polygonXZ);
        ProjectRooms.Reset();
    }

    [Test]
    public void ApplyFloorplan_CreatesWholePlanInOneUndo_AndIsIdempotentByScope()
    {
        ProjectInstructions.Text = "partition_wall_thickness_mm: 100\nfloor_thickness_mm: 120";
        var handler = new McpCommandHandler();
        McpResponse Apply(ParamsFloorplanDeclaration d) => handler.Handle(new McpRequest
        { id = "a", method = "apply_floorplan", Params = JObject.Parse(JsonConvert.SerializeObject(d)) });

        var first = Apply(TwoRooms());
        Assert.AreEqual("result", first.type);
        Assert.AreEqual(10, PartRegistry.GetAll().Count, "7 walls + 2 floors + 1 opening");
        Assert.AreEqual(1, CommandStack.UndoCount);
        Assert.AreEqual(2, ProjectRooms.Items.Count);
        Assert.AreEqual(10, ProjectFloorplans.Find("Plan")!.elements.Length);
        var shared = PartRegistry.GetAll().Find(e => e.PartName == "wall_B_E")!.GetComponent<Wall>();
        Assert.AreEqual(1, shared.AttachedDoors.Count);

        var reduced = TwoRooms();
        reduced.rooms = new[] { reduced.rooms[0] };
        reduced.openings = System.Array.Empty<FloorplanOpening>();
        var second = Apply(reduced);
        Assert.AreEqual("result", second.type);
        Assert.AreEqual(5, PartRegistry.GetAll().Count, "4 walls + 1 floor; obsolete scope elements removed");
        Assert.AreEqual(1, ProjectRooms.Items.Count);
        Assert.AreEqual(2, CommandStack.UndoCount, "each whole-plan call is one undo");

        CommandStack.Undo();
        Assert.AreEqual(10, PartRegistry.GetAll().Count);
        Assert.AreEqual(2, ProjectRooms.Items.Count);
        Assert.AreEqual(1, PartRegistry.GetAll().Find(e => e.PartName == "wall_B_E")!
            .GetComponent<Wall>().AttachedDoors.Count);
    }

    [Test]
    public void ApplyFloorplan_PerWallThicknessOverridesTheKindInstruction()
    {
        // A real plan carries more than two thicknesses: 250 mm cross-walls, 125 mm
        // partitions and a 150 mm facade. Only the first two fit the instructions.
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 250\nfloor_thickness_mm: 120";
        var d = new ParamsFloorplanDeclaration
        {
            id = "Plan",
            points = new[]
            {
                new FloorplanPoint { id = "A", x = 0, z = 0 },
                new FloorplanPoint { id = "B", x = 3000, z = 0 },
                new FloorplanPoint { id = "C", x = 3000, z = 3000 },
            },
            walls = new[]
            {
                new FloorplanWall { id = "Cross", from = "A", to = "B", kind = "bearing", height_mm = 2700 },
                new FloorplanWall { id = "Facade", from = "B", to = "C", kind = "bearing", height_mm = 2700, thickness_mm = 150 },
                // partition thickness is NOT in the instructions — the override stands in for it
                new FloorplanWall { id = "Light", from = "A", to = "C", kind = "partition", height_mm = 2700, thickness_mm = 125 },
            },
        };
        var response = new McpCommandHandler().Handle(new McpRequest
        { id = "a", method = "apply_floorplan", Params = JObject.Parse(JsonConvert.SerializeObject(d)) });

        Assert.AreEqual("result", response.type, JsonConvert.SerializeObject(response.data));
        int Thickness(string name)
        {
            var dims = PartRegistry.GetAll().Find(e => e.PartName == name)!.DimensionsMM;
            return dims.z;
        }
        Assert.AreEqual(250, Thickness("Cross"), "no override -> bearing instruction");
        Assert.AreEqual(150, Thickness("Facade"), "override wins over the bearing instruction");
        Assert.AreEqual(125, Thickness("Light"), "override stands in for a missing instruction");
    }

    [Test]
    public void ApplyFloorplan_LateFailureRollsBackEverythingAndDoesNotAddUndo()
    {
        ProjectInstructions.Text = "partition_wall_thickness_mm: 100"; // floor thickness intentionally absent
        var d = TwoRooms();
        var response = new McpCommandHandler().Handle(new McpRequest
        { id = "a", method = "apply_floorplan", Params = JObject.Parse(JsonConvert.SerializeObject(d)) });
        Assert.AreEqual("error", response.type);
        Assert.AreEqual(0, PartRegistry.GetAll().Count);
        Assert.AreEqual(0, CommandStack.UndoCount);
        Assert.IsNull(ProjectFloorplans.Find("Plan"));
        Assert.AreEqual(0, ProjectRooms.Items.Count);
    }
}
