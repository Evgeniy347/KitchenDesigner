using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SanitaryDecor
    {
        public static string CeramicLabel => Loc.T("decor.slot.ceramic");

        public static string ButtonLabel => Loc.T("decor.slot.button");

        public static string PlateLabel => Loc.T("decor.slot.flushPlate");

        public static bool IsFactoryLook(string? materialId)
            => string.IsNullOrEmpty(materialId) || materialId == MaterialCatalog.DefaultId;

        public static Material ChosenOrFactory(string? materialId, Material chosen,
            Material factory)
            => IsFactoryLook(materialId) ? factory : chosen;
    }
}
