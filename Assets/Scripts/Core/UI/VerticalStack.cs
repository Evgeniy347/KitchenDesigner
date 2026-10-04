using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class VerticalStack
    {
        private readonly RectTransform _host;
        private float _cursor;

        public VerticalStack(RectTransform host, float width, float startY = 0f)
        {
            _host = host;
            Width = width;
            _cursor = startY;
        }

        public RectTransform Host => _host;

        public float Width { get; }

        public float Height => _cursor;

        public void Gap(float px) => _cursor += px;

        public RectTransform Place(RectTransform rect, float indent = 0f)
        {
            rect.SetParent(_host, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(LayoutDirection.IsRtl ? Width - indent - rect.sizeDelta.x : indent,
                -_cursor);
            _cursor += rect.sizeDelta.y;
            return rect;
        }

        public TextMeshProUGUI Text(string name, string text, int fontSize, Color color, float indent = 0f)
        {
            float width = Width - indent;
            var label = UIFactory.CreateLabel(name, _host, text, fontSize, Vector2.zero,
                new Vector2(width, 0f), TextAnchor.UpperLeft);
            label.color = color;
            label.enableWordWrapping = true;
            float h = Mathf.Ceil(label.GetPreferredValues(text, width, 0f).y);
            label.rectTransform.sizeDelta = new Vector2(width, h);
            Place(label.rectTransform, indent);
            return label;
        }
    }
}
