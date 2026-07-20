using System.Text.Json;
using KitchenDesigner.Core.MCP.Contract;
using KitchenServer.McpContract;
using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

public class ServerMcpToolsTests
{
    [Fact]
    public async Task ListTools_ExposesAuthenticatePlusEveryNonStaticTool()
    {
        var result = await McpToolHandlers.ListToolsAsync(null!, default);
        var names = result.Tools.Select(t => t.Name).ToHashSet();

        Assert.Contains(McpToolHandlers.AuthToolName, names);

        var expected = McpToolRegistry.Tools.Where(t => !t.StaticText).Select(t => t.Name);
        foreach (var name in expected)
            Assert.Contains(name, names);

        Assert.DoesNotContain("guide", names);
    }

    [Fact]
    public async Task ListTools_EveryToolHasAnObjectInputSchema()
    {
        var result = await McpToolHandlers.ListToolsAsync(null!, default);
        foreach (var tool in result.Tools)
        {
            Assert.Equal(JsonValueKind.Object, tool.InputSchema.ValueKind);
            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
        }
    }

    [Fact]
    public void AuthGateMessage_IsTheExactRussianWording()
    {
        Assert.Equal(
            "требуется подключение к проекту. Если ты уже был подключён, соединение могло оборваться — " +
            "попробуй вызвать authenticate с тем же ключом ещё раз. Иначе попроси пользователя передать ключ проекта.",
            McpToolHandlers.NotAuthenticatedMessage);
    }

    [Fact]
    public void Schema_EditOp_HasCoordsAndRequiresName()
    {
        var schema = McpJsonSchema.BuildInputSchema(typeof(EditOp));
        var props = schema.GetProperty("properties");

        Assert.Equal("string", props.GetProperty("name").GetProperty("type").GetString());
        Assert.Equal("number", props.GetProperty("x").GetProperty("type").GetString());

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("name", required);
        Assert.DoesNotContain("x", required);
    }

    [Fact]
    public void Schema_CreateItem_HasRequiredNameAndOptionalGeometry()
    {
        var schema = McpJsonSchema.BuildInputSchema(typeof(CreateItem));
        var props = schema.GetProperty("properties");

        Assert.True(props.TryGetProperty("name", out _));
        Assert.Equal("string", props.GetProperty("name").GetProperty("type").GetString());

        Assert.True(props.TryGetProperty("type", out _));
        Assert.True(props.TryGetProperty("x", out _));
        Assert.True(props.TryGetProperty("width", out _));

        // name is required, geometry is optional
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("name", required);
        Assert.DoesNotContain("x", required);
    }

    [Fact]
    public void Schema_EditOp_ExposesModeEnum()
    {
        var schema = McpJsonSchema.BuildInputSchema(typeof(EditOp));
        var mode = schema.GetProperty("properties").GetProperty("mode");
        var values = mode.GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Contains("front_left", values);
        Assert.Contains("drawer_down", values);
        Assert.Equal(18, values.Count);
    }

    [Fact]
    public void Registry_HasUniqueNamesAndDescriptions()
    {
        var tools = McpToolRegistry.Tools;
        Assert.Equal(tools.Count, tools.Select(t => t.Name).Distinct().Count());
        Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));

        foreach (var t in tools.Where(t => !t.StaticText))
            _ = McpJsonSchema.BuildInputSchema(t.ParamsType);
    }
}
