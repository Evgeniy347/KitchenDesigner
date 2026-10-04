using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ToolbarIssueBadge
    {
        public const string NodeName = "Badge";

        private readonly RectTransform _pill;
        private readonly Image _fill;
        private readonly TMP_Text _label;

        private ToolbarIssueBadge(RectTransform pill, Image fill, TMP_Text label)
        {
            _pill = pill;
            _fill = fill;
            _label = label;
        }

        public bool Visible => _pill.gameObject.activeSelf;

        public string Text => _label.text;

        internal RectTransform Pill => _pill;

        internal Color LabelColor => _label.color;

        internal float FontSize => _label.fontSize;

        public static ToolbarIssueBadge Create(Transform button)
        {
            var pill = UIFactory.CreateRect(NodeName, button);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = new Vector2(-ToolbarMetrics.BadgeInsetRight, -ToolbarMetrics.BadgeInsetTop);
            var fill = pill.gameObject.AddComponent<Image>();
            fill.raycastTarget = false;
            RoundedRectSprites.Apply(fill, RoundedRectSprites.Fill(ToolbarMetrics.BadgeH * 0.5f));

            var label = UIFactory.CreateLabel(NodeName + "_Label", pill, string.Empty, UIStyle.FontCaption,
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            pill.gameObject.SetActive(false);
            return new ToolbarIssueBadge(pill, fill, label);
        }

        public void Show(int total, bool anyError)
        {
            _pill.gameObject.SetActive(total > 0);
            if (total <= 0) return;

            _label.text = total > ToolbarMetrics.BadgeMax ? ToolbarMetrics.BadgeMax + "+" : total.ToString();
            _label.color = anyError ? UIStyle.TextOnAccent : UIStyle.NavBg;
            _fill.color = anyError ? UIStyle.Danger : UIStyle.TextWarning;
            float width = Mathf.Max(ToolbarMetrics.BadgeH,
                _label.GetPreferredValues(_label.text).x + ToolbarMetrics.BadgePadX * 2f);
            _pill.sizeDelta = new Vector2(width, ToolbarMetrics.BadgeH);
        }
    }
}
