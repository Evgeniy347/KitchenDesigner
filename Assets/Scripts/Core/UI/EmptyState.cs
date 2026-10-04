using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class EmptyState
    {
        public const string TitleNode = "EmptyTitle";
        public const string HintNode = "EmptyHint";
        public const string ActionNode = "EmptyAction";

        private EmptyState(RectTransform root, TMP_Text title, TMP_Text hint, Button? action)
        {
            Root = root;
            Title = title;
            Hint = hint;
            ActionButton = action;
        }

        public RectTransform Root { get; }

        public TMP_Text Title { get; }

        public TMP_Text Hint { get; }

        public Button? ActionButton { get; }

        public static EmptyState Create(Transform parent, string name, string title, string hint,
            string? actionCaption = null, System.Action? onAction = null, float width = 360f)
        {
            var root = UIFactory.CreateRect(name, parent);
            var stack = UIFactory.CreateRect(name + "_Stack", root);
            stack.anchorMin = stack.anchorMax = stack.pivot = new Vector2(0.5f, 0.5f);

            var titleLabel = Line(stack, TitleNode, title, UIStyle.FontBody, UIStyle.Text, width);
            var hintLabel = Line(stack, HintNode, hint, UIStyle.FontSmall, UIStyle.TextSecondary, width);
            float h = titleLabel.rectTransform.sizeDelta.y + UIStyle.Space2 + hintLabel.rectTransform.sizeDelta.y;

            Button? action = null;
            if (actionCaption != null && onAction != null)
            {
                action = UIFactory.CreateButton(ActionNode, stack, actionCaption, Vector2.zero,
                    new Vector2(WindowFooter.MinButtonW, UIStyle.ControlH), onAction);
                var label = action.GetComponentInChildren<TMP_Text>();
                ((RectTransform)action.transform).sizeDelta =
                    new Vector2(WindowFooter.WidthFor(label, actionCaption), UIStyle.ControlH);
                ButtonRoles.Paint(action, ButtonRole.Primary);
                h += UIStyle.Space4 + UIStyle.ControlH;
            }

            stack.sizeDelta = new Vector2(width, h);
            float y = 0f;
            y = Place(titleLabel.rectTransform, y) + UIStyle.Space2;
            y = Place(hintLabel.rectTransform, y);
            if (action != null) Place((RectTransform)action.transform, y + UIStyle.Space4);
            return new EmptyState(root, titleLabel, hintLabel, action);
        }

        private static TextMeshProUGUI Line(RectTransform stack, string name, string text, int size, Color color, float width)
        {
            var label = UIFactory.CreateLabel(name, stack, text, size, Vector2.zero, new Vector2(width, 0f),
                TextAnchor.UpperCenter);
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(width, Mathf.Ceil(label.GetPreferredValues(text, width, 0f).y));
            return label;
        }

        private static float Place(RectTransform rt, float top)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            return top + rt.sizeDelta.y;
        }
    }
}
