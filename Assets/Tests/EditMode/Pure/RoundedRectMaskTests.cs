using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class RoundedRectMaskTests
{
    [Test]
    public void RoundedRectMask_Fill_IsOpaqueInside_ClearOutside()
    {
        Assert.AreEqual(1f, RoundedRectMask.Fill(10.5f, 10.5f, 20f, 20f, 4f), 1e-4f);
        Assert.AreEqual(0f, RoundedRectMask.Fill(25.5f, 10.5f, 20f, 20f, 4f), 1e-4f);
    }

    [Test]
    public void RoundedRectMask_Fill_CutsTheCorner_ButNotTheMiddleOfTheEdge()
    {
        Assert.AreEqual(0f, RoundedRectMask.Fill(0.5f, 0.5f, 20f, 20f, 4f), 1e-4f,
            "угловой пиксель скругления 4 px пуст: иначе скругление не видно, а 9-slice "
            + "растягивает этот угол на каждую кнопку");
        Assert.AreEqual(1f, RoundedRectMask.Fill(0.5f, 10.5f, 20f, 20f, 4f), 1e-4f,
            "середина стороны не скругляется");
    }

    [Test]
    public void RoundedRectMask_Fill_AntiAliasesTheEdge()
    {
        float edge = RoundedRectMask.Fill(20f, 10.5f, 20f, 20f, 4f);
        Assert.AreEqual(0.5f, edge, 1e-4f, "пиксель, через центр которого проходит край, — полупрозрачный");
    }

    [Test]
    public void RoundedRectMask_Ring_IsEmptyInTheMiddle_AndFullOnTheBorder()
    {
        Assert.AreEqual(0f, RoundedRectMask.Ring(10.5f, 10.5f, 20f, 20f, 4f, 1f), 1e-4f,
            "контур — только рамка: середина остаётся под заливкой поля");
        Assert.AreEqual(1f, RoundedRectMask.Ring(0.5f, 10.5f, 20f, 20f, 4f, 1f), 1e-4f);
        Assert.AreEqual(0f, RoundedRectMask.Ring(2.5f, 10.5f, 20f, 20f, 4f, 1f), 1e-4f,
            "толщина 1 px: второй пиксель внутрь уже пуст");
    }

    [Test]
    public void RoundedRectMask_Radius_IsClampedToHalfTheShortSide_SoAPillStaysAPill()
    {
        float d = RoundedRectMask.SignedDistance(10f, 0f, 40f, 20f, 100f);
        Assert.AreEqual(0f, d, 1e-4f,
            "радиус больше половины высоты даёт капсулу (тумблер 40×20), а не вывернутую фигуру");
    }

    [Test]
    public void RoundedRectMask_Shadow_FadesOutwards()
    {
        float inside = RoundedRectMask.Shadow(20f, 20f, 40f, 40f, 6f, 8f);
        float edge = RoundedRectMask.Shadow(40f, 20f, 40f, 40f, 6f, 8f);
        float far = RoundedRectMask.Shadow(46f, 20f, 40f, 40f, 6f, 8f);
        Assert.AreEqual(1f, inside, 1e-4f);
        Assert.AreEqual(0.5f, edge, 1e-4f);
        Assert.AreEqual(0f, far, 1e-4f);
    }
}
