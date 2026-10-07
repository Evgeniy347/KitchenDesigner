using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Главная петля слабой модели: прочитала «posXMm» (центр), подставила в «anchor» (угол),
/// деталь уехала на полширины, пересеклась с соседом, модель правит наугад. Сторож замыкает цикл
/// чтение -> запись для КАЖДОГО значения ref и для повёрнутой на 90 градусов детали: точка,
/// прочитанная в ref и записанная обратно в том же ref, не двигает деталь.</summary>
public class McpReferenceRoundTripTests : McpTestFixture
{
    private const float MmTolerance = 0.06f;

    [SetUp]
    public void Setup() => CommandStack.Clear();

    [TearDown]
    public void Teardown() => CommandStack.Clear();

    private static IEnumerable<string> AllReferences()
    {
        foreach (ReferenceSide x in Enum.GetValues(typeof(ReferenceSide)))
        foreach (ReferenceSide y in Enum.GetValues(typeof(ReferenceSide)))
        foreach (ReferenceSide z in Enum.GetValues(typeof(ReferenceSide)))
            yield return new McpReference(x, y, z).Canonical;
    }

    private static IEnumerable<TestCaseData> EveryReferenceAtBothRotations()
    {
        foreach (var name in AllReferences())
            foreach (var rotY in new[] { 0f, 90f })
                yield return new TestCaseData(name, rotY).SetName(
                    $"ReadInRef_ThenWriteTheSameNumbers_DoesNotMoveThePart_{name}_rotY{(int)rotY}");
    }

    private KitchenElement CreateBoard(string name, string? reference = null, float x = 1234.5f, float y = 0f, float z = -890f,
        float rotY = 0f)
    {
        var create = _handler!.Handle(MakeReq("create_elements", new
        {
            @ref = reference,
            items = new[] { new { name, width = 800, height = 720, depth = 560, anchor_x_mm = x, anchor_y_mm = y, anchor_z_mm = z } }
        }));
        Assert.AreEqual("result", create.type, "create_elements отказал: " + ReplyOf(create));
        var element = PartRegistry.GetAll().First(e => e.PartName == name);
        if (rotY != 0f)
        {
            var rotate = _handler.Handle(MakeReq("edit_elements", new { ops = new[] { new { name, rot_y = rotY } } }));
            Assert.AreEqual("result", rotate.type, "поворот отказал: " + ReplyOf(rotate));
        }
        return element;
    }

    private static (Vector3 min, Vector3 max) WorldBoxMm(KitchenElement el)
    {
        var vertices = el.GetVertices();
        var min = vertices[0];
        var max = vertices[0];
        foreach (var v in vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        return (min * 1000f, max * 1000f);
    }

    private static McpReference Parsed(string name)
    {
        Assert.IsTrue(McpReference.TryParse(name, out var reference, out var error), error);
        return reference;
    }

    private float[] ReadPos(string name, string reference)
    {
        var resp = _handler!.Handle(MakeReq("get", new { names = new[] { name }, fields = new[] { "posMm" }, @ref = reference }));
        Assert.AreEqual("result", resp.type, "get отказал: " + ReplyOf(resp));
        var reply = ReplyOf(resp);
        Assert.AreEqual(reference, reply["ref"]!.Value<string>(), "get эхом возвращает ref, по которому считал");
        return reply["elements"]![0]!["posMm"]!.ToObject<float[]>()!;
    }

    [TestCaseSource(nameof(EveryReferenceAtBothRotations))]
    public void ReadInRef_ThenWriteTheSameNumbers_DoesNotMoveThePart(string reference, float rotY)
    {
        var board = CreateBoard("Probe", rotY: rotY);
        var (minBefore, maxBefore) = WorldBoxMm(board);

        var pos = ReadPos("Probe", reference);
        var expected = Parsed(reference).PointOf(minBefore, maxBefore);
        for (int axis = 0; axis < 3; axis++)
            Assert.AreEqual(expected[axis], pos[axis], MmTolerance,
                $"get ref={reference} обязан отдать именно эту точку мирового бокса, ось {axis}");

        var write = _handler!.Handle(MakeReq("edit_elements", new
        {
            @ref = reference,
            ops = new[] { new { name = "Probe", anchor_x_mm = pos[0], anchor_y_mm = pos[1], anchor_z_mm = pos[2] } }
        }));
        Assert.AreEqual("result", write.type, "edit_elements отказал: " + ReplyOf(write));

        var (minAfter, maxAfter) = WorldBoxMm(board);
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.AreEqual(minBefore[axis], minAfter[axis], MmTolerance,
                $"ref={reference} rot_y={rotY}: прочитанное и записанное обратно в том же ref сдвинуло деталь (min, ось {axis})");
            Assert.AreEqual(maxBefore[axis], maxAfter[axis], MmTolerance, $"то же для max, ось {axis}");
        }
        CollectionAssert.AreEqual(pos, ReadPos("Probe", reference), "и второе чтение совпало с первым");
        Assert.AreEqual(reference, ReplyOf(write)["ref"]!.Value<string>(),
            "ответ мутации называет ref, в котором дал позицию");
    }

    [Test]
    public void ReadInRef_ThenWriteIntoTheSameRef_ReturnsTheSameNumbersInThePlacement()
    {
        CreateBoard("Probe", rotY: 90f);
        var pos = ReadPos("Probe", "center-bottom-center");

        var write = _handler!.Handle(MakeReq("edit_elements", new
        {
            @ref = "center-bottom",
            ops = new[] { new { name = "Probe", anchor_x_mm = pos[0], anchor_y_mm = pos[1], anchor_z_mm = pos[2] } }
        }));

        CollectionAssert.AreEqual(pos, PlacementOf(write)["posMm"]!.ToObject<float[]>(),
            "posMm в ответе мутации в том же ref, что и в запросе: прочитал -> записал -> получил то же самое");
        Assert.AreEqual("center-bottom-center", ReplyOf(write)["ref"]!.Value<string>(),
            "ref в ответе канонический: «center-bottom» превращается в три слова по осям x-y-z, и его можно подставлять обратно дословно");
    }

    [TestCaseSource(nameof(EveryReferenceAtBothRotations))]
    public void CreateAtRef_PutsThatPointOfTheWorldBoxOnTheAnchor(string reference, float rotY)
    {
        var board = CreateBoard("Fresh", reference, x: 2000f, y: 360f, z: 1500f);
        if (rotY != 0f)
        {
            var rotate = _handler!.Handle(MakeReq("edit_elements", new
            {
                @ref = reference,
                ops = new[] { new { name = "Fresh", rot_y = rotY, anchor_x_mm = 2000f, anchor_y_mm = 360f, anchor_z_mm = 1500f } }
            }));
            Assert.AreEqual("result", rotate.type, "правка с поворотом отказала: " + ReplyOf(rotate));
        }

        var (min, max) = WorldBoxMm(board);
        var point = Parsed(reference).PointOf(min, max);
        Assert.AreEqual(2000f, point.x, MmTolerance, $"ref={reference} rot_y={rotY}: x");
        Assert.AreEqual(360f, point.y, MmTolerance, "y");
        Assert.AreEqual(1500f, point.z, MmTolerance,
            "точка ref ПОВЁРНУТОГО бокса встаёт на якорь: при rot_y=90 в одной правке поворот и позиция считаются вместе");
    }

    [Test]
    public void WithoutRef_TheNumbersAreTheMinimumCorner_AsBeforeRefExisted()
    {
        var board = CreateBoard("Legacy");

        var (min, _) = WorldBoxMm(board);
        Assert.AreEqual(1234.5f, min.x, MmTolerance, "без ref anchor_x_mm — минимальный угол, как раньше");
        Assert.AreEqual(-890f, min.z, MmTolerance);
        CollectionAssert.AreEqual(new[] { min.x, min.y, min.z }, ReadPos("Legacy", McpReference.DefaultName).Select(v => v).ToArray(),
            "и чтение без ref отдаёт тот же угол");
        Assert.AreEqual(McpReference.DefaultName, ReplyOf(_handler!.Handle(MakeReq("get", new { names = new[] { "Legacy" } })))["ref"]!.Value<string>(),
            "ответ называет умолчание вслух: слабой модели не нужно помнить, что оно такое");
    }

    [Test]
    public void CreateAtCenterBottom_StandsTheFootprintMiddleOnTheAnchor_OnTheFloor()
    {
        var board = CreateBoard("Mid", "center-bottom", x: 3000f, y: 0f, z: 2000f);

        var (min, max) = WorldBoxMm(board);
        Assert.AreEqual(3000f, (min.x + max.x) / 2f, MmTolerance, "середина следа по X");
        Assert.AreEqual(2000f, (min.z + max.z) / 2f, MmTolerance, "середина следа по Z");
        Assert.AreEqual(0f, min.y, MmTolerance, "низ на высоте якоря");
    }

    private static IEnumerable<TestCaseData> ToolsThatTakeARef()
    {
        yield return new TestCaseData("create_elements", new { items = new[] { new { name = "X", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } } });
        yield return new TestCaseData("edit_elements", new { ops = new[] { new { name = "Probe", locked = false } } });
        yield return new TestCaseData("clone_elements", new { ops = new[] { new { name = "Probe", count = 1 } } });
        yield return new TestCaseData("align_elements", new { ops = new[] { new { name = "Probe", face = "left", target = "Other", target_face = "right" } } });
        yield return new TestCaseData("distribute_evenly", new { names = new[] { "Probe", "Other", "Third" }, axis = "x" });
        yield return new TestCaseData("convert_elements", new { ops = new[] { new { name = "Probe", target = "facade" } } });
        yield return new TestCaseData("get", new { names = new[] { "Probe" } });
        yield return new TestCaseData("get_elements", new { names = new[] { "Probe" } });
        yield return new TestCaseData("snap_diagnose", new { ops = new[] { new { name = "Probe" } } });
        yield return new TestCaseData("set_attr", new { selector = "Probe", locked = false });
        yield return new TestCaseData("move", new { selector = "Probe", dx = 1f });
        yield return new TestCaseData("align", new { selector = "Probe", target = "Other", face = "left" });
        yield return new TestCaseData("resize_module", new { module = "Nope", delta_mm = 1f });
    }

    private static IEnumerable<TestCaseData> ToolsThatRefuseABadRefBeforeTouchingTheScene()
    {
        foreach (var data in ToolsThatTakeARef()) yield return data;
    }

    [TestCaseSource(nameof(ToolsThatRefuseABadRefBeforeTouchingTheScene))]
    public void AContradictoryOrUnknownRef_IsRefusedWithTheSyntax_ByEveryToolThatTakesOne(string tool, object payload)
    {
        MakeElement("Probe", new Vector3Int(500, 400, 18));
        MakeElement("Other", new Vector3Int(500, 400, 18), new Vector3(3f, 0f, 0f));
        MakeElement("Third", new Vector3Int(500, 400, 18), new Vector3(6f, 0f, 0f));
        var body = JObject.FromObject(payload);
        body["ref"] = "left-right";

        var resp = _handler!.Handle(new McpRequest { id = "t", method = tool, Params = body });

        Assert.AreEqual("error", resp.type, $"{tool} принял противоречивый ref «left-right» молча");
        var message = JObject.FromObject(resp.data!)["message"]!.Value<string>()!;
        StringAssert.Contains("ref", message, $"{tool}: ошибка называет параметр");
        StringAssert.Contains("left-bottom-back", message, $"{tool}: ошибка показывает, как писать правильно");
    }

    [Test]
    public void EveryToolThatReportsAPosition_ExposesTheRefParameter_InItsSchema()
    {
        foreach (var name in ToolsThatTakeARef().Select(c => (string)c.Arguments[0]))
        {
            var tool = KitchenDesigner.Core.MCP.Contract.McpToolRegistry.Tools.First(t => t.Name == name);
            Assert.IsTrue(KitchenDesigner.Core.MCP.Contract.McpJsonSchema.ForTool(tool)["properties"] is IDictionary<string, object> props
                && props.ContainsKey("ref"), $"{name}: в схеме нет параметра ref, а ответ называет позицию");
        }
    }
}
