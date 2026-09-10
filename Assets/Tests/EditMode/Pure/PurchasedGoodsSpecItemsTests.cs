using UnityEngine;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Покупное изделие — одна строка в штуках, с габаритами для справки, без
/// материала (у него нет декора, который выбирает пользователь) и без площади
/// (площадь у листовых деталей, а не у комплекта техники).</summary>
public class PurchasedGoodsSpecItemsTests
{
    [Test]
    public void Piece_ReportsOnePieceInThePurchasedGoodsSection()
    {
        var item = PurchasedGoodsSpecItems.Piece("Мойка", new Vector3Int(500, 188, 500));

        Assert.AreEqual(PurchasedGoodsSpecItems.Section, item.section);
        Assert.AreEqual("Мойка", item.name);
        Assert.AreEqual(SpecUnit.Pieces, item.unit);
        Assert.AreEqual(1f, item.qty);
        Assert.IsTrue(item.hasDims);
        Assert.AreEqual(new Vector3Int(500, 188, 500), item.dimsMM);
        Assert.AreEqual("", item.material);
    }

    /// <summary>Противоположный вход: другое имя и другие габариты дают другую
    /// строку — GroupKey не сворачивает разные изделия в одну.</summary>
    [Test]
    public void Piece_DifferentNameOrDims_DifferentGroupKey()
    {
        var sink = PurchasedGoodsSpecItems.Piece("Мойка", new Vector3Int(500, 188, 500));
        var oven = PurchasedGoodsSpecItems.Piece("Bosch HBA514BB3", new Vector3Int(594, 595, 568));

        Assert.AreNotEqual(sink.GroupKey(), oven.GroupKey());
    }
}
