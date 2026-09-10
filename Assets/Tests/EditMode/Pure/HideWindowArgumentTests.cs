using NUnit.Framework;
using KitchenDesigner.Core;

public class HideWindowArgumentTests
{
    [Test]
    public void Parse_ReturnsFalse_WhenArgsIsNull()
    {
        Assert.IsFalse(HideWindowArgument.Parse(null));
    }

    [Test]
    public void Parse_ReturnsFalse_WhenArgsIsEmpty()
    {
        Assert.IsFalse(HideWindowArgument.Parse(new string[0]));
    }

    [Test]
    public void Parse_ReturnsFalse_WhenTheFlagIsAbsent()
    {
        Assert.IsFalse(HideWindowArgument.Parse(new[] { "-mcpPort", "9337" }));
    }

    [Test]
    public void Parse_ReturnsTrue_WhenTheFlagIsPresent()
    {
        Assert.IsTrue(HideWindowArgument.Parse(new[] { "-hideWindow" }));
    }

    [Test]
    public void Parse_ReturnsTrue_WhenTheFlagIsMixedWithOtherArguments()
    {
        Assert.IsTrue(HideWindowArgument.Parse(new[] { "-mcpPort", "9337", "-hideWindow", "-muteAudio" }));
    }

    [Test]
    public void Parse_IsCaseInsensitive_OnTheFlagName()
    {
        Assert.IsTrue(HideWindowArgument.Parse(new[] { "-HIDEWINDOW" }));
    }
}
