using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP.Contract;

public class McpToolProfilesTests
{
    private static readonly string[] SimpleTools =
    {
        "guide", "describe_scene", "render_plan", "get", "apply_floorplan", "get_violations",
        "edit_elements", "place", "apply_run", "delete_elements", "undo", "redo", "save_project",
    };

    private const string NotPartOfTheLoop = "guide";

    private static readonly Dictionary<string, string> ReplacementOfHidden = new Dictionary<string, string>
    {
        ["ping"] = "describe_scene",
        ["get_status"] = "describe_scene",
        ["get_all_elements"] = "describe_scene",
        ["get_scene_tree"] = "describe_scene",
        ["get_scene_hierarchy"] = "describe_scene",
        ["find_objects"] = "describe_scene",
        ["get_modules"] = "describe_scene",
        ["module_info"] = "describe_scene",
        ["get_element_gaps"] = "describe_scene",
        ["get_free_space"] = "describe_scene",
        ["get_elements"] = "get",
        ["get_element_debug"] = "get",
        ["get_object_info"] = "get",
        ["snap_diagnose"] = "get_violations",
        ["take_screenshot"] = "render_plan",
        ["preview_floorplan"] = "apply_floorplan",
        ["create_walls"] = "apply_floorplan",
        ["create_floor"] = "apply_floorplan",
        ["add_opening"] = "apply_floorplan",
        ["create_elements"] = "place",
        ["clone_elements"] = "place",
        ["align"] = "place",
        ["align_elements"] = "place",
        ["distribute_evenly"] = "place",
        ["set_attr"] = "edit_elements",
        ["move"] = "edit_elements",
        ["resize_module"] = "edit_elements",
        ["set_position"] = "edit_elements",
        ["set_rotation"] = "edit_elements",
        ["set_scale"] = "edit_elements",
        ["delete_object"] = "delete_elements",
        ["group"] = NotPartOfTheLoop,
        ["create_module"] = NotPartOfTheLoop,
        ["dissolve_module"] = NotPartOfTheLoop,
        ["add_to_module"] = NotPartOfTheLoop,
        ["remove_from_module"] = NotPartOfTheLoop,
        ["enter_module_edit"] = NotPartOfTheLoop,
        ["exit_module_edit"] = NotPartOfTheLoop,
        ["convert_elements"] = NotPartOfTheLoop,
        ["select_elements"] = NotPartOfTheLoop,
        ["cycle_drawer_animation"] = NotPartOfTheLoop,
        ["set_object_active"] = NotPartOfTheLoop,
        ["get_specification"] = NotPartOfTheLoop,
        ["export_specification_csv"] = NotPartOfTheLoop,
        ["get_settings"] = NotPartOfTheLoop,
        ["set_setting"] = NotPartOfTheLoop,
        ["get_project_instructions"] = NotPartOfTheLoop,
        ["set_project_instructions"] = NotPartOfTheLoop,
        ["set_photo_camera"] = NotPartOfTheLoop,
        ["set_snap_verbose"] = NotPartOfTheLoop,
        ["get_console_logs"] = NotPartOfTheLoop,
        ["list_materials"] = NotPartOfTheLoop,
        ["reload_textures"] = NotPartOfTheLoop,
        ["load_project"] = NotPartOfTheLoop,
        ["execute_menu_item"] = NotPartOfTheLoop,
        ["enter_play_mode"] = NotPartOfTheLoop,
        ["exit_play_mode"] = NotPartOfTheLoop,
    };

    private static readonly string[] HiddenNamesThatAreAlsoPlainWords = { "ping", "align", "move", "group" };

    private static List<string> Names(McpToolProfile profile) =>
        McpToolProfiles.Select(McpToolRegistry.Tools, profile).Select(t => t.Name).ToList();

    private static List<string> Hidden() =>
        McpToolRegistry.Tools.Where(t => !t.Simple).Select(t => t.Name).ToList();

    [Test]
    public void SimpleProfile_ListsExactlyTheThirteenChosenTools_InRegistryOrder()
    {
        CollectionAssert.AreEquivalent(SimpleTools, Names(McpToolProfile.Simple),
            "состав простого профиля закреплён поимённо: добавить или убрать инструмент — решение, а не побочный эффект");
        var registryOrder = McpToolRegistry.Tools.Where(t => SimpleTools.Contains(t.Name)).Select(t => t.Name).ToList();
        CollectionAssert.AreEqual(registryOrder, Names(McpToolProfile.Simple), "порядок как в реестре");
    }

    [Test]
    public void FullProfile_ListsEveryRegisteredTool_ExactlyAsBefore()
    {
        CollectionAssert.AreEqual(McpToolRegistry.Tools.Select(t => t.Name).ToList(), Names(McpToolProfile.Full),
            "профиль по умолчанию не имеет права ничего скрыть или переставить");
        Assert.AreEqual(McpToolRegistry.Tools.Count, Names(McpToolProfile.Full).Distinct().Count());
    }

    [Test]
    public void EveryHiddenTool_HasANamedReplacement_ThatIsListedInSimple()
    {
        var hidden = Hidden();

        CollectionAssert.AreEquivalent(hidden, ReplacementOfHidden.Keys.ToList(),
            "таблица замен и список скрытых инструментов обязаны совпасть: новый инструмент без замены или запись о снятом инструменте — красный");
        foreach (var pair in ReplacementOfHidden)
            CollectionAssert.Contains(SimpleTools, pair.Value,
                $"замена {pair.Key} -> {pair.Value} не достижима в простом профиле");
    }

    [Test]
    public void EverySimpleTool_IsActuallyRegistered_AndMarkedSimple()
    {
        foreach (var name in SimpleTools)
        {
            var tool = McpToolRegistry.Tools.FirstOrDefault(t => t.Name == name);
            Assert.IsNotNull(tool, name + " нет в реестре");
            Assert.IsTrue(tool!.Simple, name + " не помечен simple");
        }
    }

    [Test]
    public void Includes_FullTakesEverything_SimpleOnlyTheMarked()
    {
        var plain = new McpToolDef("a", "A", "d", McpToolKind.Read, null);
        var marked = new McpToolDef("b", "B", "d", McpToolKind.Read, null, simple: true);

        Assert.IsTrue(McpToolProfiles.Includes(plain, McpToolProfile.Full));
        Assert.IsFalse(McpToolProfiles.Includes(plain, McpToolProfile.Simple));
        Assert.IsTrue(McpToolProfiles.Includes(marked, McpToolProfile.Simple));
    }

    [Test]
    public void SimpleTexts_NameNoHiddenTool()
    {
        var texts = new List<string> { McpSimpleGuideTexts.Instructions, McpSimpleGuideTexts.GuideDescription };
        texts.AddRange(McpSimpleGuideTexts.Topics.Values);
        var leaks = new List<string>();

        foreach (var name in Hidden().Where(n => !HiddenNamesThatAreAlsoPlainWords.Contains(n)))
            foreach (var text in texts)
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b"))
                    leaks.Add(name);

        CollectionAssert.IsEmpty(leaks.Distinct().ToList(),
            "простой клиент читает только петлю из простых инструментов; скрытое имя в тексте уводит его туда, где нет подсказок");
    }

    [Test]
    public void SimpleTexts_DoNameEveryListedTool_SoNothingInTheSetIsUnexplained()
    {
        var all = string.Join("\n", McpSimpleGuideTexts.Topics.Values) + McpSimpleGuideTexts.Instructions;

        foreach (var name in SimpleTools.Where(n => n != "guide" && n != "get"))
            StringAssert.Contains(name, all, name + " есть в списке, но нигде не объяснён");
    }

    [Test]
    public void SimpleWorkflow_SaysOtherToolsStayCallable_WithoutNamingThem()
    {
        StringAssert.Contains("called by exact name", McpSimpleGuideTexts.Instructions);
        StringAssert.Contains(McpSimpleGuideTexts.OtherToolsSentence, McpSimpleGuideTexts.Topics["workflow"],
            "фраза про вызов скрытых по имени обязана быть в workflow: на неё ссылаются замены «guide»");
    }

    [Test]
    public void SimpleTopics_AreTheAdvertisedOnes_AndTheDefaultIsAmongThem()
    {
        CollectionAssert.AreEquivalent(McpSimpleGuideTexts.TopicWords, McpSimpleGuideTexts.Topics.Keys.ToList());
        Assert.IsTrue(McpSimpleGuideTexts.Topics.ContainsKey(McpGuideTexts.DefaultTopic));
        Assert.AreSame(McpGuideTexts.Topics, McpGuideTexts.TopicsFor(McpToolProfile.Full));
        Assert.AreSame(McpSimpleGuideTexts.Topics, McpGuideTexts.TopicsFor(McpToolProfile.Simple));
        Assert.AreEqual(McpGuideTexts.Instructions, McpGuideTexts.InstructionsFor(McpToolProfile.Full));
        Assert.AreEqual(McpSimpleGuideTexts.Instructions, McpGuideTexts.InstructionsFor(McpToolProfile.Simple));
    }

    [Test]
    public void SimpleInstructions_AreMuchShorterThanTheFullOnes()
    {
        Assert.Less(McpSimpleGuideTexts.Instructions.Length, McpGuideTexts.Instructions.Length,
            "смысл профиля — меньше текста для слабой модели");
    }
}
