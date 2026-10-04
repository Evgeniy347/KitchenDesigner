using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ProgressBar
    {
        public const float Height = UIStyle.Space2;

        private readonly RectTransform _fill;

        private ProgressBar(RectTransform root, RectTransform fill)
        {
            Root = root;
            _fill = fill;
        }

        public RectTransform Root { get; }

        public float Value { get; private set; }

        public static ProgressBar Create(Transform parent, string name)
        {
            var root = UIFactory.CreateRect(name, parent);
            root.anchorMin = Vector2.zero;
            root.anchorMax = new Vector2(1f, 1f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            Paint(root, UIStyle.Field);

            var fill = UIFactory.CreateRect(name + "Fill", root);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Paint(fill, UIStyle.Accent);
            return new ProgressBar(root, fill);
        }

        public void SetValue(float t01)
        {
            Value = Mathf.Clamp01(t01);
            _fill.anchorMax = new Vector2(Value, 1f);
        }

        private static void Paint(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(image, RoundedRectSprites.Fill(Height * 0.5f));
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
