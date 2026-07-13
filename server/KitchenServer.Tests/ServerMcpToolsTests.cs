using System.Text.Json;
using KitchenDesigner.Core.MCP.Contract;
using KitchenServer.McpContract;
using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

/// <summary>
/// The server's real-MCP surface: tools/list built from the shared contract, the
/// JSON-schema/rename derivation, and the auth-first gate wording.
/// </summary>
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

        // Static-only tools (guide) are NOT exposed on the server.
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
            "требуется подключение к проекту, попроси пользователя передать ключ проекта",
            McpToolHandlers.NotAuthenticatedMessage);
    }

    [Fact]
    public void Schema_MoveElement_HasCoordsAndRequiresName()
    {
        var schema = McpJsonSchema.BuildInputSchema(typeof(ParamsMoveElement));
        var props = schema.GetProperty("properties");

        Assert.Equal("string", props.GetProperty("name").GetProperty("type").GetString());
        Assert.Equal("number", props.GetProperty("x").GetProperty("type").GetString());

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("name", required);
        Assert.DoesNotContain("x", required); // optional axis
    }

    [Fact]
    public void Schema_CreateElement_RenamesGapsAndHidesTemplateName()
    {
        var schema = McpJsonSchema.BuildInputSchema(typeof(ParamsCreateElement));
        var props = schema.GetProperty("properties");

        // Agent sees snake_case gap_* names, never the wire fields or template_name.
        Assert.True(props.TryGetProperty("gap_left", out _));
        Assert.False(props.TryGetProperty("gapLeft", out _));
        Assert.False(props.TryGetProperty("template_name", out _));

        var rename = McpJsonSchema.BuildRenameMap(typeof(ParamsCreateElement));
        Assert.NotNull(rename);
        Assert.Equal("gapLeft", rename!["gap_left"]);
        Assert.Equal("gapBottom", rename["gap_bottom"]);
    }

    [Fact]
    public void Schema_SetFacadeMode_ExposesModeEnum()
    {
        var schema = McpJsonSchema.BuildInputSchema(typeof(ParamsSetFacadeMode));
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

        // Every non-static tool's schema builds without throwing.
        foreach (var t in tools.Where(t => !t.StaticText))
            _ = McpJsonSchema.BuildInputSchema(t.ParamsType);
    }
}
