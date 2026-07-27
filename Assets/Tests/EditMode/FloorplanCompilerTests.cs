using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FloorplanCompilerTests
{
    [TearDown]
    public void TearDown() => ProjectRooms.Reset();

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
            new FloorplanOpening { id="Door1", wall="wall_B_E", kind="door", offset_mm=500, width=900, height=2100 }
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
}
