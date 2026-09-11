using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ApplianceMaterials
    {
        private static Material? _cooktopGlass;
        private static Material? _cooktopDecor;
        private static Material? _sinkSteel;
        private static Material? _sinkBowlBottom;
        private static Material? _ovenBody;
        private static Material? _ovenFacade;
        private static Material? _ovenGlass;
        private static Material? _ovenPanel;
        private static Material? _ovenHandle;
        private static Material? _dishwasherTank;
        private static Material? _dishwasherDoor;
        private static Material? _dishwasherPanel;
        private static Material? _laundryBody;
        private static Material? _laundryGlass;
        private static Material? _laundryPanel;

        public static Material CooktopGlass =>
            Cached(ref _cooktopGlass, new Color(0.08f, 0.08f, 0.08f, 1f), 0.05f, 0.92f);

        public static Material CooktopDecor =>
            Cached(ref _cooktopDecor, new Color(0.19f, 0.19f, 0.20f, 1f), 0.05f, 0.55f);

        public static Material SinkSteel =>
            Cached(ref _sinkSteel, new Color(0.72f, 0.74f, 0.76f, 1f), 0.85f, 0.75f);

        public static Material SinkBowlBottom =>
            Cached(ref _sinkBowlBottom, new Color(0.55f, 0.57f, 0.59f, 1f), 0.7f, 0.6f);

        public static Material OvenBody =>
            Cached(ref _ovenBody, new Color(0.45f, 0.45f, 0.46f, 1f), 0.15f, 0.35f);

        public static Material OvenFacade =>
            Cached(ref _ovenFacade, new Color(0.05f, 0.05f, 0.05f, 1f), 0.05f, 0.25f);

        public static Material OvenGlass =>
            Cached(ref _ovenGlass, new Color(0.03f, 0.03f, 0.035f, 1f), 0.05f, 0.95f);

        public static Material OvenPanel =>
            Cached(ref _ovenPanel, new Color(0.14f, 0.14f, 0.15f, 1f), 0.05f, 0.6f);

        public static Material OvenHandle =>
            Cached(ref _ovenHandle, new Color(0.22f, 0.22f, 0.23f, 1f), 0.6f, 0.7f);

        public static Material DishwasherTank =>
            Cached(ref _dishwasherTank, new Color(0.62f, 0.63f, 0.65f, 1f), 0.5f, 0.55f);

        public static Material DishwasherDoor =>
            Cached(ref _dishwasherDoor, new Color(0.18f, 0.18f, 0.19f, 1f), 0.1f, 0.35f);

        public static Material DishwasherPanel =>
            Cached(ref _dishwasherPanel, new Color(0.03f, 0.03f, 0.035f, 1f), 0.05f, 0.7f);

        public static Material LaundryBody =>
            Cached(ref _laundryBody, new Color(0.93f, 0.93f, 0.94f, 1f), 0.05f, 0.45f);

        public static Material LaundryGlass =>
            Cached(ref _laundryGlass, new Color(0.10f, 0.12f, 0.14f, 1f), 0.05f, 0.95f);

        public static Material LaundryPanel =>
            Cached(ref _laundryPanel, new Color(0.20f, 0.21f, 0.23f, 1f), 0.1f, 0.6f);

        internal static Material Cached(ref Material? slot, Color color, float metallic, float smoothness)
        {
            if (slot == null) slot = Lit(color, metallic, smoothness);
            return slot!;
        }

        internal static Material Lit(Color color, float metallic, float smoothness)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
