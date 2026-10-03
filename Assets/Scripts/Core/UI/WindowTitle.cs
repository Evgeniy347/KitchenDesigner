using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class WindowTitleMarker : MonoBehaviour
    {
    }

    public static class WindowTitle
    {
        public static TextMeshProUGUI Create(Transform panel, string name, string text, int fontSize,
            float width, float height, TextAnchor align = TextAnchor.MiddleCenter, float inset = 0f)
        {
            var label = UIFactory.CreateLabel(name, panel, text, fontSize, Vector2.zero,
                new Vector2(width, height), align);
            var rt = label.rectTransform;
            bool leftAligned = align == TextAnchor.MiddleLeft;
            float anchorX = leftAligned ? 0f : 0.5f;
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.anchoredPosition = new Vector2(inset, -UIStyle.WindowTitleCenterFromTop);
            Mark(label);
            return label;
        }

        public static void Mark(Component title)
        {
            if (title.GetComponent<WindowTitleMarker>() == null)
                title.gameObject.AddComponent<WindowTitleMarker>();
        }
    }
}
