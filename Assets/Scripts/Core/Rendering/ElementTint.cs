using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementTint
    {
        public const string SelectionName = "KD Selection Tint";
        public const string DragName = "KD Drag Tint";
        public const string ValidityName = "KD Validity Tint";
        public const string HoverName = "KD Hover Tint";

        public static bool Wears(MeshRenderer renderer, Material? painted, string tintName)
        {
            if (renderer == null) return false;
            var current = renderer.sharedMaterial;
            if (current == null) return false;
            if (painted != null && ReferenceEquals(current, painted)) return true;
            return current.name.StartsWith(tintName, System.StringComparison.Ordinal);
        }
    }
}
