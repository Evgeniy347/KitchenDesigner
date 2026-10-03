using NUnit.Framework;
using KitchenDesigner.Core;

public class LevelSwitcherStateTests
{
    private static Level L(string id, int elevationMm) => new Level(id, id + " этаж", elevationMm, 3000);

    private static readonly Level[] ThreeLevelsStoredOutOfOrder =
    {
        L("2", 3000), L("1", 0), L("3", 6000),
    };

    [Test]
    public void OneLevel_ShowsNoArrows_AndNothingToPress()
    {
        var state = LevelSwitcherState.Of(new[] { L("1", 0) }, "1");

        Assert.IsFalse(state.ShowArrows,
            "один этаж: ▲ ▼ и подпись не нужны, остаётся только кнопка «Этажи…»");
        Assert.IsFalse(state.CanGoUp, "с одним этажом ▲ нажать некуда");
        Assert.IsFalse(state.CanGoDown, "с одним этажом ▼ нажать некуда");
    }

    [Test]
    public void NoLevels_BehavesLikeOneLevel()
    {
        var state = LevelSwitcherState.Of(new Level[0], "");

        Assert.IsFalse(state.ShowArrows);
        Assert.IsFalse(state.CanGoUp);
        Assert.IsFalse(state.CanGoDown);
    }

    [Test]
    public void NullLevels_BehavesLikeOneLevel()
    {
        var state = LevelSwitcherState.Of(null, null);

        Assert.IsFalse(state.ShowArrows);
        Assert.IsFalse(state.CanGoUp);
        Assert.IsFalse(state.CanGoDown);
    }

    [Test]
    public void TwoLevels_ShowArrows()
    {
        var state = LevelSwitcherState.Of(new[] { L("1", 0), L("2", 3000) }, "1");

        Assert.IsTrue(state.ShowArrows, "два этажа и больше: переключатель виден целиком");
    }

    [Test]
    public void BottomLevel_CanGoUp_NotDown()
    {
        var state = LevelSwitcherState.Of(ThreeLevelsStoredOutOfOrder, "1");

        Assert.IsTrue(state.CanGoUp);
        Assert.IsFalse(state.CanGoDown, "ниже нижнего этажа хода нет — ▼ выключена");
    }

    [Test]
    public void TopLevel_CanGoDown_NotUp()
    {
        var state = LevelSwitcherState.Of(ThreeLevelsStoredOutOfOrder, "3");

        Assert.IsFalse(state.CanGoUp, "выше верхнего этажа хода нет — ▲ выключена");
        Assert.IsTrue(state.CanGoDown);
    }

    [Test]
    public void MiddleLevel_CanGoBothWays()
    {
        var state = LevelSwitcherState.Of(ThreeLevelsStoredOutOfOrder, "2");

        Assert.IsTrue(state.CanGoUp);
        Assert.IsTrue(state.CanGoDown);
    }

    [Test]
    public void ArrayOrderIsIrrelevant_OnlyElevationDecidesTopAndBottom()
    {
        var state = LevelSwitcherState.Of(ThreeLevelsStoredOutOfOrder, "2");
        var reversed = LevelSwitcherState.Of(
            new[] { ThreeLevelsStoredOutOfOrder[2], ThreeLevelsStoredOutOfOrder[0], ThreeLevelsStoredOutOfOrder[1] }, "2");

        Assert.AreEqual(state, reversed);
    }

    [Test]
    public void UnknownCurrentId_ActsAsTheLowestLevel()
    {
        var state = LevelSwitcherState.Of(ThreeLevelsStoredOutOfOrder, "does-not-exist");

        Assert.IsTrue(state.CanGoUp);
        Assert.IsFalse(state.CanGoDown, "LevelNavigation считает неизвестный id нижним этажом — кнопки обязаны с ним согласиться");
    }

    [TestCase("1", +1)]
    [TestCase("1", -1)]
    [TestCase("2", +1)]
    [TestCase("2", -1)]
    [TestCase("3", +1)]
    [TestCase("3", -1)]
    [TestCase("nope", +1)]
    [TestCase("nope", -1)]
    public void ButtonIsEnabled_ExactlyWhenPressingItMovesTheCurrentLevel(string currentId, int direction)
    {
        var state = LevelSwitcherState.Of(ThreeLevelsStoredOutOfOrder, currentId);
        string here = LevelNavigation.AdjacentLevelId(ThreeLevelsStoredOutOfOrder, currentId, 0);
        bool moves = LevelNavigation.AdjacentLevelId(ThreeLevelsStoredOutOfOrder, currentId, direction) != here;

        Assert.AreEqual(moves, direction > 0 ? state.CanGoUp : state.CanGoDown,
            "кнопка, что ничего не сдвинет, обязана быть выключена, а рабочая — включена");
    }

    [Test]
    public void SingleLevelWithAnyCurrentId_NeverEnablesArrows()
    {
        var state = LevelSwitcherState.Of(new[] { L("1", 0) }, "ghost");

        Assert.IsFalse(state.CanGoUp);
        Assert.IsFalse(state.CanGoDown);
    }
}
