using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class HeatingMaterials
    {
        private static Material? _supply;
        private static Material? _return;

        public static Material Supply =>
            ApplianceMaterials.Cached(ref _supply,
                new Color(0.72f, 0.26f, 0.18f, 1f), 0.45f, 0.5f);

        public static Material Return =>
            ApplianceMaterials.Cached(ref _return,
                new Color(0.18f, 0.38f, 0.70f, 1f), 0.45f, 0.5f);
    }
}
