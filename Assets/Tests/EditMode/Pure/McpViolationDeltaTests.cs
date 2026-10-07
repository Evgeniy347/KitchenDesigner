using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class McpViolationDeltaTests
{
    [Test]
    public void Between_AViolationThatWasThereBefore_IsNeitherAddedNorRemoved()
    {
        var delta = McpViolationDelta.Between(new[] { "Old1", "Old2" }, new[] { "Old1", "Old2" });

        Assert.IsEmpty(delta.added, "чужое старое нарушение вызов не создавал");
        Assert.IsEmpty(delta.removed);
    }

    [Test]
    public void Between_OnlyTheCallersOwnEffectIsReported_InASceneWithPreExistingViolations()
    {
        var before = new[] { "Old1", "Old2", "Old3" };
        var after = new[] { "Old1", "Old2", "Old3", "NewBoard" };

        var delta = McpViolationDelta.Between(before, after);

        CollectionAssert.AreEqual(new[] { "NewBoard" }, delta.added);
        Assert.IsEmpty(delta.removed, "три старых остались: счётчик 4 вместо 3 не должен прятать, что виновата одна деталь");
    }

    [Test]
    public void Between_AFixedViolation_IsRemoved_AndAnUndoneCreationToo()
    {
        var delta = McpViolationDelta.Between(new[] { "A", "B" }, new[] { "A" });

        CollectionAssert.AreEqual(new[] { "B" }, delta.removed);
        Assert.IsEmpty(delta.added);
    }

    [Test]
    public void Between_AddedAndRemovedAtOnce_AreBothReported()
    {
        var delta = McpViolationDelta.Between(new[] { "Fixed" }, new[] { "Broken" });

        CollectionAssert.AreEqual(new[] { "Broken" }, delta.added);
        CollectionAssert.AreEqual(new[] { "Fixed" }, delta.removed);
    }

    [Test]
    public void Between_NamesAreSortedAndDeduplicated_SoTheAnswerDoesNotDependOnSetOrder()
    {
        var delta = McpViolationDelta.Between(new string[0], new[] { "c", "a", "b", "a" });

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, delta.added);
    }

    [Test]
    public void Between_AHugeBreakage_IsCappedWithACountOfTheRest()
    {
        var many = Enumerable.Range(0, McpViolationDelta.MaxNamedPerSide + 5).Select(i => "P" + i.ToString("D2")).ToArray();

        var delta = McpViolationDelta.Between(new string[0], many);

        Assert.AreEqual(McpViolationDelta.MaxNamedPerSide + 1, delta.added.Count);
        Assert.AreEqual("+5 more", delta.added.Last(),
            "модель должна знать, что имён больше, чем показано, а не принять обрезку за полный список");
    }
}
