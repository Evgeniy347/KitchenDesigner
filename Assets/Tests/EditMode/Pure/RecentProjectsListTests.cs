using NUnit.Framework;
using KitchenDesigner.Core;

public class RecentProjectsListTests
{
    [Test]
    public void WithPromoted_PutsTheNewPathFirst_OnAnEmptyList()
    {
        var result = RecentProjectsList.WithPromoted(null, "a.kdproj");
        CollectionAssert.AreEqual(new[] { "a.kdproj" }, result);
    }

    [Test]
    public void WithPromoted_MovesAnExistingPathToTheFront_WithoutDuplicating()
    {
        var existing = new[] { "a.kdproj", "b.kdproj", "c.kdproj" };
        var result = RecentProjectsList.WithPromoted(existing, "b.kdproj");
        CollectionAssert.AreEqual(new[] { "b.kdproj", "a.kdproj", "c.kdproj" }, result);
    }

    [Test]
    public void WithPromoted_MatchesPathsCaseInsensitively()
    {
        var existing = new[] { "C:\\Projects\\Kitchen.kdproj" };
        var result = RecentProjectsList.WithPromoted(existing, "c:\\projects\\kitchen.kdproj");
        Assert.AreEqual(1, result.Length, "тот же файл в другом регистре не должен задваиваться");
    }

    [Test]
    public void WithPromoted_TrimsToCapacity()
    {
        var existing = new[] { "a", "b", "c" };
        var result = RecentProjectsList.WithPromoted(existing, "new", capacity: 3);
        CollectionAssert.AreEqual(new[] { "new", "a", "b" }, result);
    }

    [Test]
    public void WithPromoted_IgnoresAnEmptyPath()
    {
        var existing = new[] { "a", "b" };
        var result = RecentProjectsList.WithPromoted(existing, "");
        CollectionAssert.AreEqual(existing, result);
    }

    [Test]
    public void WithPromoted_DefaultCapacity_IsTen()
    {
        Assert.AreEqual(10, RecentProjectsList.Capacity);
    }

    [Test]
    public void WithPromoted_ReturnsEmpty_WhenCapacityIsZero()
    {
        var result = RecentProjectsList.WithPromoted(new[] { "a" }, "b", capacity: 0);
        Assert.AreEqual(0, result.Length);
    }

    [Test]
    public void Without_DropsOnlyTheNamedPath_AndKeepsTheOrder()
    {
        var result = RecentProjectsList.Without(new[] { "a.kdproj", "b.kdproj", "c.kdproj" }, "b.kdproj");
        CollectionAssert.AreEqual(new[] { "a.kdproj", "c.kdproj" }, result);
    }

    [Test]
    public void Without_MatchesCaseInsensitively_LikeWithPromoted()
    {
        var result = RecentProjectsList.Without(new[] { "C:\\Projects\\Kitchen.kdproj", "x" }, "c:\\projects\\kitchen.kdproj");
        CollectionAssert.AreEqual(new[] { "x" }, result);
    }

    [Test]
    public void Without_AnUnknownOrEmptyPath_ChangesNothing()
    {
        var existing = new[] { "a", "b" };
        CollectionAssert.AreEqual(existing, RecentProjectsList.Without(existing, "zzz"));
        CollectionAssert.AreEqual(existing, RecentProjectsList.Without(existing, ""));
        Assert.AreEqual(0, RecentProjectsList.Without(null, "a").Length);
    }

    [Test]
    public void Without_ReturnsACopy_NotTheCallersArray()
    {
        var existing = new[] { "a", "b" };
        var result = RecentProjectsList.Without(existing, "");
        result[0] = "changed";
        Assert.AreEqual("a", existing[0]);
    }
}
