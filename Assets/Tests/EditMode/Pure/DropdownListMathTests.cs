using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class DropdownListMathTests
{
    private static (float X, float Y) Shift(float lowX, float lowY, float highX, float highY) =>
        DropdownListMath.ShiftInto(lowX, lowY, highX, highY, -960f, -540f, 960f, 540f);

    [Test]
    public void ShiftInto_ListInside_DoesNotMove()
    {
        Assert.AreEqual((0f, 0f), Shift(-100f, -200f, 100f, -20f));
    }

    [Test]
    public void ShiftInto_ListBelowTheScreen_MovesUp()
    {
        Assert.AreEqual((0f, 30f), Shift(-100f, -570f, 100f, -400f));
    }

    [Test]
    public void ShiftInto_ListPastTheRightEdge_MovesLeft()
    {
        Assert.AreEqual((-40f, 0f), Shift(800f, 0f, 1000f, 160f));
    }

    [Test]
    public void ShiftInto_ListPastTheTopAndLeft_MovesRightAndDown()
    {
        Assert.AreEqual((20f, -15f), Shift(-980f, 380f, -800f, 555f));
    }

    [Test]
    public void ScrollOffset_SelectedInTheMiddle_CentresIt()
    {
        Assert.AreEqual(300f - 80f, DropdownListMath.ScrollOffset(300f, 160f, 960f), 0.001f);
    }

    [Test]
    public void ScrollOffset_SelectedFirst_StaysAtTheTop()
    {
        Assert.AreEqual(0f, DropdownListMath.ScrollOffset(12f, 160f, 960f), 0.001f);
    }

    [Test]
    public void ScrollOffset_SelectedLast_StopsAtTheBottom()
    {
        Assert.AreEqual(800f, DropdownListMath.ScrollOffset(950f, 160f, 960f), 0.001f);
    }

    [Test]
    public void ScrollOffset_EverythingFits_IsZero()
    {
        Assert.AreEqual(0f, DropdownListMath.ScrollOffset(60f, 160f, 100f), 0.001f);
    }

    [Test]
    public void OpensAbove_NoRoomBelowButRoomAbove_IsTrue()
    {
        Assert.IsTrue(DropdownListMath.OpensAbove(-560f, -540f, 900f, 160f));
    }

    [Test]
    public void OpensAbove_RoomBelow_IsFalse()
    {
        Assert.IsFalse(DropdownListMath.OpensAbove(-300f, -540f, 900f, 160f));
    }

    [Test]
    public void OpensAbove_NoRoomEitherSide_IsFalse()
    {
        Assert.IsFalse(DropdownListMath.OpensAbove(-560f, -540f, 100f, 160f));
    }
}
