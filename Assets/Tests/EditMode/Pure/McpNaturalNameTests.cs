using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class McpNaturalNameTests
{
    [Test]
    public void Compare_NumbersInsideNamesAreComparedAsNumbers()
    {
        var sorted = new[] { "B10", "B2", "B1", "B100", "A9" }.OrderBy(n => n, Comparer<string>.Create(McpNaturalName.Compare)).ToArray();

        CollectionAssert.AreEqual(new[] { "A9", "B1", "B2", "B10", "B100" }, sorted);
    }

    [Test]
    public void Compare_CaseDoesNotReorderNames_ButEqualIgnoringCaseStillGetsAStableOrder()
    {
        Assert.Less(McpNaturalName.Compare("alpha", "Beta"), 0);
        Assert.AreNotEqual(0, McpNaturalName.Compare("Cab", "cab"), "два разных имени не должны считаться одним");
        Assert.AreEqual(-McpNaturalName.Compare("Cab", "cab"), McpNaturalName.Compare("cab", "Cab"));
    }

    [Test]
    public void Compare_APrefixComesBeforeTheLongerName()
    {
        Assert.Less(McpNaturalName.Compare("B4", "B4_Side"), 0);
        Assert.Greater(McpNaturalName.Compare("B4_Side", "B4"), 0);
    }

    [Test]
    public void Compare_LeadingZerosDoNotChangeTheNumber()
    {
        Assert.Less(McpNaturalName.Compare("B02", "B10"), 0);
        Assert.AreNotEqual(0, McpNaturalName.Compare("B007", "B7"));
    }

    [Test]
    public void Compare_TheSameName_IsEqual_AndNullIsTheEmptyName()
    {
        Assert.AreEqual(0, McpNaturalName.Compare("B4", "B4"));
        Assert.AreEqual(0, McpNaturalName.Compare(null, ""));
        Assert.Less(McpNaturalName.Compare(null, "A"), 0);
    }
}
