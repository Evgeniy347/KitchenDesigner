using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class HierarchyRectExtensions
    {
        public static void SetAnchor(this RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }
    }
}
