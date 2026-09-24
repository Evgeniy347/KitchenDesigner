using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// L5a, чистая половина (план LEVELS / docs/todo_evolution.md §3.4, строка «Что видно»):
/// текущий уровень виден всегда целиком, уровень ВЫШЕ скрыт всегда (Sims 2 — крыша над
/// головой не рисуется), а уровень НИЖЕ подчиняется настройке «Соседние этажи»
/// (показывать/приглушать/скрывать, дефолт «показывать»). Настройка и реестр уровней
/// сюда не входят — только чистое решение по трём числам.
/// </summary>
public class LevelVisibilityTests
{
    [TestCase(NeighbourLevelsMode.Show)]
    [TestCase(NeighbourLevelsMode.Dim)]
    [TestCase(NeighbourLevelsMode.Hide)]
    public void SameLevel_IsAlwaysFullyVisible_RegardlessOfMode(NeighbourLevelsMode mode)
    {
        var decision = LevelVisibility.Decide(3000, 3000, mode);

        Assert.AreEqual(LevelVisibilityDecision.Visible, decision,
            "текущий уровень обязан быть виден целиком независимо от настройки соседей");
    }

    [TestCase(NeighbourLevelsMode.Show)]
    [TestCase(NeighbourLevelsMode.Dim)]
    [TestCase(NeighbourLevelsMode.Hide)]
    public void LevelAbove_IsAlwaysHidden_RegardlessOfMode(NeighbourLevelsMode mode)
    {
        var decision = LevelVisibility.Decide(6000, 3000, mode);

        Assert.AreEqual(LevelVisibilityDecision.Hidden, decision,
            "этаж выше текущего скрыт всегда — настройка «Соседние этажи» на него не влияет "
            + "(как в Sims 2: крыша сверху не рисуется, что бы ни стояло в настройках)");
    }

    [Test]
    public void LevelBelow_ModeShow_IsFullyVisible()
    {
        Assert.AreEqual(LevelVisibilityDecision.Visible,
            LevelVisibility.Decide(0, 3000, NeighbourLevelsMode.Show),
            "дефолт «показывать» — нижний этаж виден целиком, как в Sims 2");
    }

    [Test]
    public void LevelBelow_ModeDim_IsDimmed()
    {
        Assert.AreEqual(LevelVisibilityDecision.Dimmed,
            LevelVisibility.Decide(0, 3000, NeighbourLevelsMode.Dim));
    }

    [Test]
    public void LevelBelow_ModeHide_IsHidden()
    {
        Assert.AreEqual(LevelVisibilityDecision.Hidden,
            LevelVisibility.Decide(0, 3000, NeighbourLevelsMode.Hide));
    }

    [Test]
    public void TwoLevelsBelow_BehavesTheSameAsOneLevelBelow_DistanceDoesNotMatter()
    {
        Assert.AreEqual(
            LevelVisibility.Decide(0, 3000, NeighbourLevelsMode.Dim),
            LevelVisibility.Decide(-3000, 3000, NeighbourLevelsMode.Dim),
            "правило смотрит только на знак разницы отметок, а не на расстояние в мм");
    }
}
