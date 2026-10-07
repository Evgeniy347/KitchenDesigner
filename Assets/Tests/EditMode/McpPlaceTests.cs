using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>place: слабая модель говорит, ГДЕ деталь (на чём стоит, у какой грани, по центру чего), а не
/// считает координаты. Отказ называет, чего не хватает или что мешает, и ничего не применяет.</summary>
public class McpPlaceTests : McpTestFixture
{
    [SetUp]
    public void Setup() => CommandStack.Clear();

    [TearDown]
    public void Teardown() => CommandStack.Clear();

    private void KitchenWallWithWindow()
    {
        MakeSupportingFloor();
        MakeElement("Wall", new Vector3Int(6000, 2700, 100), new Vector3(0f, 1.35f, -0.05f));
        MakeElement("Win1", new Vector3Int(1200, 1200, 100), new Vector3(0.5f, 1.5f, -0.05f));
    }

    private static object Cabinet(string name, object[] against, object[]? align = null) => new
    {
        name, width = 600, height = 720, depth = 560, against, align = align ?? new object[0],
    };

    private static object[] WallFront() => new object[] { new { target = "Wall", face = "front" } };

    private static object CenterOnWindow() => new { target = "Win1", axis = "x", at = "center" };

    private McpResponse Place(params object[] items) => _handler!.Handle(MakeReq("place", new { items }));

    private KitchenElement Part(string name) => PartRegistry.GetAll().First(e => e.PartName == name);

    private static Vector3 MinMm(KitchenElement el)
    {
        var vertices = el.GetVertices();
        var min = vertices[0];
        foreach (var v in vertices) min = Vector3.Min(min, v);
        return min * 1000f;
    }

    private static string Error(McpResponse resp) => JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    [Test]
    public void ThreeCabinetsUnderTheWindow_NeedNoCoordinate_AndAreFlushWithEachOtherAndTheWall()
    {
        KitchenWallWithWindow();

        var resp = Place(
            Cabinet("Cab2", WallFront(), new object[] { CenterOnWindow() }),
            Cabinet("Cab1", new object[] { new { target = "Wall", face = "front" }, new { target = "Cab2", face = "left" } }),
            Cabinet("Cab3", new object[] { new { target = "Wall", face = "front" }, new { target = "Cab2", face = "right" } }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? Error(resp) : "");
        Assert.AreEqual(1, CommandStack.UndoCount, "батч place - один шаг истории");
        var cab2 = MinMm(Part("Cab2"));
        Assert.AreEqual(500f - 300f, cab2.x, 0.1f, "центр шкафа 2 на центре окна (x=500)");
        Assert.AreEqual(0f, cab2.y, 0.1f, "на полу");
        Assert.AreEqual(0f, cab2.z, 0.1f, "вплотную к лицевой грани стены");
        Assert.AreEqual(cab2.x - 600f, MinMm(Part("Cab1")).x, 0.1f, "шкаф 1 вплотную слева");
        Assert.AreEqual(cab2.x + 600f, MinMm(Part("Cab3")).x, 0.1f, "шкаф 3 вплотную справа");
        var reply = ReplyOf(resp);
        Assert.AreEqual(3, ((JArray)reply["placements"]!).Count);
        CollectionAssert.IsEmpty(reply["sceneViolationDelta"]!["added"]!.ToObject<string[]>(), "ничего не сломано");
        Assert.AreEqual("Floor", PlacementOf(resp, 0)["on"]!.Value<string>());
        CollectionAssert.Contains(((JArray)PlacementOf(resp, 0)["touches"]!).Select(t => t["n"]!.Value<string>() + "@" + t["face"]!.Value<string>()).ToArray(),
            "Cab1@left");
    }

    [Test]
    public void OneUndo_TakesTheWholePlaceBatchBack()
    {
        KitchenWallWithWindow();
        Place(Cabinet("Cab2", WallFront(), new object[] { CenterOnWindow() }),
            Cabinet("Cab1", new object[] { new { target = "Wall", face = "front" }, new { target = "Cab2", face = "left" } }));

        _handler!.Handle(MakeReq("undo", new { }));

        Assert.IsFalse(PartRegistry.GetAll().Any(e => e.PartName == "Cab1" || e.PartName == "Cab2"));
    }

    [Test]
    public void ANewPartWithoutX_IsRefusedWithTheMissingAxisAndTheFix_AndNothingIsCreated()
    {
        KitchenWallWithWindow();

        var resp = Place(Cabinet("Cab1", WallFront()));

        Assert.AreEqual("error", resp.type);
        var message = Error(resp);
        StringAssert.Contains("item 1 'Cab1': x is not determined", message);
        StringAssert.Contains("align", message, "отказ предлагает конкретный параметр");
        StringAssert.Contains("resend the WHOLE batch", message);
        Assert.IsFalse(PartRegistry.GetAll().Any(e => e.PartName == "Cab1"), "отказ ничего не создал");
        Assert.AreEqual(0, CommandStack.UndoCount);
    }

    [Test]
    public void ATypoInATargetName_IsRefusedWithTheClosestRealNames()
    {
        KitchenWallWithWindow();

        var resp = Place(Cabinet("Cab1", new object[] { new { target = "Wal", face = "front" } }, new object[] { CenterOnWindow() }));

        StringAssert.Contains("closest names: Wall", Error(resp));
    }

    [Test]
    public void ASpotThatOverlapsANeighbour_IsRefusedWithTheFaceToStandAgainstInstead()
    {
        KitchenWallWithWindow();
        Place(Cabinet("Cab2", WallFront(), new object[] { CenterOnWindow() }));

        var resp = Place(Cabinet("Cab1", WallFront(), new object[] { CenterOnWindow() }));

        Assert.AreEqual("error", resp.type);
        var message = Error(resp);
        StringAssert.Contains("overlaps 'Cab2'", message);
        StringAssert.Contains("against {target:'Cab2', face:", message);
        Assert.IsFalse(PartRegistry.GetAll().Any(e => e.PartName == "Cab1"));
    }

    [Test]
    public void TheBatchIsAtomic_ALaterBadItemTakesTheEarlierGoodOneBack()
    {
        KitchenWallWithWindow();

        var resp = Place(
            Cabinet("Good", WallFront(), new object[] { CenterOnWindow() }),
            Cabinet("Bad", new object[] { new { target = "Nowhere", face = "front" } }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("item 2 'Bad'", Error(resp));
        StringAssert.DoesNotContain("item 1", Error(resp), "первый элемент в порядке и в отказе не упоминается");
        Assert.IsFalse(PartRegistry.GetAll().Any(e => e.PartName == "Good"), "и он тоже не остался в сцене");
    }

    [Test]
    public void AnItemThatDependsOnARejectedOne_IsSkippedInsteadOfBlamingAMissingPart()
    {
        KitchenWallWithWindow();

        var resp = Place(
            Cabinet("Cab2", WallFront()),
            Cabinet("Cab1", new object[] { new { target = "Wall", face = "front" }, new { target = "Cab2", face = "left" } }));

        var message = Error(resp);
        StringAssert.Contains("item 2 'Cab1': skipped", message);
        StringAssert.Contains("'Cab2', which was rejected", message, "причина - отказ первого, а не «Cab2 не найден»");
    }

    [Test]
    public void PlacingAnExistingNameAgain_MovesThatPart_DoesNotDuplicateIt_AndUndoPutsItBack()
    {
        KitchenWallWithWindow();
        Place(Cabinet("Cab1", WallFront(), new object[] { CenterOnWindow() }));
        var before = MinMm(Part("Cab1"));
        int count = PartRegistry.GetAll().Count;

        var resp = Place(new { name = "Cab1", against = WallFront(), align = new object[] { new { target = "Win1", axis = "x", at = "min" } } });

        Assert.AreEqual("result", resp.type, resp.type == "error" ? Error(resp) : "");
        Assert.AreEqual(count, PartRegistry.GetAll().Count, "перестановка не плодит деталь");
        Assert.AreEqual(-100f, MinMm(Part("Cab1")).x, 0.1f, "левая кромка шкафа на левой кромке окна (x=-100)");
        Assert.AreEqual(before.z, MinMm(Part("Cab1")).z, 0.1f, "размер и высота остались прежними");
        _handler!.Handle(MakeReq("undo", new { }));
        Assert.AreEqual(before.x, MinMm(Part("Cab1")).x, 0.1f, "undo вернул на прежнее место");
    }

    [Test]
    public void PlacingAnExistingNameWithASize_IsRefused_ResizeBelongsToEditElements()
    {
        KitchenWallWithWindow();
        Place(Cabinet("Cab1", WallFront(), new object[] { CenterOnWindow() }));

        var resp = Place(Cabinet("Cab1", WallFront(), new object[] { CenterOnWindow() }));

        StringAssert.Contains("already exists", Error(resp));
        StringAssert.Contains("edit_elements", Error(resp));
    }

    [Test]
    public void DryRun_ReportsThePlacementsAndDelta_ButLeavesNothingBehind()
    {
        KitchenWallWithWindow();
        int count = PartRegistry.GetAll().Count;

        var resp = _handler!.Handle(MakeReq("place", new
        {
            dry_run = true,
            items = new[] { Cabinet("Cab2", WallFront(), new object[] { CenterOnWindow() }) },
        }));

        Assert.AreEqual("result", resp.type);
        var reply = ReplyOf(resp);
        Assert.IsTrue(reply["dryRun"]!.Value<bool>());
        Assert.IsFalse(reply["applied"]!.Value<bool>());
        Assert.AreEqual("Cab2", PlacementOf(resp)["name"]!.Value<string>());
        Assert.AreEqual(count, PartRegistry.GetAll().Count, "примерка не оставила деталь");
        Assert.AreEqual(0, CommandStack.UndoCount, "и шага истории");
        Assert.IsFalse(Object.FindObjectsByType<KitchenElement>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(e => e.PartName == "Cab2"), "и скрытого GameObject");
    }

    [Test]
    public void RotationBeforePlacing_UsesTheRotatedFootprint()
    {
        KitchenWallWithWindow();

        var resp = Place(new
        {
            name = "Turned", width = 600, height = 720, depth = 560, rot_y = 90f,
            against = WallFront(), align = new object[] { CenterOnWindow() },
        });

        Assert.AreEqual("result", resp.type, resp.type == "error" ? Error(resp) : "");
        var footprint = PlacementOf(resp)["footprintMm"]!.ToObject<float[]>()!;
        Assert.AreEqual(560f, footprint[0], 0.1f, "после rot_y=90 по X лежит глубина");
        Assert.AreEqual(600f, footprint[2], 0.1f);
        Assert.AreEqual(0f, MinMm(Part("Turned")).z, 0.1f, "вплотную к стене по повёрнутому следу");
    }

    [Test]
    public void OnAnotherPart_RestsOnItsTop_AndLiftHangsItAbove()
    {
        KitchenWallWithWindow();
        Place(Cabinet("Base", WallFront(), new object[] { CenterOnWindow() }));

        var resp = Place(new
        {
            name = "Hanging", width = 600, height = 400, depth = 300, on = "Base", lift_mm = 100f,
            against = WallFront(), align = new object[] { CenterOnWindow() },
        });

        Assert.AreEqual("result", resp.type, resp.type == "error" ? Error(resp) : "");
        Assert.AreEqual(720f + 100f, MinMm(Part("Hanging")).y, 0.1f);
    }

    [TestCase("window", "add_opening")]
    [TestCase("door", "add_opening")]
    [TestCase("wall", "create_walls")]
    [TestCase("floor", "create_floor")]
    public void TypesThatNeedAHostOrADeclaration_AreRefusedWithTheRightTool(string type, string tool)
    {
        KitchenWallWithWindow();

        var resp = Place(new { name = "X1", type, against = WallFront(), align = new object[] { CenterOnWindow() } });

        StringAssert.Contains(tool, Error(resp));
    }

    [Test]
    public void ALockedPart_IsNotMoved_TheRefusalSaysHowToUnlockIt()
    {
        KitchenWallWithWindow();
        Place(Cabinet("Cab1", WallFront(), new object[] { CenterOnWindow() }));
        Part("Cab1").Movable = false;

        var resp = Place(new { name = "Cab1", against = WallFront(), align = new object[] { new { target = "Win1", axis = "x", at = "min" } } });

        StringAssert.Contains("LOCKED", Error(resp));
        StringAssert.Contains("edit_elements", Error(resp));
    }

    [Test]
    public void ABadRef_IsRefusedByPlaceLikeByEveryOtherTool()
    {
        KitchenWallWithWindow();

        var resp = _handler!.Handle(MakeReq("place", new
        {
            @ref = "left-right",
            items = new[] { Cabinet("Cab1", WallFront(), new object[] { CenterOnWindow() }) },
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("left-bottom-back", Error(resp));
    }

    [Test]
    public void ThePlaceTool_IsOnTheSurface_AsABatchWithNoSingleName()
    {
        var tool = KitchenDesigner.Core.MCP.Contract.McpToolRegistry.Tools.FirstOrDefault(t => t.Name == "place");

        Assert.IsNotNull(tool);
        Assert.AreEqual(KitchenDesigner.Core.MCP.Contract.McpToolKind.Write, tool!.Kind);
        Assert.AreEqual("ParamsPlace", tool.ParamsType!.Name);
    }
}
