using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Слабая модель ставит, не видит последствий, удаляет и ставит заново. Ответ мутации — это
/// короткое «размещение»: где деталь (в её ref), какой у неё мировой след, на чём стоит, кого касается,
/// где зазоры, что с ней не так — и что ЭТОТ вызов сломал или починил в сцене (а не сколько нарушений
/// в сцене вообще, чужие старые не в счёт).</summary>
public class McpPlacementReplyTests : McpTestFixture
{
    private const int ThreeCabinetsByteBudget = 1500;

    private static readonly HashSet<string> ReplyKeys = new HashSet<string>
    {
        "ok", "ref", "placements", "omittedCount", "sceneViolationDelta", "dryRun", "applied", "axis", "spacingMm",
        "deleted", "matchedCount", "updatedCount", "steps", "doneCount", "undoAvailableCount", "redoAvailableCount",
    };

    private static readonly HashSet<string> PlacementKeys = new HashSet<string>
    {
        "name", "posMm", "footprintMm", "on", "touches", "gaps", "room", "level", "issues",
    };

    [SetUp]
    public void Setup()
    {
        CommandStack.Clear();
        ProjectRooms.Set(null);
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        ProjectRooms.Set(null);
    }

    private void FloorAndBackWall()
    {
        MakeElement("Floor", new Vector3Int(6000, 20, 6000), new Vector3(0f, -0.010f, 0f));
        MakeElement("Wall", new Vector3Int(6000, 2700, 100), new Vector3(0f, 1.35f, -0.05f));
    }

    private McpResponse CreateCabinets(params (string name, float x)[] cabinets)
    {
        var items = cabinets.Select(c => new
        {
            name = c.name, width = 600, height = 720, depth = 560,
            anchor_x_mm = c.x, anchor_y_mm = 0f, anchor_z_mm = 0f,
        }).ToArray();
        var resp = _handler!.Handle(MakeReq("create_elements", new { items }));
        Assert.AreEqual("result", resp.type, "create_elements отказал: " + ReplyOf(resp));
        return resp;
    }

    private static List<string> Faces(JToken placement, string list) =>
        (placement[list] as JArray ?? new JArray()).Select(t => t["n"]!.Value<string>() + "@" + t["face"]!.Value<string>()).ToList();

    [Test]
    public void CreateElements_ThreeCabinetsInARow_EachPlacementSaysWhatItStandsOnAndWhomItTouches()
    {
        FloorAndBackWall();

        var resp = CreateCabinets(("Cab1", 0f), ("Cab2", 600f), ("Cab3", 1200f));

        var reply = ReplyOf(resp);
        Assert.AreEqual(McpReference.DefaultName, reply["ref"]!.Value<string>());
        Assert.AreEqual(3, ((JArray)reply["placements"]!).Count);
        var middle = PlacementOf(resp, 1);
        Assert.AreEqual("Cab2", middle["name"]!.Value<string>());
        CollectionAssert.AreEqual(new[] { 600f, 0f, 0f }, middle["posMm"]!.ToObject<float[]>()!.Select(Mathf.Round).ToArray(),
            "pos в ref по умолчанию — те же числа, что модель прислала в anchor");
        CollectionAssert.AreEqual(new[] { 600f, 720f, 560f }, middle["footprintMm"]!.ToObject<float[]>()!.Select(Mathf.Round).ToArray(),
            "след — мировые размеры по X, Y, Z");
        Assert.AreEqual("Floor", middle["on"]!.Value<string>(), "стоит на полу");
        CollectionAssert.AreEquivalent(new[] { "Floor@bottom", "Wall@back", "Cab1@left", "Cab3@right" }, Faces(middle, "touches"),
            "середина ряда касается пола, стены и обоих соседей — это и есть «поставилось как надо»");
        Assert.IsNull(middle["gaps"], "зазоров нет: касающиеся грани в gaps не повторяются");
        foreach (var issue in (JArray)middle["issues"]!)
            StringAssert.DoesNotContain(": Cab", issue.Value<string>(), "в ряду встык никто ни с кем не пересекается");
    }

    [Test]
    public void CreateElements_ACabinetTwelveMillimetresFromItsNeighbour_ReportsTheGapOnThatFace()
    {
        FloorAndBackWall();
        CreateCabinets(("Cab1", 0f));

        var resp = CreateCabinets(("Cab2", 612f));

        var placement = PlacementOf(resp);
        var gap = ((JArray)placement["gaps"]!).Single(g => g["face"]!.Value<string>() == "left");
        Assert.AreEqual("Cab1", gap["n"]!.Value<string>());
        Assert.AreEqual(12f, gap["gapMm"]!.Value<float>(), 0.1f,
            "ровно та щель, из-за которой модель раньше выпадала в GAP-01 и гадала почему");
    }

    [Test]
    public void CreateElements_AFloatingCabinet_HasNoSupportAndReportsTheDropToTheFloor()
    {
        FloorAndBackWall();

        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Floaty", width = 600, height = 720, depth = 560, anchor_x_mm = 0f, anchor_y_mm = 100f, anchor_z_mm = 0f } }
        }));

        var placement = PlacementOf(resp);
        Assert.IsNull(placement["on"], "под ней ничего: поле on отсутствует");
        var drop = ((JArray)placement["gaps"]!).Single(g => g["face"]!.Value<string>() == "bottom");
        Assert.AreEqual("Floor", drop["n"]!.Value<string>());
        Assert.AreEqual(100f, drop["gapMm"]!.Value<float>(), 0.1f);
    }

    [Test]
    public void CreateElements_OverlappingAnExistingPart_NamesTheNeighbourAndTheDepthInIssues()
    {
        MakeElement("Existing", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Clash", width = 600, height = 400, depth = 18, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));

        var issues = ((JArray)PlacementOf(resp)["issues"]!).Select(i => i.Value<string>()!).ToList();
        Assert.AreEqual(1, issues.Count, "одно пересечение — одна строка");
        StringAssert.StartsWith("deep_penetration: Existing ", issues[0],
            "строка начинается серьёзностью, дальше сосед и глубина в мм: модель видит, с кем и насколько");
        StringAssert.EndsWith("mm", issues[0]);
    }

    [Test]
    public void SceneViolationDelta_CountsOnlyWhatThisCallBrokeInASceneThatAlreadyHadViolations()
    {
        MakeElement("OldA", new Vector3Int(1000, 1000, 1000), new Vector3(10f, 0f, 0f));
        MakeElement("OldB", new Vector3Int(1000, 1000, 1000), new Vector3(10f, 0f, 0f));
        MakeElement("Target", new Vector3Int(500, 400, 18), new Vector3(20f, 0f, 0f));
        var preexisting = HasViolation("OldA") && HasViolation("OldB");
        Assume.That(preexisting, "предусловие: в сцене уже есть нарушения, не от нашего вызова");

        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Newcomer", width = 500, height = 400, depth = 18, anchor_x_mm = 19750f, anchor_y_mm = -200f, anchor_z_mm = -9f } }
        }));

        var delta = ReplyOf(resp)["sceneViolationDelta"]!;
        CollectionAssert.AreEquivalent(new[] { "Newcomer", "Target" }, delta["added"]!.ToObject<string[]>(),
            "добавлены ровно две детали, пересёкшиеся по нашей вине; OldA/OldB и так были нарушителями и в разницу не входят");
        CollectionAssert.IsEmpty(delta["removed"]!.ToObject<string[]>());
    }

    [Test]
    public void SceneViolationDelta_AfterFixingAnOverlap_ListsTheFixedPartsAsRemoved()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.2f, 0f, 0f));
        Assume.That(HasViolation("A"), "предусловие: пересечение есть");

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "B", anchor_x_mm = 5000f } }
        }));

        var delta = ReplyOf(resp)["sceneViolationDelta"]!;
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, delta["removed"]!.ToObject<string[]>(),
            "правка развела пару: обе детали перестали быть нарушителями по её вине");
        CollectionAssert.IsEmpty(delta["added"]!.ToObject<string[]>());
    }

    private bool HasViolation(string name)
    {
        var violating = ReplyOf(_handler!.Handle(MakeReq("get_violations", new { })))["violations"] as JArray;
        return violating != null && violating.Any(v => v["name"]!.Value<string>() == name);
    }

    [Test]
    public void EditElements_RotatedNinetyDegrees_ReportsTheWorldFootprint_NotTheLocalSize()
    {
        MakeElement("Turned", new Vector3Int(800, 720, 560), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Turned", rot_y = 90f } }
        }));

        CollectionAssert.AreEqual(new[] { 560f, 720f, 800f },
            PlacementOf(resp)["footprintMm"]!.ToObject<float[]>()!.Select(Mathf.Round).ToArray(),
            "после rot_y=90 ширина 800 лежит вдоль Z: ответ называет мировой след, и модели не нужно пересчитывать его самой");
    }

    [Test]
    public void EditElements_DryRun_ReportsTheResultingPlacementAndDelta_ButChangesNothing()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            dry_run = true,
            ops = new[] { new { name = "A", anchor_x_mm = 1900f } }
        }));

        var reply = ReplyOf(resp);
        Assert.IsTrue(reply["dryRun"]!.Value<bool>());
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, reply["sceneViolationDelta"]!["added"]!.ToObject<string[]>());
        Assert.AreEqual(0f, a.transform.position.x, 1e-6f, "dry-run вернул сцену как была");
        Assert.AreEqual(0, CommandStack.UndoCount, "и в историю ничего не положил");
    }

    [Test]
    public void DeleteElements_ReportsTheDeletedNames_AndTheViolationsThatWentAwayWithThem()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("delete_elements", new { names = new[] { "B" } }));

        var reply = ReplyOf(resp);
        CollectionAssert.AreEqual(new[] { "B" }, reply["deleted"]!.ToObject<string[]>());
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, reply["sceneViolationDelta"]!["removed"]!.ToObject<string[]>(),
            "удаление одной детали из пересекающейся пары снимает нарушение с обеих");
        Assert.IsNull(reply["placements"], "удалённому нечего размещать");
    }

    [Test]
    public void BulkTools_ReturnCountsPlacementsAndTheDelta()
    {
        MakeElement("S1", new Vector3Int(600, 400, 18), Vector3.zero);
        MakeElement("S2", new Vector3Int(600, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("move", new { selector = "S*", dy = 100f }));

        var reply = ReplyOf(resp);
        Assert.AreEqual(2, reply["matchedCount"]!.Value<int>());
        Assert.AreEqual(2, reply["updatedCount"]!.Value<int>());
        Assert.AreEqual(2, ((JArray)reply["placements"]!).Count);
        Assert.IsNotNull(reply["sceneViolationDelta"]);
        Assert.IsNull(reply["sceneViolationCount"]);
    }

    [Test]
    public void PlacementsAreCapped_AndTheReplyAdmitsHowManyWereLeftOut()
    {
        var items = Enumerable.Range(0, McpMutationReport.MaxPlacements + 3).Select(i => new
        {
            name = "Shelf" + i, width = 300, height = 18, depth = 300,
            anchor_x_mm = i * 400f, anchor_y_mm = 0f, anchor_z_mm = 5000f,
        }).ToArray();

        var resp = _handler!.Handle(MakeReq("create_elements", new { items }));

        var reply = ReplyOf(resp);
        Assert.AreEqual(McpMutationReport.MaxPlacements, ((JArray)reply["placements"]!).Count);
        Assert.AreEqual(3, reply["omittedCount"]!.Value<int>(),
            "обрезанный ответ обязан сказать, что он обрезан: иначе пропуск принимают за «всё поставилось»");
    }

    [Test]
    public void EveryMutationReply_UsesOnlyTheDocumentedKeys_AndNeverTheOldCentreOrWholeSceneCount()
    {
        FloorAndBackWall();
        var replies = new List<McpResponse>
        {
            CreateCabinets(("K1", 0f), ("K2", 700f)),
            _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "K1", anchor_x_mm = 10f } } })),
            _handler.Handle(MakeReq("clone_elements", new { ops = new[] { new { name = "K1", count = 1, offset_x_mm = 1400f } } })),
            _handler.Handle(MakeReq("align_elements", new { ops = new[] { new { name = "K2", face = "left", target = "K1", target_face = "right" } } })),
            _handler.Handle(MakeReq("delete_elements", new { names = new[] { "K1_1" } })),
        };

        foreach (var resp in replies)
        {
            Assert.AreEqual("result", resp.type, ReplyOf(resp).ToString());
            var reply = ReplyOf(resp);
            foreach (var key in reply.Properties().Select(p => p.Name))
                Assert.IsTrue(ReplyKeys.Contains(key), $"ключ «{key}» ответа мутации не описан в guide");
            Assert.IsNull(reply["elements"], "полный ElementInfo остался только у get_elements");
            foreach (var placement in (reply["placements"] as JArray) ?? new JArray())
                foreach (var key in ((JObject)placement).Properties().Select(p => p.Name))
                    Assert.IsTrue(PlacementKeys.Contains(key), $"ключ «{key}» размещения не описан в guide");
        }
    }

    [Test]
    public void CreateElements_ThreeCabinetsOnTheWire_FitTheByteBudget_AndNothingHasTheOldFatShape()
    {
        FloorAndBackWall();
        var args = JObject.Parse(@"{""items"":[
            {""name"":""Cab1"",""width"":600,""height"":720,""depth"":560,""anchor_x_mm"":0,""anchor_y_mm"":0,""anchor_z_mm"":0},
            {""name"":""Cab2"",""width"":600,""height"":720,""depth"":560,""anchor_x_mm"":600,""anchor_y_mm"":0,""anchor_z_mm"":0},
            {""name"":""Cab3"",""width"":600,""height"":720,""depth"":560,""anchor_x_mm"":1200,""anchor_y_mm"":0,""anchor_z_mm"":0}]}");
        var tool = McpToolRegistry.Tools.First(t => t.Name == "create_elements");

        var content = new McpToolCall(_handler!.Handle).Invoke(tool, args, "bytes");

        var text = content["content"]![0]!["text"]!.Value<string>()!;
        int bytes = System.Text.Encoding.UTF8.GetByteCount(text);
        Assert.LessOrEqual(bytes, ThreeCabinetsByteBudget,
            $"ответ create_elements на три шкафа занял {bytes} байт при бюджете {ThreeCabinetsByteBudget}: до рефакторинга полный "
            + "ElementInfo на каждый (около 1,3 КБ с отступами) давал 4,2 КБ. Рост ответа жжёт контекст слабой модели");
        StringAssert.DoesNotContain("\n", text, "на проводе JSON без отступов: каждая строка отступов — байты контекста впустую");
        StringAssert.DoesNotContain("aabbMin", text);
        StringAssert.DoesNotContain("effectiveDim", text);
        StringAssert.DoesNotContain("posXMm", text);
    }
}
