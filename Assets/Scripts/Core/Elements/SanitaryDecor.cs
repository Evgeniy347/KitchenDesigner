using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SanitaryDecor
    {
        public const string CeramicLabel = "Керамика";

        public const string ButtonLabel = "Кнопка";

        public const string PlateLabel = "Панель";

        public static bool IsFactoryLook(string? materialId)
            => string.IsNullOrEmpty(materialId) || materialId == MaterialCatalog.DefaultId;

        public static Material ChosenOrFactory(string? materialId, Material chosen,
            Material factory)
            => IsFactoryLook(materialId) ? factory : chosen;
    }
}
