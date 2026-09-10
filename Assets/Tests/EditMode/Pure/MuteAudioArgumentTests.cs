using NUnit.Framework;
using KitchenDesigner.Core;

public class MuteAudioArgumentTests
{
    [Test]
    public void Parse_ReturnsFalse_WhenArgsIsNull()
    {
        Assert.IsFalse(MuteAudioArgument.Parse(null));
    }

    [Test]
    public void Parse_ReturnsFalse_WhenArgsIsEmpty()
    {
        Assert.IsFalse(MuteAudioArgument.Parse(new string[0]));
    }

    [Test]
    public void Parse_ReturnsFalse_WhenTheFlagIsAbsent()
    {
        Assert.IsFalse(MuteAudioArgument.Parse(new[] { "-mcpPort", "9337" }));
    }

    [Test]
    public void Parse_ReturnsTrue_WhenTheFlagIsPresent()
    {
        Assert.IsTrue(MuteAudioArgument.Parse(new[] { "-muteAudio" }));
    }

    [Test]
    public void Parse_ReturnsTrue_WhenTheFlagIsMixedWithOtherArguments()
    {
        Assert.IsTrue(MuteAudioArgument.Parse(new[] { "-mcpPort", "9337", "-muteAudio", "-mcpSaveDir", "C:\\tmp" }));
    }

    [Test]
    public void Parse_IsCaseInsensitive_OnTheFlagName()
    {
        Assert.IsTrue(MuteAudioArgument.Parse(new[] { "-MUTEAUDIO" }));
    }
}
