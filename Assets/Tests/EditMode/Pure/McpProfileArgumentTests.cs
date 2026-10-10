using NUnit.Framework;
using KitchenDesigner.Core;

public class McpProfileArgumentTests
{
    [Test]
    public void Parse_NoArgument_MeansFull_SoTheDefaultBehaviourIsUnchanged()
    {
        Assert.AreEqual(McpToolProfile.Full, McpProfileArgument.Parse(new[] { "-mcpPort", "9400" }).Profile);
        Assert.AreEqual(McpToolProfile.Full, McpProfileArgument.Parse(new string[0]).Profile);
        Assert.AreEqual(McpToolProfile.Full, McpProfileArgument.Parse(null).Profile);
        Assert.IsNull(McpProfileArgument.Parse(null).Warning);
    }

    [Test]
    public void Parse_Simple_IsRecognised_IgnoringCaseOfTheFlagAndTheValue()
    {
        Assert.AreEqual(McpToolProfile.Simple, McpProfileArgument.Parse(new[] { "-mcpProfile", "simple" }).Profile);
        Assert.AreEqual(McpToolProfile.Simple, McpProfileArgument.Parse(new[] { "-MCPPROFILE", " Simple " }).Profile);
        Assert.IsNull(McpProfileArgument.Parse(new[] { "-mcpProfile", "simple" }).Warning);
    }

    [Test]
    public void Parse_FullWordedOut_IsFull_WithoutAWarning()
    {
        var result = McpProfileArgument.Parse(new[] { "-mcpProfile", "full" });

        Assert.AreEqual(McpToolProfile.Full, result.Profile);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_GarbageValue_FallsBackToFull_AndSaysSo_InsteadOfSilentlyHidingNothing()
    {
        var result = McpProfileArgument.Parse(new[] { "-mcpProfile", "tiny" });

        Assert.AreEqual(McpToolProfile.Full, result.Profile, "опечатка не должна урезать список инструментов у работающего клиента");
        StringAssert.Contains("tiny", result.Warning);
        StringAssert.Contains("-mcpProfile", result.Warning);
    }

    [Test]
    public void Parse_TheFlagWithoutAValue_IsIgnored_AndDoesNotReadPastTheArray()
    {
        var result = McpProfileArgument.Parse(new[] { "-mcpPort", "9400", "-mcpProfile" });

        Assert.AreEqual(McpToolProfile.Full, result.Profile);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_TheFlagAmongOthers_IsFoundWhereverItStands()
    {
        var result = McpProfileArgument.Parse(new[] { "-batchmode", "-mcpSaveDir", "C:\\x", "-mcpProfile", "simple", "-mcpPort", "9400" });

        Assert.AreEqual(McpToolProfile.Simple, result.Profile);
    }
}
