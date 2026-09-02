using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class PlumbingMaterials
    {
        private static Material? _chrome;
        private static Material? _matteBlack;

        public static Material Chrome =>
            ApplianceMaterials.Cached(ref _chrome,
                new Color(0.82f, 0.84f, 0.87f, 1f), 1f, 0.94f);

        public static Material MatteBlack =>
            ApplianceMaterials.Cached(ref _matteBlack,
                new Color(0.07f, 0.07f, 0.075f, 1f), 0.35f, 0.22f);
    }
}
