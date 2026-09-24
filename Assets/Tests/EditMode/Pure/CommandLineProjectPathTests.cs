using NUnit.Framework;
using KitchenDesigner.Core;

public class CommandLineProjectPathTests
{
    [Test]
    public void Parse_ReturnsNull_WhenArgsIsNull()
    {
        Assert.IsNull(CommandLineProjectPath.Parse(null));
    }

    [Test]
    public void Parse_ReturnsNull_WhenNoArgumentLooksLikeAProjectFile()
    {
        Assert.IsNull(CommandLineProjectPath.Parse(new[] { "-mcpPort", "9337" }));
    }

    [Test]
    public void Parse_FindsAKdprojPath()
    {
        Assert.AreEqual(@"C:\Kitchen.kdproj",
            CommandLineProjectPath.Parse(new[] { @"C:\Kitchen.kdproj" }));
    }

    [Test]
    public void Parse_FindsALegacyJsonPath()
    {
        Assert.AreEqual(@"C:\Kitchen.json",
            CommandLineProjectPath.Parse(new[] { @"C:\Kitchen.json" }));
    }

    [Test]
    public void Parse_SkipsFlagsAndTheirValues_AndFindsThePathAfterThem()
    {
        Assert.AreEqual(@"C:\Kitchen.kdproj", CommandLineProjectPath.Parse(
            new[] { "-mcpPort", "9337", "-muteAudio", @"C:\Kitchen.kdproj" }));
    }

    [Test]
    public void Parse_IgnoresAPathWithAnUnrelatedExtension()
    {
        Assert.IsNull(CommandLineProjectPath.Parse(new[] { @"C:\Readme.txt" }));
    }

    [Test]
    public void Parse_IgnoresEmptyEntries()
    {
        Assert.IsNull(CommandLineProjectPath.Parse(new[] { "", null! }));
    }
}
