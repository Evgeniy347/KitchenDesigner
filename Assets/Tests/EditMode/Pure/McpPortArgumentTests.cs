using NUnit.Framework;
using KitchenDesigner.Core;

public class McpPortArgumentTests
{
    private const int Fallback = 9337;

    [Test]
    public void Parse_ReturnsFallback_WhenArgumentIsAbsent()
    {
        var result = McpPortArgument.Parse(new[] { "-someOtherFlag", "42" }, Fallback);

        Assert.AreEqual(Fallback, result.Port);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsFallback_WhenArgsIsEmpty()
    {
        var result = McpPortArgument.Parse(new string[0], Fallback);

        Assert.AreEqual(Fallback, result.Port);
        Assert.IsNull(result.Warning);
    }

    [Test]
    public void Parse_ReturnsTheNumber_WhenArgumentIsAValidPort()
    {
        var result = McpPortArgument.Parse(new[] { "-mcpPort", "9500" }, Fallback);

        Assert.AreEqual(9500, result.Port);
        Assert.IsNull(result.Warning,
            "число в допустимом диапазоне не имеет права оставлять предупреждение в логе");
    }

    [Test]
    public void Parse_IsCaseInsensitive_OnTheFlagName()
    {
        var result = McpPortArgument.Parse(new[] { "-MCPPORT", "9500" }, Fallback);

        Assert.AreEqual(9500, result.Port);
    }

    [Test]
    public void Parse_FallsBackWithAWarning_WhenValueIsGarbage()
    {
        var result = McpPortArgument.Parse(new[] { "-mcpPort", "banana" }, Fallback);

        Assert.AreEqual(Fallback, result.Port,
            "мусор вместо числа не должен уронить приложение — оно обязано продолжить с портом по умолчанию");
        Assert.IsNotNull(result.Warning, "тихий откат к умолчанию без записи в лог невозможно отличить "
            + "от намеренного выбора порта — пользователь решит, что порт 9337, а он и правда 9337, но "
            + "по счастливой случайности, а не потому что аргумент сработал");
        StringAssert.Contains("banana", result.Warning);
    }

    [Test]
    public void Parse_FallsBackWithAWarning_WhenValueIsBelowTheValidRange()
    {
        var result = McpPortArgument.Parse(new[] { "-mcpPort", "0" }, Fallback);

        Assert.AreEqual(Fallback, result.Port);
        Assert.IsNotNull(result.Warning);
    }

    [Test]
    public void Parse_FallsBackWithAWarning_WhenValueIsAboveTheValidRange()
    {
        var result = McpPortArgument.Parse(new[] { "-mcpPort", "70000" }, Fallback);

        Assert.AreEqual(Fallback, result.Port);
        Assert.IsNotNull(result.Warning);
        StringAssert.Contains("70000", result.Warning);
    }

    [Test]
    public void Parse_FallsBackWithAWarning_WhenValueIsNegative()
    {
        var result = McpPortArgument.Parse(new[] { "-mcpPort", "-5" }, Fallback);

        Assert.AreEqual(Fallback, result.Port);
        Assert.IsNotNull(result.Warning);
    }

    [Test]
    public void Parse_AcceptsTheBoundaryPorts()
    {
        Assert.AreEqual(1, McpPortArgument.Parse(new[] { "-mcpPort", "1" }, Fallback).Port);
        Assert.AreEqual(65535, McpPortArgument.Parse(new[] { "-mcpPort", "65535" }, Fallback).Port);
    }

    [Test]
    public void Parse_ReturnsFallback_WhenFlagIsTheLastArgumentWithNoValueFollowing()
    {
        var result = McpPortArgument.Parse(new[] { "-mcpPort" }, Fallback);

        Assert.AreEqual(Fallback, result.Port,
            "флаг без значения не должен читать за пределы массива и не должен падать");
    }

    [Test]
    public void Parse_ReturnsFallback_WhenArgsIsNull()
    {
        var result = McpPortArgument.Parse(null, Fallback);

        Assert.AreEqual(Fallback, result.Port);
        Assert.IsNull(result.Warning);
    }
}
