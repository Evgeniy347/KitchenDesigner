using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>dry_run у create_elements / clone_elements / align_elements: показать, что получится и что
/// сломается, и вернуть сцену как была - без детали, без скрытого GameObject и без шага истории.</summary>
public class McpDryRunTests : McpTestFixture
{
    [SetUp]
    public void Setup() => CommandStack.Clear();

    [TearDown]
    public void Teardown() => CommandStack.Clear();

    private static bool AnyElementNamed(string prefix) =>
        Object.FindObjectsByType<KitchenElement>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(e => e.PartName.StartsWith(prefix));

    [Test]
    public void CreateElements_DryRun_ReportsThePlacementAndWhatItWouldBreak_ThenLeavesNothing()
    {
        MakeSupportingFloor();
        MakeElement("Stand", new Vector3Int(1000, 1000, 1000), new Vector3(0f, 0.5f, 0f));

        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            dry_run = true,
            items = new[] { new { name = "Clash", width = 600, height = 400, depth = 18, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var reply = ReplyOf(resp);
        Assert.IsTrue(reply["dryRun"]!.Value<bool>());
        Assert.IsFalse(reply["applied"]!.Value<bool>());
        StringAssert.StartsWith("deep_penetration: Stand", PlacementOf(resp)["issues"]![0]!.Value<string>());
        CollectionAssert.AreEquivalent(new[] { "Clash", "Stand" }, reply["sceneViolationDelta"]!["added"]!.ToObject<string[]>());
        Assert.IsFalse(AnyElementNamed("Clash"), "ни активной, ни скрытой детали не осталось");
        Assert.AreEqual(0, CommandStack.UndoCount, "и шага истории");
    }

    [Test]
    public void CreateElements_DryRun_ThenTheRealCall_StillCreatesThePart()
    {
        var items = new[] { new { name = "Shelf", width = 600, height = 400, depth = 18, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } };
        _handler!.Handle(MakeReq("create_elements", new { dry_run = true, items }));

        var real = _handler.Handle(MakeReq("create_elements", new { items }));

        Assert.AreEqual("result", real.type, "имя после примерки свободно - «already exists» было бы утечкой");
        Assert.IsTrue(PartRegistry.GetAll().Any(e => e.PartName == "Shelf"));
    }

    [Test]
    public void CloneElements_DryRun_ReportsTheCopies_ButCreatesNone()
    {
        MakeElement("Shelf", new Vector3Int(600, 18, 400), new Vector3(0f, 1f, 0f));
        int count = PartRegistry.GetAll().Count;

        var resp = _handler!.Handle(MakeReq("clone_elements", new
        {
            dry_run = true,
            ops = new[] { new { name = "Shelf", count = 2, offset_y_mm = 300f } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(2, ((JArray)ReplyOf(resp)["placements"]!).Count);
        Assert.IsTrue(ReplyOf(resp)["dryRun"]!.Value<bool>());
        Assert.AreEqual(count, PartRegistry.GetAll().Count);
        Assert.IsFalse(AnyElementNamed("Shelf_"), "и скрытых копий нет");
        Assert.AreEqual(0, CommandStack.UndoCount);
    }

    [Test]
    public void AlignElements_DryRun_ReportsTheResult_ButMovesNothing()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("align_elements", new
        {
            dry_run = true,
            ops = new[] { new { name = "A", face = "left", target = "B", target_face = "right" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("A", PlacementOf(resp)["name"]!.Value<string>());
        Assert.IsTrue(ReplyOf(resp)["dryRun"]!.Value<bool>());
        Assert.AreEqual(0f, a.transform.position.x, 1e-6f, "в сцене A на месте");
        Assert.AreEqual(0, CommandStack.UndoCount);
    }

    [Test]
    public void ARejectedClone_LeavesNoHalfBuiltCopiesBehind()
    {
        MakeElement("Shelf", new Vector3Int(600, 18, 400));
        int count = PartRegistry.GetAll().Count;

        var resp = _handler!.Handle(MakeReq("clone_elements", new
        {
            ops = new[] { new { name = "Shelf", count = 2 }, new { name = "Missing", count = 1 } }
        }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(count, PartRegistry.GetAll().Count, "копии первого op не должны пережить отказ батча");
        Assert.IsFalse(AnyElementNamed("Shelf_"));
    }
}
