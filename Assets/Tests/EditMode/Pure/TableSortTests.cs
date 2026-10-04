using System.Linq;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class TableSortTests
{
    private static int[] Order(string[] kinds, string[] keys, bool descending) =>
        TableSort.Order(kinds.Select(k => k == "i").ToArray(), kinds.Select(k => k == "g").ToArray(),
            i => keys[i], descending);

    [Test]
    public void TableSort_NumbersCompareAsNumbers_NotAsText()
    {
        var order = Order(new[] { "i", "i", "i" }, new[] { "100", "20", "3" }, descending: false);
        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, order,
            "«20» меньше «100»: колонка чисел сортируется по значению, а не по первой цифре");
    }

    [Test]
    public void TableSort_ReadsTheLocaleSeparatorAndTheTypographicMinus()
    {
        var order = Order(new[] { "i", "i", "i" }, new[] { "1,5", "\u22122", "0.5" }, descending: false);
        CollectionAssert.AreEqual(new[] { 1, 2, 0 }, order, "ячейки показаны через NumberFormat — и читаются им же");
    }

    [Test]
    public void TableSort_Descending_FlipsItems_ButKeepsTiesInInputOrder()
    {
        var order = Order(new[] { "i", "i", "i" }, new[] { "b", "a", "b" }, descending: true);
        CollectionAssert.AreEqual(new[] { 0, 2, 1 }, order, "сортировка устойчива: равные остаются в прежнем порядке");
    }

    [Test]
    public void TableSort_SortsInsideEachGroup_GroupAndSubtotalRowsStayPut()
    {
        var kinds = new[] { "g", "i", "i", "s", "g", "i", "i", "s" };
        var keys = new[] { "Мебель", "Полка", "Дно", "Итого", "Сантехника", "Смеситель", "Мойка", "Итого" };
        var order = Order(kinds, keys, descending: false);
        CollectionAssert.AreEqual(new[] { 0, 2, 1, 3, 4, 6, 5, 7 }, order,
            "раздел спецификации — строка-группа; строки сортируются внутри неё, подытог остаётся последним");
    }
}
