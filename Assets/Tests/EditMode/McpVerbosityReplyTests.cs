using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>verbosity: terse (по умолчанию) отвечает размещением и разницей нарушений, full добавляет полный
/// ElementInfo ровно тех деталей, что изменил вызов. Байты считаются на проводе (McpToolCall): их видит агент.</summary>
public class McpVerbosityReplyTests : McpTestFixture
{
    private const int FullMustBeAtLeastTimesTerse = 3;

    [SetUp]
    public void Setup()
    {
        CommandStack.Clear();
        ProjectRooms.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        ProjectRooms.Reset();
    }

    private void FloorAndBackWall()
    {
        MakeSupportingFloor();
        MakeElement("Wall", new Vector3Int(6000, 2700, 100), new Vector3(0f, 1.35f, -0.05f));
    }

    private static object ThreeCabinets(string verbosity) => new
    {
        verbosity,
        items = new[]
        {
            new { name = "Cab1", width = 600, height = 720, depth = 560, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            new { name = "Cab2", width = 600, height = 720, depth = 560, anchor_x_mm = 600f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            new { name = "Cab3", width = 600, height = 720, depth = 560, anchor_x_mm = 1200f, anchor_y_mm = 0f, anchor_z_mm = 0f },
        }
    };

    private int WireBytes(string tool, object args)
    {
        var def = McpToolRegistry.Tools.First(t => t.Name == tool);
        var content = new McpToolCall(_handler!.Handle).Invoke(def, JObject.FromObject(args), "verbosity");
        Assert.IsNull(content["isError"], tool + " отказал: " + content["content"]![0]!["text"]);
        return Encoding.UTF8.GetByteCount(content["content"]![0]!["text"]!.Value<string>()!);
    }

    private JObject Reply(string method, object args)
    {
        var resp = _handler!.Handle(MakeReq(method, args));
        Assert.AreEqual("result", resp.type, method + " отказал: " + ReplyOf(resp));
        return ReplyOf(resp);
    }

    [Test]
    public void ByDefault_TheReplyIsTerse_NoElementInfoAtAll()
    {
        FloorAndBackWall();

        var reply = Reply("create_elements", ThreeCabinets("terse"));

        Assert.IsNull(reply["elements"], "terse = размещения и разница нарушений, без ElementInfo");
        Assert.AreEqual(3, ((JArray)reply["placements"]!).Count);
    }

    [Test]
    public void NoVerbosityAtAll_MeansTerse()
    {
        FloorAndBackWall();

        var reply = Reply("create_elements", new
        {
            items = new[] { new { name = "Cab1", width = 600, height = 720, depth = 560, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        });

        Assert.IsNull(reply["elements"]);
    }

    [Test]
    public void Full_AddsTheCompleteElementInfoOfEveryChangedPart_NextToThePlacements()
    {
        FloorAndBackWall();

        var reply = Reply("create_elements", ThreeCabinets("full"));

        var elements = (JArray)reply["elements"]!;
        var placements = (JArray)reply["placements"]!;
        Assert.AreEqual(placements.Count, elements.Count, "полный ElementInfo на каждую изменённую деталь");
        for (int i = 0; i < elements.Count; i++)
        {
            Assert.AreEqual(placements[i]["name"]!.Value<string>(), elements[i]["name"]!.Value<string>(), "тот же порядок, что у размещений");
            Assert.IsNotNull(elements[i]["aabbMinXMm"], "это настоящий ElementInfo, а не сокращение");
            CollectionAssert.AreEqual(placements[i]["posMm"]!.ToObject<float[]>(), elements[i]["posMm"]!.ToObject<float[]>(),
                "позиции в обоих блоках - в одном и том же ref");
        }
        Assert.IsNotNull(reply["placements"], "full НЕ отменяет размещения: он их дополняет");
        Assert.IsNotNull(reply["sceneViolationDelta"]);
    }

    [Test]
    public void Full_ThreeCabinetsOnTheWire_CostManyTimesTheTerseBytes()
    {
        FloorAndBackWall();

        int terse = WireBytes("create_elements", ThreeCabinets("terse"));
        _handler!.Handle(MakeReq("undo", new { }));
        int full = WireBytes("create_elements", ThreeCabinets("full"));

        TestContext.WriteLine($"VERBOSITY create_elements x3: terse={terse} bytes, full={full} bytes, ratio={(float)full / terse:0.0}");
        Assert.GreaterOrEqual(full, terse * FullMustBeAtLeastTimesTerse,
            $"full ({full} Б) должен стоить минимум в {FullMustBeAtLeastTimesTerse} раза больше terse ({terse} Б): иначе терс не экономит ничего");
    }

    [Test]
    public void Full_WorksOnPlace_Edit_Clone_Align_AndTheBulkTools()
    {
        FloorAndBackWall();
        Reply("create_elements", ThreeCabinets("terse"));

        var placed = Reply("place", new
        {
            verbosity = "full",
            items = new[]
            {
                new
                {
                    name = "Cab4", width = 600, height = 720, depth = 560,
                    against = new[] { new { target = "Wall", face = "front" }, new { target = "Cab3", face = "right" } },
                }
            }
        });
        var edited = Reply("edit_elements", new { verbosity = "full", ops = new[] { new { name = "Cab1", anchor_y_mm = 0f } } });
        var cloned = Reply("clone_elements", new { verbosity = "full", ops = new[] { new { name = "Cab1", count = 1, offset_z_mm = 3000f } } });
        var aligned = Reply("align_elements", new
        {
            verbosity = "full",
            ops = new[] { new { name = "Cab4", face = "left", target = "Cab3", target_face = "right" } }
        });
        var moved = Reply("move", new { verbosity = "full", selector = "Cab2", dx = 0f, dy = 0f, dz = 0f });
        var resized = Reply("set_attr", new { verbosity = "full", selector = "Cab2", height = 700 });

        foreach (var (tool, reply) in new[] { ("place", placed), ("edit_elements", edited), ("clone_elements", cloned),
                     ("align_elements", aligned), ("move", moved), ("set_attr", resized) })
            Assert.IsNotNull(reply["elements"], tool + " обязан понимать verbosity:\"full\"");
    }

    [Test]
    public void Full_OnADryRun_StillRevertsEverything()
    {
        FloorAndBackWall();

        var reply = Reply("create_elements", new
        {
            dry_run = true,
            verbosity = "full",
            items = new[] { new { name = "Ghost", width = 600, height = 720, depth = 560, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        });

        Assert.IsTrue(reply["dryRun"]!.Value<bool>());
        Assert.AreEqual(1, ((JArray)reply["elements"]!).Count);
        Assert.IsFalse(PartRegistry.GetAll().Any(e => e.PartName == "Ghost"), "dry_run убрал деталь, а полное описание о ней осталось в ответе");
        Assert.AreEqual(0, CommandStack.UndoCount);
    }

    [Test]
    public void Full_ReportsPositionsInTheRequestedRef()
    {
        FloorAndBackWall();

        var reply = Reply("create_elements", new
        {
            verbosity = "full",
            @ref = "center-bottom-back",
            items = new[] { new { name = "Cab1", width = 600, height = 720, depth = 560, anchor_x_mm = 300f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        });

        CollectionAssert.AreEqual(new[] { 300f, 0f, 0f }, ((JArray)reply["elements"]!)[0]["posMm"]!.ToObject<float[]>()!.Select(Mathf.Round).ToArray());
    }

    [Test]
    public void CreateElementsFull_ElementsAreCappedLikePlacements()
    {
        var items = Enumerable.Range(0, McpMutationReport.MaxPlacements + 3).Select(i => new
        {
            name = "Shelf" + i, width = 300, height = 18, depth = 300,
            anchor_x_mm = i * 400f, anchor_y_mm = 0f, anchor_z_mm = 5000f,
        }).ToArray();

        var reply = Reply("create_elements", new { verbosity = "full", items });

        Assert.AreEqual(McpMutationReport.MaxPlacements, ((JArray)reply["elements"]!).Count);
        Assert.AreEqual(3, reply["omittedCount"]!.Value<int>(), "обрезка названа вслух, как у размещений");
    }

    [Test]
    public void AnUnknownVerbosity_IsRefusedWithTheValidWords_AndNothingIsApplied()
    {
        FloorAndBackWall();

        var resp = _handler!.Handle(MakeReq("create_elements", ThreeCabinets("chatty")));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("terse | full", JObject.Parse(McpJson.Serialize(resp.data!))["message"]!.Value<string>());
        Assert.IsFalse(PartRegistry.GetAll().Any(e => e.PartName == "Cab1"), "отказ до первой мутации");
    }

    [Test]
    public void EveryMutatingTool_ThatTakesARef_AlsoTakesAVerbosity()
    {
        var withRef = McpToolRegistry.Tools
            .Where(t => t.Kind != McpToolKind.Read && t.ParamsType != null)
            .Where(t => t.ParamsType!.GetField("ref", BindingFlags.Public | BindingFlags.Instance) != null)
            .ToList();

        Assert.IsNotEmpty(withRef, "сторож ослеп: ни у одного изменяющего инструмента не нашлось ref");
        var missing = withRef.Where(t => t.ParamsType!.GetField("verbosity") == null).Select(t => t.Name).ToList();
        CollectionAssert.IsEmpty(missing, "ref говорит, что ответ несёт размещения, а значит у него есть terse и full: " + string.Join(", ", missing));
    }
}
