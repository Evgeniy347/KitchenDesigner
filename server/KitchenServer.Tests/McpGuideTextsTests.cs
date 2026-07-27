using System.Text.RegularExpressions;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenServer.Tests;

/// <summary>
/// The guide is the first thing an agent reads. If it advertises a tool name that
/// no longer exists (the v1 surface used singular names: create_element,
/// batch_edit, set_drawer_properties…), the agent burns calls on 'unknown method'.
/// </summary>
public partial class McpGuideTextsTests
{
    [GeneratedRegex(@"\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b")]
    private static partial Regex SnakeCaseToken();

    // A guide token is treated as a tool reference when it is snake_case and starts
    // with one of the verbs the tool surface actually uses. Parameter/field names
    // (drawer_type, top_y_mm, all_boards…) never start with these.
    private static readonly string[] ToolPrefixes =
    {
        "get_", "set_", "create_", "edit_", "delete_", "add_", "remove_", "align_",
        "clone_", "convert_", "distribute_", "select_", "resize_", "apply_",
        "preview_", "list_", "reload_", "export_", "cycle_", "enter_", "exit_",
        "dissolve_", "module_", "find_", "snap_", "take_", "execute_",
    };

    [Fact]
    public void EveryToolNameMentionedInAGuideTopicExists()
    {
        var known = McpToolRegistry.Tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var unknown = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (topic, text) in McpGuideTexts.Topics)
            foreach (Match m in SnakeCaseToken().Matches(text))
            {
                var token = m.Value;
                if (!ToolPrefixes.Any(p => token.StartsWith(p, StringComparison.Ordinal))) continue;
                if (!known.Contains(token)) unknown.Add($"{topic}: {token}");
            }

        Assert.Empty(unknown);
    }

    [Fact]
    public void EveryTopicOfferedByTheGuideParameterHasText()
    {
        var offered = typeof(ParamsGuide).GetField(nameof(ParamsGuide.topic))!
            .GetCustomAttributes(typeof(McpParamAttribute), false)
            .Cast<McpParamAttribute>().Single().Enum;

        Assert.NotEmpty(offered);
        foreach (var topic in offered)
            Assert.True(McpGuideTexts.Topics.ContainsKey(topic), $"guide topic '{topic}' has no text");
        Assert.Contains(McpGuideTexts.DefaultTopic, McpGuideTexts.Topics.Keys);
    }
}
