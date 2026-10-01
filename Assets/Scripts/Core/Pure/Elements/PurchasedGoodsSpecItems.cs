using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PurchasedGoodsSpecItems
    {
        public static string Section => SpecSections.PurchasedGoods;

        public static SpecItem Piece(string name, Vector3Int dimsMM) =>
            new SpecItem(Section, name, "", SpecUnit.Pieces, 1f, dimsMM, hasDims: true);
    }
}
