using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>describe_scene глазами слабой модели: одним вызовом и одним коротким текстом узнать, что в сцене,
/// где стоит и что не так - и не получить ни знака сверх max_chars. Тексты проверяются на проводе (McpToolCall),
/// потому что агент видит именно их.</summary>
public class McpDescribeSceneTests : McpTestFixture
{
    private const int ModuleCount = 4;
    private const int PartsPerModule = 8;
    private const int LooseCabinets = 16;

    [SetUp]
    public void Setup()
    {
        GroupManager.Clear();
        CommandStack.Clear();
        ProjectRooms.Reset();
        LevelRegistry.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        GroupManager.Clear();
        CommandStack.Clear();
        ProjectRooms.Reset();
        LevelRegistry.Reset();
    }

    private (bool isError, string text) Ask(string tool, object args)
    {
        var def = McpToolRegistry.Tools.First(t => t.Name == tool);
        var content = new McpToolCall(_handler!.Handle).Invoke(def, JObject.FromObject(args), "describe");
        return (content["isError"] != null, content["content"]![0]!["text"]!.Value<string>()!);
    }

    private string Describe(object? args = null)
    {
        var (isError, text) = Ask("describe_scene", args ?? new { });
        Assert.IsFalse(isError, "describe_scene отказал: " + text);
        return text;
    }

    private KitchenElement Cabinet(string name, float xMm, float zMm = 0f) =>
        MakeElement(name, new Vector3Int(600, 720, 560), new Vector3((xMm + 300f) / 1000f, 0.36f, (zMm + 280f) / 1000f));

    private KitchenElement Slab(string name, float xMm, float zMm) =>
        MakeElement(name, new Vector3Int(100, 720, 560), new Vector3((xMm + 50f) / 1000f, 0.36f, (zMm + 280f) / 1000f));

    private void FloorAndWall()
    {
        MakeSupportingFloor();
        MakeElement("Wall", new Vector3Int(8000, 2700, 100), new Vector3(0f, 1.35f, -0.05f));
    }

    private void FiftyPartScene()
    {
        FloorAndWall();
        for (int m = 1; m <= ModuleCount; m++)
        {
            var module = GroupManager.Create("Module" + m);
            for (int k = 0; k < PartsPerModule; k++)
                GroupManager.AddTo(module, Slab($"M{m}_Part{k + 1}", m * 1500f + k * 100f, 2000f));
        }
        for (int i = 1; i <= LooseCabinets; i++) Cabinet("Cab" + i.ToString("00"), i * 600f);
    }

    [Test]
    public void FiftyPartScene_FitsTheDefaultBudget_AndEveryModuleNameIsInIt()
    {
        FiftyPartScene();
        Assume.That(PartRegistry.GetAll().Count, Is.EqualTo(50), "предусловие: в сцене ровно пятьдесят деталей");

        var text = Describe();

        TestContext.WriteLine($"DIGEST 50 parts: {text.Length} chars\n{text}");
        Assert.LessOrEqual(text.Length, SceneDigestText.DefaultMaxChars, "дайджест не длиннее бюджета по умолчанию");
        for (int m = 1; m <= ModuleCount; m++)
            StringAssert.Contains($"Module{m} module({PartsPerModule})", text, "модуль назван в первом же экране: он важнее одиночных деталей");
        StringAssert.StartsWith($"scene: 50 parts, {ModuleCount} modules, {LooseCabinets + 2} loose", text);
        StringAssert.Contains("more, use scope", text, "пятидесяти деталей на 1500 знаков не хватает: обрезка названа вслух");
    }

    [Test]
    public void TheDigest_IsPlainText_NotAJsonStringWithEscapedNewlines()
    {
        FloorAndWall();

        var text = Describe();

        StringAssert.DoesNotStartWith("\"", text);
        StringAssert.DoesNotContain("\\n", text, "перевод строки настоящий: JSON-строка тратила бы знаки бюджета на экранирование");
        Assert.Greater(text.Split('\n').Length, 2);
    }

    [Test]
    public void TwoCallsOnTheSameScene_ReturnTheSameText()
    {
        FiftyPartScene();

        Assert.AreEqual(Describe(), Describe(), "порядок строк и числа - функция сцены, а не обхода");
        Assert.AreEqual(Describe(new { max_chars = 600 }), Describe(new { max_chars = 600 }));
    }

    [TestCase(200)]
    [TestCase(450)]
    [TestCase(1000)]
    [TestCase(1500)]
    public void TheDigest_NeverExceedsMaxChars(int maxChars)
    {
        FiftyPartScene();

        var text = Describe(new { max_chars = maxChars });

        Assert.LessOrEqual(text.Length, maxChars);
    }

    [Test]
    public void MaxChars_OutOfRange_IsRefusedWithTheRange()
    {
        FloorAndWall();

        var (isError, text) = Ask("describe_scene", new { max_chars = 50 });

        Assert.IsTrue(isError);
        StringAssert.Contains($"{SceneDigestText.MinMaxChars}..{SceneDigestText.MaxMaxChars}", text);
    }

    [Test]
    public void ScopeAModule_ListsItsPartsOneByOne_AndLeavesTheOtherModulesOut()
    {
        FiftyPartScene();

        var text = Describe(new { scope = "Module2" });

        StringAssert.StartsWith($"scope module Module2: {PartsPerModule} parts", text);
        for (int k = 1; k <= PartsPerModule; k++) StringAssert.Contains($"M2_Part{k} board", text);
        StringAssert.DoesNotContain("M1_Part1", text);
        StringAssert.DoesNotContain("Module3", text);
        StringAssert.DoesNotContain("more, use scope", text, "восемь строк по сто знаков - в бюджет по умолчанию помещаются");
    }

    [Test]
    public void ScopeAModule_WorksWithTheModulePrefixToo()
    {
        FiftyPartScene();

        StringAssert.StartsWith("scope module Module2:", Describe(new { scope = "module:Module2" }));
    }

    [Test]
    public void ScopeASelector_ListsTheMatchedPartsOnly()
    {
        FiftyPartScene();

        var text = Describe(new { scope = "Cab0*" });

        StringAssert.StartsWith("scope selector \"Cab0*\": 9 parts", text);
        StringAssert.Contains("Cab01 board", text);
        StringAssert.DoesNotContain("Cab10 ", text);
    }

    [Test]
    public void ScopeARoom_KeepsOnlyWhatStandsInsideIt_ModulesStayModules()
    {
        FiftyPartScene();
        ProjectRooms.Set(new[]
        {
            new RoomData { id = "Kitchen", floor = "Floor", polygonXZ = new[] { -1000, -500, 12000, -500, 12000, 1500, -1000, 1500 } }
        });

        var text = Describe(new { scope = "room:Kitchen" });

        StringAssert.StartsWith("scope room Kitchen:", text);
        StringAssert.Contains("Cab01 board", text);
        StringAssert.DoesNotContain("Module1", text, "модули стоят в z=2000, за полигоном комнаты");
    }

    [Test]
    public void ScopeNothing_IsRefusedWithTheRealModuleAndRoomNames()
    {
        FiftyPartScene();
        ProjectRooms.Set(new[] { new RoomData { id = "Kitchen", polygonXZ = new[] { 0, 0, 1000, 0, 1000, 1000, 0, 1000 } } });

        var (isError, text) = Ask("describe_scene", new { scope = "Modul1" });

        Assert.IsTrue(isError);
        StringAssert.Contains("closest: Module1", text);
        StringAssert.Contains("Rooms: Kitchen", text);
    }

    [Test]
    public void ScopeAnUnknownRoom_IsRefusedToo()
    {
        FloorAndWall();

        var (isError, text) = Ask("describe_scene", new { scope = "room:Garage" });

        Assert.IsTrue(isError);
        StringAssert.Contains("matches no module, room or part", text);
    }

    [Test]
    public void ARelationInTheLine_UsesTheVeryWordsOfThePlacementBlock()
    {
        FloorAndWall();
        var created = ReplyOf(_handler!.Handle(MakeReq("create_elements", new
        {
            items = new[]
            {
                new { name = "Cab1", width = 600, height = 720, depth = 560, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
                new { name = "Cab2", width = 600, height = 720, depth = 560, anchor_x_mm = 600f, anchor_y_mm = 0f, anchor_z_mm = 0f },
                new { name = "Cab3", width = 600, height = 720, depth = 560, anchor_x_mm = 1212f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            }
        })));
        var placement = (JObject)((JArray)created["placements"]!)[1]!;

        var line = Describe(new { scope = "Cab2" }).Split('\n').Single(l => l.StartsWith("Cab2 "));

        foreach (var touch in (JArray)placement["touches"]!)
        {
            string face = touch["face"]!.Value<string>()!, other = touch["n"]!.Value<string>()!;
            if (face == "bottom" && other == placement["on"]!.Value<string>()) continue;
            StringAssert.Contains($"{face}→{other}", line, "касание названо словами placement: грань→сосед");
        }
        Assert.IsNotNull(placement["touches"], "предусловие: у середины ряда есть касания");
        StringAssert.Contains("on " + placement["on"]!.Value<string>(), line);
        var pos = placement["posMm"]!.ToObject<float[]>()!.Select(Mathf.Round).ToArray();
        StringAssert.Contains($"@({pos[0]},{pos[1]},{pos[2]})", line, "позиция - те же числа, что posMm размещения");
        var size = placement["footprintMm"]!.ToObject<float[]>()!.Select(Mathf.Round).ToArray();
        StringAssert.Contains($"{size[0]}×{size[1]}×{size[2]}", line, "размер - мировой след размещения");
        StringAssert.EndsWith("; ok", line);

        var neighbour = Describe(new { scope = "Cab3" }).Split('\n').Single(l => l.StartsWith("Cab3 "));
        StringAssert.Contains("left→Cab2 gap12", neighbour, "щель в 12 мм названа как gaps размещения");
    }

    [Test]
    public void AnOverlap_IsListedFirstAndMarked_AheadOfCleanParts()
    {
        FloorAndWall();
        Cabinet("Aaa", 0f);
        Cabinet("Zzz", 3000f);
        MakeElement("ClashA", new Vector3Int(500, 500, 500), new Vector3(4f, 0.25f, 1.5f));
        MakeElement("ClashB", new Vector3Int(500, 500, 500), new Vector3(4.2f, 0.25f, 1.5f));

        var lines = Describe().Split('\n').ToList();

        int clash = lines.FindIndex(l => l.StartsWith("ClashA "));
        Assert.Greater(clash, 0);
        Assert.Less(clash, lines.FindIndex(l => l.StartsWith("Aaa ")), "проблемная деталь раньше алфавита");
        StringAssert.Contains("; !", lines[clash]);
        StringAssert.Contains("ClashB", lines[clash], "строка называет, с кем пересечение");
        StringAssert.Contains("with issues", lines[0]);
    }

    [Test]
    public void AModule_WithAViolatingPart_SaysHowManyPartsHaveIssues()
    {
        FloorAndWall();
        var module = GroupManager.Create("Box");
        GroupManager.AddTo(module, MakeElement("Box_A", new Vector3Int(500, 500, 500), new Vector3(4f, 0.25f, 1.5f)));
        GroupManager.AddTo(module, MakeElement("Box_B", new Vector3Int(500, 500, 500), new Vector3(4.2f, 0.25f, 1.5f)));

        var line = Describe().Split('\n').Single(l => l.StartsWith("Box "));

        StringAssert.Contains("module(2)", line);
        StringAssert.Contains("!2 of 2 parts have issues", line);
    }

    [Test]
    public void Levels_AreListedWithTheirPartCounts()
    {
        FloorAndWall();
        LevelRegistry.Set(new[] { new Level("L1", "Ground", 0, 2700) });

        var text = Describe();

        StringAssert.Contains("levels: L1 \"Ground\" 0..2700 (2)", text);
    }

    [Test]
    public void TheRef_ChangesTheCoordinatesOfTheLines_AndTheLegendNamesIt()
    {
        FloorAndWall();
        Cabinet("Cab", 1000f);

        var min = Describe(new { scope = "Cab" });
        var centre = Describe(new { scope = "Cab", @ref = "center" });

        StringAssert.Contains("@(1000,0,0)", min);
        StringAssert.Contains("@(1300,360,280)", centre);
        StringAssert.Contains("= center-center-center point", centre);
    }

    [Test]
    public void AnEmptyScene_IsDescribedNotRefused()
    {
        var text = Describe();

        StringAssert.StartsWith("scene: 0 parts; no issues", text);
    }

    [Test]
    public void LookPlaceReadUndo_TheDigestSeesThePlacedRowAndForgetsItAfterUndo()
    {
        FloorAndWall();
        var before = Describe();

        var placed = ReplyOf(_handler!.Handle(MakeReq("place", new
        {
            items = new[]
            {
                new
                {
                    name = "Cab1", width = 600, height = 720, depth = 560,
                    against = new[] { new { target = "Wall", face = "front" } },
                    align = new[] { new { target = "Wall", axis = "x", at = "min", offset_mm = 0f } },
                }
            }
        })));
        Assert.IsTrue(placed["applied"]!.Value<bool>());
        var during = Describe();
        _handler.Handle(MakeReq("undo", new { }));
        var after = Describe();

        StringAssert.Contains("Cab1 board 600×720×560", during);
        StringAssert.Contains("back→Wall", during);
        Assert.AreEqual(before, after, "после undo дайджест такой же, как до place");
    }

    [Test]
    public void TheGuide_TeachesTheLoop_DescribeThenPlaceThenReadThenUndo_AndNamesVerbosity()
    {
        var workflow = McpGuideTexts.Topics["workflow"];
        int loop = workflow.IndexOf("THE LOOP: DESCRIBE -> PLACE -> READ THE PLACEMENT -> UNDO", System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(loop, 0, "цикл назван в шпаргалке");
        var steps = workflow.Substring(loop, 600);
        Assert.Less(steps.IndexOf("describe_scene", System.StringComparison.Ordinal), steps.IndexOf("place {", System.StringComparison.Ordinal));
        Assert.Less(steps.IndexOf("place {", System.StringComparison.Ordinal), steps.IndexOf("undo", System.StringComparison.Ordinal));
        StringAssert.Contains("describe_scene", McpGuideTexts.Instructions);
        StringAssert.Contains("verbosity", McpGuideTexts.Instructions);
        StringAssert.Contains("PLAIN TEXT", McpGuideTexts.Topics["fields"], "поля дайджеста объяснены там же, где поля ответов");
    }

    [Test]
    public void TheToolIsRegistered_AsAReadOnlyTool_WithADescriptiveName()
    {
        var def = McpToolRegistry.Tools.Single(t => t.Name == "describe_scene");

        Assert.AreEqual(McpToolKind.Read, def.Kind, "дайджест ничего не меняет: клиент не должен спрашивать подтверждения");
        Assert.AreEqual(typeof(ParamsDescribeScene), def.ParamsType);
    }

}
