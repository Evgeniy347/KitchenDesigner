using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class TableColumnLayoutTests
{
    [Test]
    public void TableColumnLayout_FlexibleColumn_TakesWhatTheFixedOnesLeave()
    {
        var widths = TableColumnLayout.Widths(new[] { 40f, 0f, 60f }, 300f);
        CollectionAssert.AreEqual(new[] { 40f, 200f, 60f }, widths,
            "одна колонка (наименование) тянется на остаток — числа справа держат свою ширину");
        CollectionAssert.AreEqual(new[] { 0f, 40f, 240f }, TableColumnLayout.Lefts(widths));
    }

    [Test]
    public void TableColumnLayout_NoRoomLeft_FlexibleCollapsesToZero_NotNegative()
    {
        var widths = TableColumnLayout.Widths(new[] { 200f, 0f, 200f }, 300f);
        Assert.AreEqual(0f, widths[1]);
    }
}
