using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class TableScrollTests
{
    [Test]
    public void OffsetToReveal_RowAlreadyInView_KeepsTheOffset()
    {
        Assert.AreEqual(40f, TableScroll.OffsetToReveal(40f, 64f, 32f, 200f, 1000f));
    }

    [Test]
    public void OffsetToReveal_RowAboveTheView_ScrollsUpToItsTop()
    {
        Assert.AreEqual(96f, TableScroll.OffsetToReveal(200f, 96f, 32f, 200f, 1000f),
            "стрелка вверх у верхней кромки: строка встаёт сверху, а не катит страницу целиком");
    }

    [Test]
    public void OffsetToReveal_RowBelowTheView_ScrollsDownToItsBottom()
    {
        Assert.AreEqual(132f, TableScroll.OffsetToReveal(0f, 300f, 32f, 200f, 1000f));
    }

    [Test]
    public void OffsetToReveal_NeverLeavesTheContent()
    {
        Assert.AreEqual(0f, TableScroll.OffsetToReveal(0f, 0f, 32f, 200f, 100f),
            "содержимое ниже окна просмотра прокручивать некуда");
        Assert.AreEqual(800f, TableScroll.OffsetToReveal(0f, 990f, 32f, 200f, 1000f));
    }

    [Test]
    public void OffsetToReveal_ZeroViewport_OnlyClamps()
    {
        Assert.AreEqual(500f, TableScroll.OffsetToReveal(500f, 10f, 32f, 0f, 1000f));
    }

    [Test]
    public void RowsPerPage_IsAtLeastOne_AndCountsWholeRows()
    {
        Assert.AreEqual(6, TableScroll.RowsPerPage(200f, 32f));
        Assert.AreEqual(1, TableScroll.RowsPerPage(10f, 32f));
        Assert.AreEqual(1, TableScroll.RowsPerPage(200f, 0f));
    }
}
