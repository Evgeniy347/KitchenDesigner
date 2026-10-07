using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class McpJsonWholeNumberTests
{
    [Test]
    public void McpJson_WritesWholeNumbersWithoutADecimalPoint_AndKeepsATenthWhenThereIsOne()
    {
        var json = McpJson.Serialize(new { posMm = new[] { 600f, 12.5f, -0.0000001f, 719.96f }, rotYDeg = 90f });

        Assert.AreEqual("{\"posMm\":[600,12.5,0,720],\"rotYDeg\":90}", json,
            "600.0 вместо 600 - два лишних байта на каждое число ответа; на ответе из тридцати чисел это "
            + "бюджет, за который слабой модели приходится платить контекстом");
    }

    [Test]
    public void McpJson_LeavesNonFiniteNumbersAlone_InsteadOfTurningThemIntoAnIntegerOverflow()
    {
        var json = McpJson.Serialize(new { v = float.PositiveInfinity });

        StringAssert.DoesNotContain("9223372036854775807", json,
            "бесконечность не равна своему floor-у только в мыслях автора: (long)Infinity было бы мусором в ответе");
    }
}
