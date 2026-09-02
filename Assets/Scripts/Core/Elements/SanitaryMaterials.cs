using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SanitaryMaterials
    {
        private static Material? _whiteAcrylic;
        private static Material? _ceramic;
        private static Material? _chrome;

        public static Material WhiteAcrylic =>
            ApplianceMaterials.Cached(ref _whiteAcrylic,
                new Color(0.95f, 0.96f, 0.96f, 1f), 0f, 0.88f);

        public static Material Ceramic =>
            ApplianceMaterials.Cached(ref _ceramic,
                new Color(0.96f, 0.97f, 0.97f, 1f), 0f, 0.9f);

        public static Material Chrome =>
            ApplianceMaterials.Cached(ref _chrome,
                new Color(0.78f, 0.80f, 0.82f, 1f), 0.9f, 0.85f);
    }
}
