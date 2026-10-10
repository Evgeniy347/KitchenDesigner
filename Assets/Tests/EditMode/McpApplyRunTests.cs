using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

public class McpApplyRunTests : McpTestFixture
{
    private const int ScenarioCallBudget = 3;
    private const int ScenarioByteBudget = 2 * 1024;

    private int _calls;
    private int _bytes;

    [SetUp]
    public void Setup()
    {
        CommandStack.Clear();
        ProjectInstructions.Reset();
        ProjectFloorplans.Reset();
        ProjectRooms.Reset();
        BuildTheRoom();
        CommandStack.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        ProjectInstructions.Reset();
        ProjectFloorplans.Reset();
        ProjectRooms.Reset();
        LevelRegistry.Reset();
    }

    private JObject Wire(string tool, object args, bool counted = false)
    {
        var def = McpToolRegistry.Tools.First(t => t.Name == tool);
        var content = new McpToolCall(_handler!.Handle).Invoke(def, JObject.FromObject(args), "run");
        var text = content["content"]![0]!["text"]!.Value<string>()!;
        Assert.IsNull(content["isError"], tool + " отказал: " + text);
        if (counted)
        {
            _calls++;
            _bytes += Encoding.UTF8.GetByteCount(text);
        }
        return tool == "describe_scene" ? new JObject { ["text"] = text } : JObject.Parse(text);
    }

    private string Refusal(string tool, object args)
    {
        var def = McpToolRegistry.Tools.First(t => t.Name == tool);
        var content = new McpToolCall(_handler!.Handle).Invoke(def, JObject.FromObject(args), "run");
        Assert.IsNotNull(content["isError"], tool + " обязан отказать");
        return content["content"]![0]!["text"]!.Value<string>()!;
    }

    private void BuildTheRoom()
    {
        Wire("create_walls", new
        {
            segments = new[] { new { name = "Wall", from_x = 0, from_z = 0, to_x = 4000, to_z = 0, kind = "bearing", height = 2700, thickness_mm = 200 } }
        });
        Wire("create_floor", new
        {
            name = "Floor", top_y_mm = 0, thickness_mm = 100,
            poly = new[] { new { x = 0, z = -100 }, new { x = 4000, z = -100 }, new { x = 4000, z = 3000 }, new { x = 0, z = 3000 } }
        });
        Wire("add_opening", new
        {
            name = "Win1", wall = "Wall", kind = "window", offset_mm = 1500, width = 1200, height = 1200, sill_mm = 900
        });
    }

    private static RunModule[] Row(params (string name, string kind, int width)[] cabinets) =>
        cabinets.Select(c => new RunModule { name = c.name, kind = c.kind, width_mm = c.width }).ToArray();

    private static object ThreeBase(int middleWidth = 600, bool withThird = true) => new
    {
        id = "Row", wall = "Wall", start_mm = 1200,
        modules = withThird
            ? Row(("B1", "base", 600), ("B2", "base", middleWidth), ("B3", "base", 600))
            : Row(("B1", "base", 600), ("B2", "base", middleWidth)),
    };

    private static Vector3 MinMm(string name)
    {
        var element = PartRegistry.GetAll().First(e => e.PartName == name);
        var min = element.GetVertices()[0];
        foreach (var v in element.GetVertices()) min = Vector3.Min(min, v);
        return min * 1000f;
    }

    private static Vector3 MaxMm(string name)
    {
        var element = PartRegistry.GetAll().First(e => e.PartName == name);
        var max = element.GetVertices()[0];
        foreach (var v in element.GetVertices()) max = Vector3.Max(max, v);
        return max * 1000f;
    }

    private static bool Exists(string name) => PartRegistry.GetAll().Any(e => e != null && e.PartName == name);

    [Test]
    public void ApplyRun_ThreeBaseCabinets_StandFlushAgainstTheRoomSideOfTheWall_InOneUndoStep()
    {
        int stepsBefore = CommandStack.UndoCount;

        var reply = Wire("apply_run", ThreeBase());

        Assert.AreEqual(stepsBefore + 1, CommandStack.UndoCount, "весь ряд — один шаг отмены");
        Assert.AreEqual("Row", reply["id"]!.Value<string>());
        Assert.AreEqual(3, ((JArray)reply["placements"]!).Count);
        var b1 = MinMm("B1");
        Assert.AreEqual(MinMm("Wall").x + 1200f, b1.x, 0.3f, "start_mm отсчитан от левого конца стены");
        Assert.AreEqual(0f, b1.y, 0.3f, "на полу");
        Assert.AreEqual(100f, b1.z, 0.3f, "вплотную к комнатной грани стены толщиной 200");
        Assert.AreEqual(b1.x + 600f, MinMm("B2").x, 0.3f);
        Assert.AreEqual(b1.x + 1200f, MinMm("B3").x, 0.3f);
        CollectionAssert.IsEmpty(reply["sceneViolationDelta"]!["added"]!.ToObject<string[]>(), "ряд под окном ничего не ломает");
    }

    [Test]
    public void ApplyRun_TheSameCallTwice_ChangesNothing_AndAddsNoUndoStep()
    {
        Wire("apply_run", ThreeBase());
        int steps = CommandStack.UndoCount;
        var before = new[] { MinMm("B1"), MinMm("B2"), MinMm("B3") };

        var again = Wire("apply_run", ThreeBase());

        Assert.AreEqual(steps, CommandStack.UndoCount, "идемпотентный повтор не должен ложиться в историю отмены");
        Assert.IsTrue(again["unchanged"]!.Value<bool>());
        Assert.IsFalse(again["applied"]!.Value<bool>());
        Assert.AreEqual(3, ((JArray)again["placements"]!).Count, "ответ по-прежнему показывает, где стоит ряд");
        var after = new[] { MinMm("B1"), MinMm("B2"), MinMm("B3") };
        for (int i = 0; i < 3; i++)
            Assert.AreEqual(0f, Vector3.Distance(before[i], after[i]), 0.01f, "ничего не сдвинулось");
        Assert.AreEqual(3, PartRegistry.GetAll().Count(e => e.PartName.StartsWith("B")), "и дублей нет");
    }

    [Test]
    public void ApplyRun_ChangingAWidth_ShiftsTheCabinetsAfterIt_InOneStep_AndUndoRestoresThem()
    {
        Wire("apply_run", ThreeBase());
        int steps = CommandStack.UndoCount;
        var b3Before = MinMm("B3");

        var reply = Wire("apply_run", ThreeBase(middleWidth: 900));

        Assert.AreEqual(steps + 1, CommandStack.UndoCount);
        Assert.IsNull(reply["unchanged"]);
        Assert.AreEqual(MinMm("Wall").x + 1200f, MinMm("B1").x, 0.3f, "до изменённого ничего не двигается");
        Assert.AreEqual(900f, InfoOf("B2").dimXMm, 0.3f, "ширина B2 действительно изменилась");
        Assert.AreEqual(b3Before.x + 300f, MinMm("B3").x, 0.3f, "B3 уехал ровно на прибавку ширины");
        Assert.AreEqual(100f, MinMm("B2").z, 0.3f, "и осталась у стены");

        _handler!.Handle(MakeReq("undo", new { }));

        Assert.AreEqual(600f, InfoOf("B2").dimXMm, 0.3f, "отмена вернула прежнюю ширину");
        Assert.AreEqual(b3Before.x, MinMm("B3").x, 0.3f, "и прежнее место соседа");
    }

    [Test]
    public void ApplyRun_ACabinetDroppedFromTheList_IsRemoved_AndUndoBringsItBack()
    {
        Wire("apply_run", ThreeBase());

        var reply = Wire("apply_run", ThreeBase(withThird: false));

        Assert.IsFalse(Exists("B3"), "шкаф, пропавший из списка, удалён");
        CollectionAssert.AreEqual(new[] { "B3" }, reply["deleted"]!.ToObject<string[]>());
        CollectionAssert.AreEqual(new[] { "B1", "B2" }, ProjectFloorplans.Find("run:Row")!.elements,
            "состав ряда в проекте обновлён: следующий вызов не вернёт B3 из забытого");

        _handler!.Handle(MakeReq("undo", new { }));

        Assert.IsTrue(Exists("B3"), "отмена возвращает удалённый шкаф");
        Assert.AreEqual(3, ProjectFloorplans.Find("run:Row")!.elements.Length);
    }

    [Test]
    public void ApplyRun_UndoOfTheFirstCall_RemovesTheRowAndItsRecord()
    {
        Wire("apply_run", ThreeBase());

        _handler!.Handle(MakeReq("undo", new { }));

        Assert.IsFalse(Exists("B1") || Exists("B2") || Exists("B3"));
        Assert.IsNull(ProjectFloorplans.Find("run:Row"), "запись о ряду откатывается вместе с шагом");
    }

    [Test]
    public void ApplyRun_ThreeCabinetsUnderTheWindow_FitTheCallAndByteBudget()
    {
        Wire("describe_scene", new { }, counted: true);
        var reply = Wire("apply_run", ThreeBase(), counted: true);

        TestContext.WriteLine($"SCENARIO apply_run: calls={_calls}, bytes={_bytes}");
        Assert.LessOrEqual(_calls, ScenarioCallBudget);
        Assert.LessOrEqual(_bytes, ScenarioByteBudget,
            $"ответы заняли {_bytes} байт при бюджете {ScenarioByteBudget}: apply_run обязан быть дешевле place");
        CollectionAssert.IsEmpty(reply["sceneViolationDelta"]!["added"]!.ToObject<string[]>());
    }

    [Test]
    public void ApplyRun_AWallCabinetOverTheWindow_IsRefused_NamingTheWindow_AndNothingIsCreated()
    {
        int steps = CommandStack.UndoCount;

        var message = Refusal("apply_run", new
        {
            id = "Up", wall = "Wall", start_mm = 1200,
            modules = Row(("W1", "wall", 600), ("W2", "wall", 600)),
        });

        StringAssert.Contains("Win1", message);
        StringAssert.Contains("start_mm", message, "отказ говорит, чем его обойти");
        Assert.IsFalse(Exists("W1") || Exists("W2"), "отказ атомарен: ни одного шкафа не осталось");
        Assert.AreEqual(steps, CommandStack.UndoCount);
        Assert.IsNull(ProjectFloorplans.Find("run:Up"));
    }

    [Test]
    public void ApplyRun_WallCabinetsBesideTheWindow_AreAccepted_AndHangHigh()
    {
        var reply = Wire("apply_run", new
        {
            id = "Up", wall = "Wall",
            modules = Row(("W1", "wall", 600), ("W2", "wall", 600)),
        });

        Assert.AreEqual(2, ((JArray)reply["placements"]!).Count);
        Assert.AreEqual(RunKindDefaults.WallHangsAboveFloorMm, MinMm("W1").y, 0.3f);
    }

    [Test]
    public void ApplyRun_ARunLongerThanTheWall_IsRefusedWithTheArithmetic()
    {
        var message = Refusal("apply_run", new
        {
            id = "Row", wall = "Wall",
            modules = Row(("B1", "base", 2500), ("B2", "base", 2500)),
        });

        StringAssert.Contains("longer", message);
        StringAssert.Contains("5000", message);
        Assert.IsFalse(Exists("B1"));
    }

    [Test]
    public void ApplyRun_ANameTakenByAForeignPart_IsRefused_AndTheForeignPartStaysPut()
    {
        Wire("create_elements", new { items = new[] { new { name = "B2", width = 300, height = 300, depth = 300, anchor_x_mm = 3000f, anchor_y_mm = 0f, anchor_z_mm = 1500f } } });
        var before = MinMm("B2");

        var message = Refusal("apply_run", ThreeBase());

        StringAssert.Contains("B2", message);
        Assert.AreEqual(0f, Vector3.Distance(before, MinMm("B2")), 0.01f, "чужую деталь ряд не присваивает и не двигает");
        Assert.IsFalse(Exists("B1"));
    }

    [Test]
    public void ApplyRun_DryRun_ShowsTheRow_ButLeavesNothingAndNoUndoStep()
    {
        int steps = CommandStack.UndoCount;

        var reply = Wire("apply_run", new
        {
            id = "Row", wall = "Wall", start_mm = 1200, dry_run = true,
            modules = Row(("B1", "base", 600), ("B2", "base", 600)),
        });

        Assert.IsTrue(reply["dryRun"]!.Value<bool>());
        Assert.AreEqual(2, ((JArray)reply["placements"]!).Count);
        Assert.IsFalse(Exists("B1"), "после dry_run сцена прежняя");
        Assert.AreEqual(steps, CommandStack.UndoCount);
        Assert.IsNull(ProjectFloorplans.Find("run:Row"));
    }

    [Test]
    public void ApplyRun_FromTheRight_StartsAtTheRightEnd_AndGrowsLeft()
    {
        Wire("apply_run", new
        {
            id = "Row", wall = "Wall", from = "right", start_mm = 100,
            modules = Row(("B1", "base", 600), ("B2", "base", 600)),
        });

        float rightEnd = MinMm("B1").x + 600f;
        Assert.AreEqual(MaxMm("Wall").x - 100f, rightEnd, 0.3f, "правый конец стены минус start_mm 100");
        Assert.AreEqual(MinMm("B1").x - 600f, MinMm("B2").x, 0.3f, "второй шкаф слева от первого");
    }

    [Test]
    public void ApplyRun_FromAPart_StartsRightAfterItsFarSide()
    {
        Wire("place", new
        {
            items = new object[]
            {
                new
                {
                    name = "Fridge", width = 600, height = 1800, depth = 600,
                    against = new[] { new { target = "Wall", face = "front" } },
                    align = new[] { new { target = "Wall", axis = "x", at = "min" } },
                },
            },
        });
        float fridgeRight = MinMm("Fridge").x + 600f;

        Wire("apply_run", new
        {
            id = "Row", wall = "Wall", from = "Fridge", gap_mm = 0,
            modules = Row(("B1", "base", 600)),
        });

        Assert.AreEqual(fridgeRight, MinMm("B1").x, 0.3f, "ряд продолжает холодильник");
    }

    [Test]
    public void ApplyRun_ALockedCabinetDroppedFromTheList_IsRefused_NotSilentlyKept()
    {
        Wire("apply_run", ThreeBase());
        _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "B3", locked = true } } }));
        int steps = CommandStack.UndoCount;

        var message = Refusal("apply_run", ThreeBase(withThird: false));

        StringAssert.Contains("LOCKED", message);
        Assert.IsTrue(Exists("B3"));
        Assert.AreEqual(steps, CommandStack.UndoCount);
    }

    [Test]
    public void ApplyRun_AWallAlongZ_StandsOnTheGivenRoomSide_AndGrowsAlongZ()
    {
        Wire("create_walls", new
        {
            segments = new[] { new { name = "SideWall", from_x = 0, from_z = 0, to_x = 0, to_z = 3000, kind = "bearing", height = 2700, thickness_mm = 200 } }
        });

        Wire("apply_run", new
        {
            id = "Side", wall = "SideWall", room_side = "right", from = "Wall",
            modules = Row(("S1", "base", 600), ("S2", "base", 600)),
        });

        Assert.AreEqual(100f, MinMm("S1").x, 0.3f, "вплотную к стороне стены, обращённой в комнату (x+)");
        Assert.AreEqual(100f, MinMm("S1").z, 0.3f, "ряд начат сразу за торцом стены Wall (её дальняя сторона по z)");
        Assert.AreEqual(MinMm("S1").z + 600f, MinMm("S2").z, 0.3f, "ряд растёт вдоль z");
    }

    [Test]
    public void ApplyRun_RoomSideOfTheWrongAxis_IsRefusedWithTheRightPair()
    {
        var message = Refusal("apply_run", new
        {
            id = "Row", wall = "Wall", room_side = "left",
            modules = Row(("B1", "base", 600)),
        });

        StringAssert.Contains("front|back", message);
    }

    [Test]
    public void ApplyRun_Tool_IsRegistered_AndInTheSimpleProfile()
    {
        var tool = McpToolRegistry.Tools.First(t => t.Name == "apply_run");

        Assert.AreEqual(McpToolKind.Write, tool.Kind);
        Assert.IsTrue(tool.Simple, "ряд шкафов — основной сценарий слабой модели");
        Assert.IsNotNull(tool.ParamsType!.GetField("verbosity"), "ref без verbosity запрещён сторожем McpVerbosityReplyTests");
    }
}
