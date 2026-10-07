using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Планировочные инструменты отвечают так же, как остальные мутации: что ЭТОТ вызов сломал или
/// починил (sceneViolationDelta), а не голый список violations по всем затронутым элементам.</summary>
public class McpPlanReplyTests : McpTestFixture
{
    [SetUp]
    public void Setup()
    {
        CommandStack.Clear();
        ProjectInstructions.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        ProjectInstructions.Reset();
        LevelRegistry.Reset();
    }

    private static void AssertDeltaInsteadOfViolations(JObject reply, string tool)
    {
        Assert.IsNotNull(reply["sceneViolationDelta"], tool + ": нет sceneViolationDelta");
        Assert.IsNull(reply["violations"], tool + ": голый список violations заменён дельтой");
        Assert.IsNull(reply["sceneViolationCount"], tool + ": общего счётчика нет");
    }

    [Test]
    public void CreateWalls_CreateFloor_AddOpening_ReplyWithTheDelta()
    {
        var walls = ReplyOf(_handler!.Handle(MakeReq("create_walls", new
        {
            segments = new[] { new { name = "Wall", from_x = 0, from_z = 0, to_x = 4000, to_z = 0, kind = "bearing", height = 2700, thickness_mm = 200 } }
        })));
        var floor = ReplyOf(_handler.Handle(MakeReq("create_floor", new
        {
            name = "Floor", top_y_mm = 0, thickness_mm = 100,
            poly = new[] { new { x = 0, z = -100 }, new { x = 4000, z = -100 }, new { x = 4000, z = 3000 }, new { x = 0, z = 3000 } }
        })));
        var opening = ReplyOf(_handler.Handle(MakeReq("add_opening", new
        {
            name = "Win1", wall = "Wall", kind = "window", offset_mm = 1500, width = 1200, height = 1200, sill_mm = 900
        })));

        AssertDeltaInsteadOfViolations(walls, "create_walls");
        AssertDeltaInsteadOfViolations(floor, "create_floor");
        AssertDeltaInsteadOfViolations(opening, "add_opening");
        CollectionAssert.AreEqual(new[] { "Wall" }, walls["created"]!.ToObject<string[]>());
        CollectionAssert.AreEqual(new[] { "Win1" }, opening["created"]!.ToObject<string[]>());
    }

    [Test]
    public void ARepeatedCreateWalls_ReportsAnUpdate_NotACreation()
    {
        object Declaration(int length) => new
        {
            segments = new[] { new { name = "W", from_x = 0, from_z = 0, to_x = length, to_z = 0, kind = "bearing", height = 2500, thickness_mm = 200 } }
        };
        _handler!.Handle(MakeReq("create_walls", Declaration(2000)));

        var reply = ReplyOf(_handler.Handle(MakeReq("create_walls", Declaration(3000))));

        Assert.AreEqual(1, reply["updatedCount"]!.Value<int>());
        Assert.IsNull(reply["created"], "создавать было нечего: имя уже есть");
    }

    [Test]
    public void ApplyFloorplan_RepliesWithCountsAndTheDelta()
    {
        ProjectInstructions.Text = "bearing_wall_thickness_mm: 200\nfloor_thickness_mm: 120";
        var resp = _handler!.Handle(MakeReq("apply_floorplan", new
        {
            id = "flat",
            points = new[] { new { id = "sw", x = 0, z = 0 }, new { id = "se", x = 3000, z = 0 }, new { id = "ne", x = 3000, z = 4000 }, new { id = "nw", x = 0, z = 4000 } },
            rooms = new[] { new { id = "kitchen", poly = new[] { "sw", "se", "ne", "nw" }, kind = "bearing", height = 2700 } },
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? JObject.FromObject(resp.data!)["message"]!.ToString() : "");
        var reply = ReplyOf(resp);
        AssertDeltaInsteadOfViolations(reply, "apply_floorplan");
        Assert.AreEqual("flat", reply["id"]!.Value<string>());
        Assert.AreEqual(1, reply["roomCount"]!.Value<int>());
        Assert.AreEqual(4, reply["wallCount"]!.Value<int>());
    }
}
