using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

public class McpRunGuideTextTests
{
    [Test]
    public void RunTopic_ExistsInTheGuide_AndTheGuideParameterOffersIt()
    {
        Assert.IsTrue(McpGuideTexts.Topics.ContainsKey("run"));
        var topicField = typeof(ParamsGuide).GetField("topic")!;
        var offered = topicField.GetCustomAttribute<McpParamAttribute>()!.Enum;
        CollectionAssert.Contains(offered, "run", "тема есть, но агент о ней не узнает из схемы guide");
    }

    [Test]
    public void RunTopic_QuotesTheKindDefaultsFromTheConstants_NotFromMemory()
    {
        var text = McpRunGuideText.Text;

        foreach (var number in new[]
                 {
                     RunKindDefaults.BaseHeightMm, RunKindDefaults.BaseDepthMm, RunKindDefaults.WallHeightMm,
                     RunKindDefaults.WallDepthMm, RunKindDefaults.WallHangsAboveFloorMm, RunKindDefaults.TallHeightMm,
                     RunKindDefaults.TallDepthMm,
                 })
            StringAssert.Contains(number.ToString(), text);
        Assert.IsFalse(text.Contains("@BASE_H@") || text.Contains("@WALL_LIFT@"), "заглушка осталась в тексте: подстановка не сработала");
    }

    [Test]
    public void RunTopic_NamesEveryParameterOfApplyRun()
    {
        var text = McpRunGuideText.Text;

        foreach (var field in typeof(ParamsApplyRun).GetFields().Select(f => f.Name))
        {
            if (field == "ref" || field == "verbosity") continue;
            StringAssert.Contains(field, text, "параметр apply_run без слова в теме run: агент о нём не прочтёт");
        }
        foreach (var field in typeof(RunModule).GetFields().Select(f => f.Name))
            StringAssert.Contains(field, text);
    }
}
