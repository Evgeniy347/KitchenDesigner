using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public enum WindowKind
    {
        Dialog,
        Tool,
    }

    public sealed class WindowChromeOptions
    {
        public WindowKind Kind { get; set; } = WindowKind.Dialog;
        public Action? OnClose { get; set; }
        public bool HasFooter { get; set; }
        public bool RuledHeader { get; set; }
        public bool Draggable { get; set; } = true;
    }

    public sealed class WindowChrome
    {
        public const string CloseButtonName = "CloseBtn";
        public const int MaxHeaderActions = 2;

        private int _headerActions;

        private WindowChrome(RectTransform panel, TextMeshProUGUI title, Button? close,
            WindowFooter? footer, WindowKind kind)
        {
            Panel = panel;
            Title = title;
            CloseButton = close;
            Footer = footer;
            Kind = kind;
        }

        public RectTransform Panel { get; }

        public TextMeshProUGUI Title { get; }

        public Button? CloseButton { get; }

        public WindowFooter? Footer { get; }

        public WindowKind Kind { get; }

        public float BodyPad => PadFor(Kind);

        public float BodyTop => UIStyle.TitleBarH + BodyPad;

        public float BodyBottom => Footer != null ? UIStyle.FooterH + BodyPad : UIStyle.Space5;

        public float BodyWidth => Panel.sizeDelta.x - 2f * BodyPad;

        public static float PadFor(WindowKind kind) =>
            kind == WindowKind.Tool ? UIStyle.ToolPanelPad : UIStyle.DialogPad;

        public static WindowChrome Create(Transform parent, string name, string title, Vector2 size,
            WindowChromeOptions options)
        {
            var panel = WindowSurface.Create(parent, name, size);

            if (options.Draggable) WindowDrag.Attach(panel, UIStyle.TitleBarH);
            if (options.RuledHeader) WindowSurface.AddRule(panel, name + "_HeaderRule", -UIStyle.TitleBarH);

            Button? close = options.OnClose != null ? CreateQuietClose(panel, options.OnClose) : null;
            float reserved = close != null ? UIStyle.CloseBtnInset + UIStyle.CloseBtnSize : 0f;
            var label = WindowTitle.Create(panel, name + "Title", title, UIStyle.FontWindowTitle,
                size.x - UIStyle.WindowTitleInset - reserved - UIStyle.Space2, UIStyle.ControlH,
                TextAnchor.MiddleLeft, UIStyle.WindowTitleInset);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;

            var footer = options.HasFooter ? WindowFooter.Create(panel, name + "Footer") : null;
            close?.transform.SetAsLastSibling();
            return new WindowChrome(panel, label, close, footer, options.Kind);
        }

        public void SetTitle(string text) => Title.text = text;

        public Button AddHeaderAction(string name, Sprite icon, string tooltip, Action onClick)
        {
            if (_headerActions >= MaxHeaderActions)
                throw new InvalidOperationException("D5: не больше двух иконных действий в шапке окна");
            _headerActions++;

            float right = (CloseButton != null ? UIStyle.CloseBtnInset + UIStyle.CloseBtnSize : 0f)
                + UIStyle.CloseBtnInset + (_headerActions - 1) * (UIStyle.CloseBtnSize + UIStyle.Space1);
            var button = UIFactory.CreateIconButton(name, Panel, icon, Vector2.zero,
                new Vector2(UIStyle.CloseBtnSize, UIStyle.CloseBtnSize), onClick,
                UIStyle.CloseBtnSize - UIStyle.IconSizeSmall);
            QuietButton.Apply(button);
            var rt = (RectTransform)button.transform;
            UIFactory.AnchorTopRight(rt);
            rt.anchoredPosition = new Vector2(-right, -UIStyle.CloseBtnInset);
            TooltipUI.Attach(button.gameObject, tooltip);

            var title = Title.rectTransform;
            title.sizeDelta = new Vector2(title.sizeDelta.x - UIStyle.CloseBtnSize - UIStyle.Space1,
                title.sizeDelta.y);
            CloseButton?.transform.SetAsLastSibling();
            return button;
        }

        public WindowBody CreateBody() =>
            WindowBody.Create(Panel, BodyTop, BodyBottom, BodyPad);

        private static Button CreateQuietClose(RectTransform panel, Action onClose)
        {
            var close = UIFactory.CreateCloseButton(panel, onClose);
            close.name = CloseButtonName;
            return close;
        }
    }
}
