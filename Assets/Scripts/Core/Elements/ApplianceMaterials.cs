using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ApplianceMaterials
    {
        private static Material? _cooktopGlass;
        private static Material? _cooktopDecor;
        private static Material? _sinkSteel;
        private static Material? _sinkBowlBottom;

        public static Material CooktopGlass =>
            Cached(ref _cooktopGlass, new Color(0.08f, 0.08f, 0.08f, 1f), 0.05f, 0.92f);

        public static Material CooktopDecor =>
            Cached(ref _cooktopDecor, new Color(0.19f, 0.19f, 0.20f, 1f), 0.05f, 0.55f);

        public static Material SinkSteel =>
            Cached(ref _sinkSteel, new Color(0.72f, 0.74f, 0.76f, 1f), 0.85f, 0.75f);

        public static Material SinkBowlBottom =>
            Cached(ref _sinkBowlBottom, new Color(0.55f, 0.57f, 0.59f, 1f), 0.7f, 0.6f);

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
