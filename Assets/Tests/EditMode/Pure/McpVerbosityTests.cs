using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class McpVerbosityTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("terse")]
    [TestCase("TERSE")]
    public void TryParse_NothingOrTerse_IsTheDefault(string? text)
    {
        Assert.IsTrue(McpVerbosity.TryParse(text, out bool full, out var error), error);
        Assert.IsFalse(full);
    }

    [TestCase("full")]
    [TestCase(" Full ")]
    public void TryParse_Full_AsksForTheWholeElementInfo(string text)
    {
        Assert.IsTrue(McpVerbosity.TryParse(text, out bool full, out _));
        Assert.IsTrue(full);
    }

    [Test]
    public void TryParse_AnythingElse_IsRefusedWithTheValidWords()
    {
        Assert.IsFalse(McpVerbosity.TryParse("chatty", out bool full, out var error));
        Assert.IsFalse(full);
        StringAssert.Contains("Unknown verbosity 'chatty'", error);
        StringAssert.Contains("terse | full", error);
    }

    [Test]
    public void ReplyShape_ParsesTheRefAndTheVerbosityTogether()
    {
        Assert.IsTrue(McpReplyShape.TryParse("center-bottom", "full", out var reference, out bool full, out var error), error);
        Assert.AreEqual("center-bottom-center", reference.Canonical);
        Assert.IsTrue(full);
    }

    [Test]
    public void ReplyShape_ABadRefIsReportedBeforeTheVerbosity()
    {
        Assert.IsFalse(McpReplyShape.TryParse("sideways", "chatty", out _, out _, out var error));
        StringAssert.DoesNotContain("verbosity", error, "ref идёт первым в сообщении: он и в запросе первым");
    }

    [Test]
    public void ReplyShape_AGoodRefWithABadVerbosity_NamesTheVerbosity()
    {
        Assert.IsFalse(McpReplyShape.TryParse("left", "chatty", out _, out _, out var error));
        StringAssert.Contains("verbosity", error);
    }

    [Test]
    public void ReplyShape_Defaults_AreTheMinCornerAndTerse()
    {
        Assert.IsTrue(McpReplyShape.TryParse(null, null, out var reference, out bool full, out _));
        Assert.IsTrue(reference.IsMinCorner);
        Assert.IsFalse(full);
    }
}
