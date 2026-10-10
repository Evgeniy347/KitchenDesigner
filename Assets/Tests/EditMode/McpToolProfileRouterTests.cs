using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

public class McpToolProfileRouterTests
{
    private static readonly string[] ToolsBeforeTheProfile =
    {
        "guide", "ping", "get_status", "get_all_elements", "describe_scene", "render_plan", "get_scene_tree", "get", "preview_floorplan", "apply_floorplan", "get_elements", "get_specification", "get_violations", "get_element_gaps", "get_element_debug", "get_settings", "get_project_instructions", "set_project_instructions", "set_attr", "move", "resize_module", "group", "align", "create_walls", "create_floor", "add_opening", "snap_diagnose", "get_free_space", "create_elements", "edit_elements", "convert_elements", "clone_elements", "align_elements", "place", "distribute_evenly", "delete_elements", "undo", "redo", "select_elements", "cycle_drawer_animation", "list_materials", "reload_textures", "get_modules", "module_info", "create_module", "dissolve_module", "add_to_module", "remove_from_module", "enter_module_edit", "exit_module_edit", "set_setting", "set_photo_camera", "set_snap_verbose", "get_console_logs", "export_specification_csv", "save_project", "load_project", "take_screenshot", "find_objects", "get_object_info", "get_scene_hierarchy", "set_object_active", "delete_object", "set_position", "set_rotation", "set_scale", "execute_menu_item", "enter_play_mode", "exit_play_mode"
    };

    private readonly List<McpRequest> _seen = new List<McpRequest>();

    private McpRpcRouter Router(McpToolProfile profile) =>
        new McpRpcRouter(request =>
        {
            _seen.Add(request);
            return McpResponse.Result(request.id, new { ok = true });
        }, "9.9.9", profile);

    private JObject Ask(McpToolProfile profile, string method, string parameters = "{}")
    {
        var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":" + parameters + "}";
        var (status, json) = Router(profile).Handle(body);
        Assert.AreEqual(200, status, json);
        return JObject.Parse(json!);
    }

    private List<string> Listed(McpToolProfile profile) =>
        ((JArray)Ask(profile, "tools/list")["result"]!["tools"]!).Select(t => t["name"]!.Value<string>()!).ToList();

    [SetUp]
    public void SetUp() => _seen.Clear();

    [Test]
    public void DefaultRouter_ListsEverythingItListedBefore_InTheSameOrder_PlusApplyRun()
    {
        var router = new McpRpcRouter(r => McpResponse.Result(r.id, new { ok = true }), "9.9.9");
        var (_, json) = router.Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
        var listed = ((JArray)JObject.Parse(json!)["result"]!["tools"]!).Select(t => t["name"]!.Value<string>()!).ToList();

        CollectionAssert.AreEqual(ToolsBeforeTheProfile.Concat(new[] { "apply_run" }).OrderBy(n => n).ToList(), listed.OrderBy(n => n).ToList(),
            "без -mcpProfile список ровно прежний (плюс новый apply_run)");
        CollectionAssert.AreEqual(McpToolRegistry.Tools.Select(t => t.Name).ToList(), listed, "и в порядке реестра");
    }

    [Test]
    public void FullProfile_IsTheSameAsTheDefault()
    {
        CollectionAssert.AreEqual(Listed(McpToolProfile.Full), McpToolRegistry.Tools.Select(t => t.Name).ToList());
    }

    [Test]
    public void SimpleProfile_ListsOnlyTheSimpleTools()
    {
        var listed = Listed(McpToolProfile.Simple);

        CollectionAssert.AreEqual(McpToolRegistry.Tools.Where(t => t.Simple).Select(t => t.Name).ToList(), listed);
        Assert.AreEqual(13, listed.Count);
        CollectionAssert.DoesNotContain(listed, "create_elements");
    }

    [Test]
    public void SimpleProfile_HiddenToolsStayCallable()
    {
        var reply = Ask(McpToolProfile.Simple, "tools/call", "{\"name\":\"create_elements\",\"arguments\":{\"items\":[]}}");

        Assert.IsNull(reply["error"], "профиль формирует только список; вызов скрытого инструмента не отвергается");
        Assert.AreEqual("create_elements", _seen.Single().method);
    }

    [Test]
    public void SimpleProfile_InitializeCarriesTheSimpleInstructions()
    {
        var instructions = (string)Ask(McpToolProfile.Simple, "initialize")["result"]!["instructions"]!;
        var full = (string)Ask(McpToolProfile.Full, "initialize")["result"]!["instructions"]!;

        Assert.AreEqual(McpSimpleGuideTexts.Instructions, instructions);
        Assert.AreEqual(McpGuideTexts.Instructions, full, "полный профиль: инструкции прежние");
        StringAssert.DoesNotContain("create_elements", instructions);
    }

    [Test]
    public void SimpleProfile_GuideAnswersWithTheSimpleTopics_AndItsSchemaOffersOnlyThem()
    {
        var router = Router(McpToolProfile.Simple);
        var guide = McpToolRegistry.Tools.First(t => t.Name == "guide");

        var workflow = new McpToolCall(r => McpResponse.Result(r.id, new { ok = true }), McpToolProfile.Simple)
            .Invoke(guide, new JObject(), "1");
        var bulk = new McpToolCall(r => McpResponse.Result(r.id, new { ok = true }), McpToolProfile.Simple)
            .Invoke(guide, new JObject { ["topic"] = "bulk" }, "1");

        Assert.AreEqual(McpSimpleGuideTexts.Topics["workflow"], (string)workflow["content"]![0]!["text"]!);
        Assert.AreEqual(McpSimpleGuideTexts.Topics["workflow"], (string)bulk["content"]![0]!["text"]!,
            "тема, которой нет в простом наборе, отвечает простым workflow, а не текстом про скрытые инструменты");

        var (_, json) = router.Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
        var guideEntry = ((JArray)JObject.Parse(json!)["result"]!["tools"]!).First(t => (string)t["name"]! == "guide");
        var offered = ((JArray)guideEntry["inputSchema"]!["properties"]!["topic"]!["enum"]!).Select(x => (string)x!).ToList();
        CollectionAssert.AreEqual(McpSimpleGuideTexts.TopicWords, offered);
        Assert.AreEqual(McpSimpleGuideTexts.GuideDescription, (string)guideEntry["description"]!);
    }

    [Test]
    public void FullProfile_GuideIsUntouched()
    {
        var guide = McpToolRegistry.Tools.First(t => t.Name == "guide");

        var text = new McpToolCall(r => McpResponse.Result(r.id, new { ok = true })).Invoke(guide, new JObject(), "1");

        Assert.AreEqual(McpGuideTexts.Topics[McpGuideTexts.DefaultTopic], (string)text["content"]![0]!["text"]!);
        var (_, json) = Router(McpToolProfile.Full).Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
        var entry = ((JArray)JObject.Parse(json!)["result"]!["tools"]!).First(t => (string)t["name"]! == "guide");
        Assert.AreEqual(guide.Description, (string)entry["description"]!);
    }

    [Test]
    public void ProfileStatus_TestOverride_WinsOverTheCommandLine()
    {
        try
        {
            McpProfileStatus.TestProfile = McpToolProfile.Simple;
            Assert.AreEqual(McpToolProfile.Simple, McpProfileStatus.Current);
        }
        finally
        {
            McpProfileStatus.ResetForTests();
        }
        Assert.AreEqual(McpToolProfile.Full, McpProfileStatus.Current, "в EditMode аргумента запуска нет: по умолчанию full");
    }
}
