using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// L6a (план LEVELS): PageUp/PageDown и ▲▼ на панели инструментов ходят по уровням в
/// порядке ОТМЕТКИ, а не в порядке массива — порядок хранения (например, после удаления
/// и повторного создания уровня) не обязан совпадать с порядком по высоте.
/// </summary>
public class LevelNavigationTests
{
    private static readonly Level[] ThreeLevels =
    {
        new Level("2", "2 этаж", 3000, 3000),
        new Level("1", "1 этаж", 0, 3000),
        new Level("3", "3 этаж", 6000, 3000),
    };

    [Test]
    public void Up_FromTheMiddleLevel_GoesToTheHigherOne()
    {
        Assert.AreEqual("3", LevelNavigation.AdjacentLevelId(ThreeLevels, "2", +1));
    }

    [Test]
    public void Down_FromTheMiddleLevel_GoesToTheLowerOne()
    {
        Assert.AreEqual("1", LevelNavigation.AdjacentLevelId(ThreeLevels, "2", -1));
    }

    [Test]
    public void Up_FromTheTopLevel_StaysPut()
    {
        Assert.AreEqual("3", LevelNavigation.AdjacentLevelId(ThreeLevels, "3", +1),
            "выше верхнего этажа хода нет — ▲ обязана остаться на месте, а не обёртываться на первый");
    }

    [Test]
    public void Down_FromTheBottomLevel_StaysPut()
    {
        Assert.AreEqual("1", LevelNavigation.AdjacentLevelId(ThreeLevels, "1", -1),
            "ниже нижнего этажа хода нет — ▼ обязана остаться на месте");
    }

    [Test]
    public void OrderFollowsElevation_NotArrayOrder()
    {
        // ThreeLevels хранится как [2, 1, 3] — навигация обязана идти по 1 -> 2 -> 3.
        Assert.AreEqual("2", LevelNavigation.AdjacentLevelId(ThreeLevels, "1", +1));
    }

    [Test]
    public void UnknownCurrentId_FallsBackToTheLowestLevel()
    {
        Assert.AreEqual("2", LevelNavigation.AdjacentLevelId(ThreeLevels, "does-not-exist", +1),
            "неизвестный текущий id обязан вести себя как первый (самый нижний) этаж");
    }

    [Test]
    public void EmptyArray_ReturnsTheCurrentIdUnchanged()
    {
        Assert.AreEqual("whatever", LevelNavigation.AdjacentLevelId(new Level[0], "whatever", +1));
    }

    [Test]
    public void SingleLevel_StaysPutInBothDirections()
    {
        var one = new[] { new Level("1", "1 этаж", 0, 3000) };
        Assert.AreEqual("1", LevelNavigation.AdjacentLevelId(one, "1", +1));
        Assert.AreEqual("1", LevelNavigation.AdjacentLevelId(one, "1", -1));
    }
}
