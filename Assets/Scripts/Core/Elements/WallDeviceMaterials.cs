using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class WallDeviceMaterials
    {
        private static Material? _plastic;
        private static Material? _contact;
        private static Material? _key;

        public static Material Plastic =>
            ApplianceMaterials.Cached(ref _plastic,
                new Color(0.94f, 0.94f, 0.93f, 1f), 0f, 0.35f);

        public static Material Contact =>
            ApplianceMaterials.Cached(ref _contact,
                new Color(0.12f, 0.12f, 0.13f, 1f), 0.35f, 0.55f);

        public static Material Key =>
            ApplianceMaterials.Cached(ref _key,
                new Color(0.97f, 0.97f, 0.96f, 1f), 0f, 0.45f);
    }
}
