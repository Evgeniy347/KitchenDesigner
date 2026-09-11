using UnityEngine;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Handles
{
    public static class HandleHover
    {
        public static Color OverlayColor(bool hovered) =>
            hovered ? UIStyle.MeasureHover : UIStyle.HighlightChanged;

        public static Material? OverlayMaterial(bool hovered) =>
            HandleMaterials.For(OverlayColor(hovered));

        public static void Paint(ResizeHandle? handle, bool hovered)
        {
            if (handle == null) return;

            var material = HandleMaterials.For(hovered
                ? UIStyle.MeasureHover
                : HandleMaterials.ForAxis(handle.faceIndex / 2));

            foreach (var renderer in handle.GetComponentsInChildren<MeshRenderer>())
                if (renderer != null) renderer.sharedMaterial = material;
        }
    }
}
