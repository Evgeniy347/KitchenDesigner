using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>render_plan на живой сцене. Рисунок как таковой проверен быстрыми тестами (Pure); здесь — то, что даёт только
/// сцена: что вообще попало в кадр, что сужает scope, что картинка согласна с describe_scene и что она ничего не меняет.
/// Работает без камеры и без PlayMode: картинка собирается из боксов деталей, а не снимается с экрана.</summary>
public class McpRenderPlanTests : McpTestFixture
{
    private const int ModuleCount = 4;
    private const int PartsPerModule = 8;
    private const int LooseCabinets = 16;
    private const int PngBudgetBytes = 7000;

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

    private JObject Call(string tool, object args)
    {
        var def = McpToolRegistry.Tools.First(t => t.Name == tool);
        return new McpToolCall(_handler!.Handle).Invoke(def, JObject.FromObject(args), "plan");
    }

    private (string caption, byte[] png) Plan(object? args = null)
    {
        var result = Call("render_plan", args ?? new { });
        Assert.IsNull(result["isError"], "render_plan отказал: " + result);
        var content = (JArray)result["content"]!;
        Assert.AreEqual(2, content.Count);
        return ((string)content[0]!["text"]!, Convert.FromBase64String((string)content[1]!["data"]!));
    }

    private string PlanError(object args)
    {
        var result = Call("render_plan", args);
        Assert.AreEqual(true, (bool?)result["isError"], "ждали отказ: " + result);
        return (string)((JArray)result["content"]!)[0]!["text"]!;
    }

    private string Describe(object? args = null) =>
        (string)((JArray)Call("describe_scene", args ?? new { })["content"]!)[0]!["text"]!;

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

    private static (int width, int height) PngSize(byte[] png) =>
        ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    private static double MmPerPixel(string caption)
    {
        var after = caption.Substring(caption.IndexOf("1 px = ", StringComparison.Ordinal) + "1 px = ".Length);
        return double.Parse(after.Substring(0, after.IndexOf(' ')), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Test]
    public void ALiveScene_GivesACaptionAndAPngOfTheRequestedSize()
    {
        FiftyPartScene();

        var (caption, png) = Plan(new { px = 512 });

        var (width, height) = PngSize(png);
        Assert.AreEqual(512, Math.Max(width, height), "длинная сторона — запрошенное px");
        StringAssert.StartsWith($"render_plan top {width}x{height} px; scope whole scene;", caption);
        StringAssert.Contains("x right, z up", caption, "север сверху, как в preview_floorplan");
        Assert.LessOrEqual(png.Length, PngBudgetBytes, "картинка пятидесяти деталей — в бюджет байтов");
        TestContext.WriteLine($"PLAN 50 parts: {png.Length} bytes PNG, caption: {caption}");
    }

    [Test]
    public void ThePictureAndTheDigest_NameTheSameThings()
    {
        FiftyPartScene();
        var digest = Describe(new { max_chars = SceneDigestText.MaxMaxChars });
        Assert.IsTrue(McpSceneDigest.TryCollect(null, McpReference.MinCorner, out var input, out var error), error);

        var svg = PlanRender.Svg(input, PlanView.Top, true, 512);

        Assert.AreEqual(ModuleCount + LooseCabinets + 2, input.Entries.Count, "предусловие: четыре модуля, шестнадцать шкафов, пол и стена");
        foreach (var entry in input.Entries)
        {
            StringAssert.Contains(entry.Name + " ", digest, "в тексте есть «" + entry.Name + "»");
            StringAssert.Contains("data-name=\"" + entry.Name + "\"", svg, "и на картинке тоже");
        }
        var (caption, _) = Plan();
        StringAssert.Contains($"{ModuleCount + LooseCabinets + 2} items", caption);
    }

    [Test]
    public void AModuleScope_DrawsItsPartsOneByOne_AndZoomsInOnThem()
    {
        FiftyPartScene();
        var (whole, _) = Plan();

        var (caption, _) = Plan(new { scope = "Module2" });

        StringAssert.Contains("scope module Module2", caption);
        StringAssert.Contains($"{PartsPerModule} items", caption);
        Assert.Less(MmPerPixel(caption) * 2, MmPerPixel(whole), "восемь деталей модуля занимают картинку, а не угол сцены");
        Assert.IsTrue(McpSceneDigest.TryCollect("Module2", McpReference.MinCorner, out var input, out _));
        var svg = PlanRender.Svg(input, PlanView.Top, true, 512);
        StringAssert.Contains("data-name=\"M2_Part1\"", svg);
        StringAssert.DoesNotContain("M1_Part1", svg);
        StringAssert.DoesNotContain("Cab01", svg);
    }

    [Test]
    public void ASelectorScope_DrawsTheMatchedPartsOnly()
    {
        FiftyPartScene();

        var (caption, _) = Plan(new { scope = "Cab0*" });

        StringAssert.Contains("scope selector \"Cab0*\"", caption);
        StringAssert.Contains("9 items", caption);
    }

    [Test]
    public void ARoomScope_DrawsTheRoomOutlineAndWhatStandsInIt()
    {
        FiftyPartScene();
        ProjectRooms.Set(new[]
        {
            new RoomData { id = "Kitchen", floor = "Floor", polygonXZ = new[] { -1000, -500, 12000, -500, 12000, 1500, -1000, 1500 } }
        });

        var (caption, _) = Plan(new { scope = "room:Kitchen" });

        StringAssert.Contains("scope room Kitchen", caption);
        Assert.IsTrue(McpSceneDigest.TryCollect("room:Kitchen", McpReference.MinCorner, out var input, out _));
        CollectionAssert.AreEqual(new[] { "Kitchen" }, input.Rooms.Select(r => r.Id).ToList());
        Assert.IsNotNull(input.Rooms[0].PolygonXzMm, "контур комнаты едет в рисунок вместе с её id");
    }

    [Test]
    public void AnUnknownScope_IsRefusedWithTheSameHintsAsDescribeScene()
    {
        FiftyPartScene();

        var text = PlanError(new { scope = "Modul1" });

        StringAssert.Contains("closest: Module1", text);
    }

    [Test]
    public void TheFrontView_SaysItsAxesInTheCaption()
    {
        FiftyPartScene();

        var (caption, png) = Plan(new { view = "front" });

        StringAssert.StartsWith("render_plan front ", caption);
        StringAssert.Contains("x right, y up", caption);
        Assert.Greater(png.Length, 500);
    }

    [Test]
    public void APartWithAnOverlap_IsCountedAndNamedAsHavingAnIssue()
    {
        FloorAndWall();
        Cabinet("Aaa", 0f);
        MakeElement("ClashA", new Vector3Int(500, 500, 500), new Vector3(4f, 0.25f, 1.5f));
        MakeElement("ClashB", new Vector3Int(500, 500, 500), new Vector3(4.2f, 0.25f, 1.5f));

        var (caption, _) = Plan();

        StringAssert.Contains("2 with issues (red outline + triangle, ! before the name)", caption);
        Assert.IsTrue(McpSceneDigest.TryCollect("Clash*", McpReference.MinCorner, out var input, out _));
        var svg = PlanRender.Svg(input, PlanView.Top, true, 512);
        StringAssert.Contains(">!ClashA<", svg, "восклицательный знак в самой подписи");
        StringAssert.Contains("stroke=\"#D21F1F\"", svg, "красная рамка");
    }

    [Test]
    public void LabelsFalse_SaysSoInTheCaption()
    {
        FiftyPartScene();

        var (caption, _) = Plan(new { labels = false });

        StringAssert.EndsWith("labels off", caption);
    }

    [Test]
    public void TwoCallsOnTheSameScene_ReturnTheSameBytes()
    {
        FiftyPartScene();

        var first = Plan();
        var second = Plan();

        CollectionAssert.AreEqual(first.png, second.png, "порядок обхода сцены не должен менять ни пикселя");
        Assert.AreEqual(first.caption, second.caption);
    }

    [Test]
    public void RenderPlan_ChangesNothingInTheScene_AndPushesNoUndoStep()
    {
        FiftyPartScene();
        int parts = PartRegistry.GetAll().Count;
        var before = PartRegistry.GetAll().Select(e => e.PartName + e.transform.position).OrderBy(s => s).ToList();
        int undoable = CommandStack.UndoCount;

        Plan();
        Plan(new { view = "front", scope = "Module1" });

        Assert.AreEqual(parts, PartRegistry.GetAll().Count);
        CollectionAssert.AreEqual(before, PartRegistry.GetAll().Select(e => e.PartName + e.transform.position).OrderBy(s => s).ToList());
        Assert.AreEqual(undoable, CommandStack.UndoCount, "это чтение: шага отмены нет");
    }

    [TestCase(100)]
    [TestCase(255)]
    [TestCase(2049)]
    public void PxOutOfRange_IsRefusedWithTheRange(int px)
    {
        FloorAndWall();

        var text = PlanError(new { px });

        StringAssert.Contains($"{PlanRender.MinPx}..{PlanRender.MaxPx}", text);
    }

    [Test]
    public void AnUnknownView_IsRefusedWithTheWords()
    {
        FloorAndWall();

        StringAssert.Contains("top|front", PlanError(new { view = "side" }));
    }

    [Test]
    public void AnEmptyScene_StillAnswersWithAPicture()
    {
        var (caption, png) = Plan();

        StringAssert.Contains("0 items", caption);
        Assert.Greater(png.Length, 100);
    }

    [Test]
    public void EveryRegisteredTool_HasAHandler_RenderPlanIncluded()
    {
        var tool = McpToolRegistry.Tools.Single(t => t.Name == "render_plan");
        Assert.AreEqual(typeof(ParamsRenderPlan), tool.ParamsType);

        var response = _handler!.Handle(MakeReq("render_plan", new { }));

        Assert.AreEqual("result", response.type, "обработчик знает render_plan: иначе ответ — Unknown method");
        Assert.IsInstanceOf<McpImageReply>(response.data);
    }
}
