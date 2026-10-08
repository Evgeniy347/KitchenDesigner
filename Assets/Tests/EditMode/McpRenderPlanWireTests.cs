using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

/// <summary>Картинка на проводе: MCP-ответ с элементом {type:"image"} рядом с короткой строкой текста. Роутер подставной
/// диспетчер, сцены нет: проверяется именно упаковка — что агент получит base64 настоящего PNG нужного размера,
/// а не путь к файлу и не строку «System.Byte[]». Сам рисунок и его байтовый бюджет держат быстрые тесты в Pure.</summary>
public class McpRenderPlanWireTests
{
    private static DigestInput Scene()
    {
        var input = new DigestInput();
        input.Entries.Add(new DigestEntry { Name = "Floor", Kind = "floor", Box = new BoxMm(new Vector3(0, -20, 0), new Vector3(3000, 0, 2000)) });
        input.Entries.Add(new DigestEntry { Name = "Cab01", Kind = "board", Box = new BoxMm(new Vector3(0, 0, 0), new Vector3(600, 720, 560)) });
        input.Entries.Add(new DigestEntry { Name = "Cab02", Kind = "board", Box = new BoxMm(new Vector3(600, 0, 0), new Vector3(1200, 720, 560)) });
        return input;
    }

    private static McpRpcRouter Router(Func<McpRequest, McpResponse> dispatch) => new McpRpcRouter(dispatch, "9.9.9");

    private static McpResponse Picture(McpRequest request)
    {
        int px = request.Params != null && request.Params["px"] != null ? (int)request.Params["px"]! : PlanRender.DefaultPx;
        return McpResponse.Result(request.id, PlanRender.Render(Scene(), PlanView.Top, true, px));
    }

    private static JObject Call(McpRpcRouter router, string arguments = "{}")
    {
        var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"render_plan\",\"arguments\":" + arguments + "}}";
        var (status, json) = router.Handle(body);
        Assert.AreEqual(200, status);
        return (JObject)JObject.Parse(json!)["result"]!;
    }

    private static (int width, int height) PngSize(byte[] png) =>
        ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    [Test]
    public void ARenderPlanCall_AnswersWithOneTextLineAndOneImage_InThatOrder()
    {
        var result = Call(Router(Picture));

        var content = (JArray)result["content"]!;
        Assert.AreEqual(2, content.Count);
        Assert.AreEqual("text", (string?)content[0]!["type"], "подпись первой: её читают раньше картинки");
        StringAssert.StartsWith("render_plan top ", (string?)content[0]!["text"]);
        Assert.AreEqual("image", (string?)content[1]!["type"]);
        Assert.AreEqual("image/png", (string?)content[1]!["mimeType"]);
        Assert.IsNull(result["isError"]);
    }

    [TestCase(256)]
    [TestCase(512)]
    [TestCase(1000)]
    public void TheImageData_IsAValidBase64Png_OfTheRequestedLongerSide(int px)
    {
        var result = Call(Router(Picture), "{\"px\":" + px + "}");

        var data = (string)((JArray)result["content"]!)[1]!["data"]!;
        var png = Convert.FromBase64String(data);
        Assert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, new List<byte>(png).GetRange(0, 8).ToArray());
        var (width, height) = PngSize(png);
        Assert.AreEqual(px, Math.Max(width, height), "длинная сторона — ровно запрошенное px");
        Assert.AreEqual(data.Length, (png.Length + 2) / 3 * 4, "base64 без переводов строк и пробелов");
    }

    [Test]
    public void TheImageItem_IsSmallEnoughToSendByDefault()
    {
        var result = Call(Router(Picture));

        var data = (string)((JArray)result["content"]!)[1]!["data"]!;
        Assert.Less(data.Length, 8000, "картинка по умолчанию идёт в ответе как base64: потолок в знаках, не в мечтах");
    }

    [Test]
    public void ARefusal_StaysAnErrorText_WithNoImage()
    {
        var router = Router(request => McpResponse.Error(request.id, -1, "unknown scope"));

        var result = Call(router);

        var content = (JArray)result["content"]!;
        Assert.AreEqual(1, content.Count);
        Assert.AreEqual("text", (string?)content[0]!["type"]);
        StringAssert.Contains("unknown scope", (string?)content[0]!["text"]);
        Assert.AreEqual(true, (bool?)result["isError"]);
    }

    [Test]
    public void APlainTextReply_StillComesBackAsOneTextItem()
    {
        var router = Router(request => McpResponse.Result(request.id, "scene: 3 parts"));

        var result = Call(router);

        var content = (JArray)result["content"]!;
        Assert.AreEqual(1, content.Count, "картинку роутер добавляет только тому ответу, который её несёт");
        Assert.AreEqual("scene: 3 parts", (string?)content[0]!["text"]);
    }

    [Test]
    public void TheToolsList_DescribesRenderPlan_WithItsFourParameters()
    {
        var (_, json) = Router(Picture).Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");

        var tools = (JArray)JObject.Parse(json!)["result"]!["tools"]!;
        JObject? tool = null;
        foreach (var candidate in tools)
            if ((string?)candidate["name"] == "render_plan") tool = (JObject)candidate;
        Assert.IsNotNull(tool, "render_plan есть в tools/list");
        var properties = (JObject)tool!["inputSchema"]!["properties"]!;
        CollectionAssert.AreEquivalent(new[] { "view", "scope", "labels", "px" }, new List<string>(Names(properties)));
        Assert.AreEqual(PlanRender.MinPx, (int)properties["px"]!["minimum"]!);
        Assert.AreEqual(PlanRender.MaxPx, (int)properties["px"]!["maximum"]!);
    }

    private static IEnumerable<string> Names(JObject properties)
    {
        foreach (var property in properties.Properties()) yield return property.Name;
    }
}
