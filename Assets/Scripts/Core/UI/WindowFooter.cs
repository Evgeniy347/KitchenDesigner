using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class WindowFooter
    {
        public const float ButtonPadX = UIStyle.Space4;
        public const float MinButtonW = 80f;

        private readonly List<RectTransform> _right = new();
        private readonly List<RectTransform> _left = new();
        private readonly string _name;

        private WindowFooter(RectTransform root, string name)
        {
            Root = root;
            _name = name;
        }

        public RectTransform Root { get; }

        public IReadOnlyList<RectTransform> RightGroup => _right;

        public IReadOnlyList<RectTransform> LeftGroup => _left;

        public static WindowFooter Create(RectTransform panel, string name) =>
            Create(panel, name, UIStyle.FooterH, ruled: true);

        public static WindowFooter Create(RectTransform panel, string name, float height, bool ruled)
        {
            var root = UIFactory.CreateRect(name, panel);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, height);
            root.anchoredPosition = Vector2.zero;
            if (ruled) WindowSurface.AddRule(root, name + "_Rule", 0f);
            return new WindowFooter(root, name);
        }

        public Button AddPrimary(string node, string caption, Action onClick) =>
            AddRight(node, caption, onClick, ButtonRole.Primary);

        public Button AddDanger(string node, string caption, Action onClick) =>
            AddRight(node, caption, onClick, ButtonRole.Danger);

        public Button AddSecondary(string node, string caption, Action onClick) =>
            AddRight(node, caption, onClick, ButtonRole.Secondary);

        public Button AddLeft(string node, string caption, Action onClick,
            ButtonRole role = ButtonRole.Secondary)
        {
            var button = CreateButton(node, caption, onClick, role);
            _left.Add((RectTransform)button.transform);
            Relayout();
            return button;
        }

        public TMP_Text AddLeftText(string node, string text)
        {
            var label = UIFactory.CreateLabel(node, Root, text, UIStyle.FontSmall, Vector2.zero,
                new Vector2(0f, UIStyle.ControlH), TextAnchor.MiddleLeft);
            label.color = UIStyle.TextSecondary;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.sizeDelta = new Vector2(label.GetPreferredValues(text).x + 1f, UIStyle.ControlH);
            _left.Add(rt);
            Relayout();
            return label;
        }

        public void Relayout()
        {
            bool rtl = LayoutDirection.IsRtl;
            float x = UIStyle.Space4;
            foreach (var rt in _left)
            {
                Place(rt, x, fromRight: rtl);
                x += rt.sizeDelta.x + UIStyle.Space2;
            }

            x = UIStyle.Space4;
            for (int i = _right.Count - 1; i >= 0; i--)
            {
                Place(_right[i], x, fromRight: !rtl);
                x += _right[i].sizeDelta.x + UIStyle.Space2;
            }
        }

        public static float WidthFor(TMP_Text label, string caption) =>
            Mathf.Max(MinButtonW, Mathf.Ceil(label.GetPreferredValues(caption).x) + 2f * ButtonPadX);

        private Button AddRight(string node, string caption, Action onClick, ButtonRole role)
        {
            var button = CreateButton(node, caption, onClick, role);
            var rt = (RectTransform)button.transform;
            if (role == ButtonRole.Primary || role == ButtonRole.Danger) _right.Add(rt);
            else _right.Insert(PrimaryIndex(), rt);
            Relayout();
            return button;
        }

        private int PrimaryIndex()
        {
            for (int i = 0; i < _right.Count; i++)
                if (_right[i].GetComponent<FooterPrimaryMark>() != null) return i;
            return _right.Count;
        }

        private Button CreateButton(string node, string caption, Action onClick, ButtonRole role)
        {
            var button = UIFactory.CreateButton(node, Root, caption, Vector2.zero,
                new Vector2(MinButtonW, UIStyle.FooterButtonH), onClick);
            var label = button.GetComponentInChildren<TMP_Text>();
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(WidthFor(label, caption), UIStyle.FooterButtonH);
            ButtonRoles.Paint(button, role);
            if (role == ButtonRole.Primary || role == ButtonRole.Danger)
                button.gameObject.AddComponent<FooterPrimaryMark>();
            return button;
        }

        private static void Place(RectTransform rt, float inset, bool fromRight)
        {
            float x = fromRight ? 1f : 0f;
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0.5f);
            rt.pivot = new Vector2(x, 0.5f);
            rt.anchoredPosition = new Vector2(fromRight ? -inset : inset, 0f);
        }
    }
}
