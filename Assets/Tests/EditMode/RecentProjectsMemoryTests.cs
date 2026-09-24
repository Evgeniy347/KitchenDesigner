using NUnit.Framework;
using KitchenDesigner.Core;

public class RecentProjectsMemoryTests
{
    private static readonly string[] EphemeralArgs = { "-ephemeralSession" };

    [SetUp]
    public void ClearBetweenTests()
    {
        RecentProjectsMemory.For(EphemeralArgs).Values = new string[0];
    }

    [Test]
    public void Remember_PersistsAcrossReads_InTheSameSession()
    {
        RecentProjectsMemory.Remember(EphemeralArgs, "a.kdproj");
        RecentProjectsMemory.Remember(EphemeralArgs, "b.kdproj");

        var values = RecentProjectsMemory.For(EphemeralArgs).Values;
        CollectionAssert.AreEqual(new[] { "b.kdproj", "a.kdproj" }, values);
    }

    [Test]
    public void Remember_ReopeningAnOlderEntry_PromotesItToTheFront()
    {
        RecentProjectsMemory.Remember(EphemeralArgs, "a.kdproj");
        RecentProjectsMemory.Remember(EphemeralArgs, "b.kdproj");
        RecentProjectsMemory.Remember(EphemeralArgs, "a.kdproj");

        var values = RecentProjectsMemory.For(EphemeralArgs).Values;
        CollectionAssert.AreEqual(new[] { "a.kdproj", "b.kdproj" }, values);
    }

    [Test]
    public void Values_WhenNothingStored_IsEmpty()
    {
        Assert.AreEqual(0, RecentProjectsMemory.For(EphemeralArgs).Values.Length);
    }
}
