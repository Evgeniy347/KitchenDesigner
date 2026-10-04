using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class WindowSurface
    {
        public const string ShadowNode = "Shadow";
        public const string FillNode = "Fill";
        public const string RimNode = "Rim";

        public static RectTransform Create(Transform parent, string name, Vector2 size)
        {
            var root = UIFactory.CreatePanel(name, parent, Vector2.zero, size, UIStyle.RaycastOnly);
            var rt = root.rectTransform;
            UIFactory.AnchorCenter(rt);
            rt.anchoredPosition = Vector2.zero;

            Layer(rt, ShadowNode, RoundedRectSprites.WindowShadow, UIStyle.WindowShadow,
                UIStyle.WindowShadowPx, new Vector2(0f, -UIStyle.Space1));
            Layer(rt, FillNode, RoundedRectSprites.WindowFill, UIStyle.Panel, 0f, Vector2.zero);
            Layer(rt, RimNode, RoundedRectSprites.WindowStroke, UIStyle.Divider, 0f, Vector2.zero);
            return rt;
        }

        public static Image AddRule(RectTransform panel, string name, float yFromTop)
        {
            var line = UIFactory.CreateRect(name, panel);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(0f, UIStyle.DividerPx);
            line.anchoredPosition = new Vector2(0f, yFromTop);
            var img = line.gameObject.AddComponent<Image>();
            img.color = UIStyle.Divider;
            img.raycastTarget = false;
            return img;
        }

        private static void Layer(RectTransform root, string name, Sprite sprite, Color color,
            float outset, Vector2 offset)
        {
            var layer = UIFactory.CreateRect(name, root);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = new Vector2(-outset, -outset) + offset;
            layer.offsetMax = new Vector2(outset, outset) + offset;
            layer.gameObject.AddComponent<WindowDecoration>();
            var img = layer.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(img, sprite);
            img.color = color;
            img.raycastTarget = false;
        }
    }
}
