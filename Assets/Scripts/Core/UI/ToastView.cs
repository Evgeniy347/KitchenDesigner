using System;
using KitchenDesigner.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ToastView
    {
        public const string PanelName = "ToastPanel";
        public const string DismissName = "ToastDismiss";
        private const float Unwrapped = 100000f;
        public const float LevelStripeW = UIStyle.SelectionBarW;
        public const float BottomOffset = UIStyle.StatusBarH + UIStyle.Space5;

        public static readonly ToastMetrics Metrics = new ToastMetrics(
            minHeight: UIStyle.ControlH + 2f * UIStyle.Space2,
            padX: UIStyle.Space3,
            padY: UIStyle.Space3,
            gap: UIStyle.Space2,
            stripeWidth: LevelStripeW,
            iconSize: UIStyle.IconSizeSmall,
            dismissSize: UIStyle.ControlHCompact,
            maxWidth: UIStyle.DialogW * 1.5f);

        private ToastView(RectTransform panel, CanvasGroup group, Image stripe, Image icon, TMP_Text label,
            Button action, Button dismiss)
        {
            Panel = panel;
            Group = group;
            Stripe = stripe;
            Icon = icon;
            Label = label;
            ActionButton = action;
            DismissButton = dismiss;
        }

        public RectTransform Panel { get; }

        public CanvasGroup Group { get; }

        public Image Stripe { get; }

        public Image Icon { get; }

        public TMP_Text Label { get; }

        public Button ActionButton { get; }

        public Button DismissButton { get; }

        public ToastLayout Layout { get; private set; }

        public static ToastView Create(Transform canvas, Action onAction, Action onDismiss)
        {
            var panel = WindowSurface.Create(canvas, PanelName, new Vector2(Metrics.MaxWidth, Metrics.MinHeight));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, BottomOffset);
            panel.Find(WindowSurface.FillNode).GetComponent<Image>().color = UIStyle.NavBg;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var stripe = Block("ToastStripe", panel);
            var icon = Block("ToastIcon", panel);
            var label = UIFactory.CreateLabel("ToastLabel", panel, "", UIStyle.FontBody, Vector2.zero,
                Vector2.zero, TextAnchor.MiddleLeft);
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            var action = UIFactory.CreateButton("ToastAction", panel, "", Vector2.zero,
                new Vector2(0f, UIStyle.ControlHCompact), onAction);
            ButtonRoles.Paint(action, ButtonRole.Link);
            var dismiss = UIFactory.CreateButton(DismissName, panel, UIStyle.GlyphClose, Vector2.zero,
                new Vector2(Metrics.DismissSize, Metrics.DismissSize), onDismiss);
            QuietButton.Apply(dismiss);
            TooltipUI.Attach(dismiss.gameObject, Loc.T("common.close"));

            var view = new ToastView(panel, group, stripe, icon, label, action, dismiss);
            panel.gameObject.SetActive(false);
            return view;
        }

        public void Apply(string message, StatusLevel level, string? actionLabel, bool hasAction)
        {
            var color = ToastLevelStyle.ColorFor(level);
            Stripe.color = color;
            Icon.sprite = ToastLevelStyle.IconFor(level);
            Icon.color = color;
            Label.text = message;
            ActionButton.gameObject.SetActive(hasAction);
            float actionW = hasAction ? FitAction(actionLabel ?? "") : 0f;

            float wanted = Mathf.Ceil(Label.GetPreferredValues(message, Unwrapped, 0f).x);
            float textW = Mathf.Min(wanted, ToastLayout.TextWidthBudget(Metrics, actionW));
            float textH = Mathf.Ceil(Label.GetPreferredValues(message, textW, 0f).y);
            Layout = ToastLayout.For(Metrics, textW, textH, actionW);
            Place(textH);
        }

        private float FitAction(string caption)
        {
            var label = ActionButton.GetComponentInChildren<TMP_Text>(true);
            label.text = caption;
            float width = Mathf.Ceil(label.GetPreferredValues(caption).x) + 2f * UIStyle.Space2;
            var rt = (RectTransform)ActionButton.transform;
            rt.sizeDelta = new Vector2(width, UIStyle.ControlHCompact);
            return width;
        }

        private void Place(float textHeight)
        {
            var layout = Layout;
            Panel.sizeDelta = new Vector2(layout.Width, layout.Height);
            bool rtl = LayoutDirection.IsRtl;

            var stripe = Stripe.rectTransform;
            stripe.sizeDelta = new Vector2(Metrics.StripeWidth, layout.Height - 2f * UIStyle.Space2);
            Pin(stripe, Metrics.PadX, rtl);
            var icon = Icon.rectTransform;
            icon.sizeDelta = new Vector2(Metrics.IconSize, Metrics.IconSize);
            Pin(icon, layout.IconX, rtl);
            var label = Label.rectTransform;
            label.sizeDelta = new Vector2(layout.TextWidth, textHeight);
            Pin(label, layout.TextX, rtl);
            Label.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            if (ActionButton.gameObject.activeSelf) Pin((RectTransform)ActionButton.transform, layout.ActionX, rtl);
            Pin((RectTransform)DismissButton.transform, layout.DismissX, rtl);
        }

        private static void Pin(RectTransform rt, float fromLeading, bool rtl)
        {
            float edge = rtl ? 1f : 0f;
            rt.anchorMin = rt.anchorMax = new Vector2(edge, 0.5f);
            rt.pivot = new Vector2(edge, 0.5f);
            rt.anchoredPosition = new Vector2(rtl ? -fromLeading : fromLeading, 0f);
        }

        private static Image Block(string name, RectTransform panel)
        {
            var rt = UIFactory.CreateRect(name, panel);
            var image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
