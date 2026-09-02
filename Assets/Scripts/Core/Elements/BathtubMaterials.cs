using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class BathtubMaterials
    {
        private static Material? _whiteAcrylic;

        public static Material WhiteAcrylic =>
            ApplianceMaterials.Cached(ref _whiteAcrylic,
                new Color(0.95f, 0.96f, 0.96f, 1f), 0f, 0.88f);
    }
}
