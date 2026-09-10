using NUnit.Framework;
using KitchenDesigner.Core;

public class McpSaveDirectoryArgumentTests
{
    [Test]
    public void Parse_ReturnsNull_WhenArgumentIsAbsent()
    {
        var result = McpSaveDirectoryArgument.Parse(new[] { "-someOtherFlag", "C:\\x" });

        Assert.IsNull(result.Directory);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsNull_WhenArgsIsEmpty()
    {
        var result = McpSaveDirectoryArgument.Parse(new string[0]);

        Assert.IsNull(result.Directory);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsNull_WhenArgsIsNull()
    {
        var result = McpSaveDirectoryArgument.Parse(null);

        Assert.IsNull(result.Directory);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsTheDirectory_WhenArgumentIsPresent()
    {
        var result = McpSaveDirectoryArgument.Parse(new[] { "-mcpSaveDir", "C:\\Saves\\MCP" });

        Assert.AreEqual("C:\\Saves\\MCP", result.Directory);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_IsCaseInsensitive_OnTheFlagName()
    {
        var result = McpSaveDirectoryArgument.Parse(new[] { "-MCPSAVEDIR", "C:\\Saves\\MCP" });

        Assert.AreEqual("C:\\Saves\\MCP", result.Directory);
    }

    [Test]
    public void Parse_ReturnsNullWithAWarning_WhenValueIsAnEmptyString()
    {
        var result = McpSaveDirectoryArgument.Parse(new[] { "-mcpSaveDir", "" });

        Assert.IsNull(result.Directory,
            "пустая строка вместо каталога не должна тихо превратиться в 'разрешено сохранять куда угодно'");
        Assert.IsNotNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsNullWithAWarning_WhenValueIsWhitespace()
    {
        var result = McpSaveDirectoryArgument.Parse(new[] { "-mcpSaveDir", "   " });

        Assert.IsNull(result.Directory);
        Assert.IsNotNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsNull_WhenFlagIsTheLastArgumentWithNoValueFollowing()
    {
        var result = McpSaveDirectoryArgument.Parse(new[] { "-mcpSaveDir" });

        Assert.IsNull(result.Directory,
            "флаг без значения не должен читать за пределы массива и не должен падать");
    }
}
