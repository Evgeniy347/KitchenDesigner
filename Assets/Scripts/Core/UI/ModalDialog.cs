using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ModalDialog
    {
        public const float ButtonsGap = UIStyle.Space5;

        private readonly RectTransform _root;
        private readonly RectTransform _panel;
        private readonly WindowFooter _buttons;
        private ModalDialogContent _content = new ModalDialogContent();

        private ModalDialog(RectTransform root, RectTransform panel, TextMeshProUGUI title,
            TextMeshProUGUI body, WindowFooter buttons,
            Button primary, Button secondary)
        {
            _root = root;
            _panel = panel;
            Title = title;
            Body = body;
            _buttons = buttons;
            PrimaryButton = primary;
            SecondaryButton = secondary;
        }

        private static readonly List<ModalDialog> Live = new();

        static ModalDialog() => ModalPresence.Probe = () => AnyVisible;

        public static bool AnyVisible
        {
            get
            {
                Live.RemoveAll(d => d._root == null);
                return Live.Exists(d => d.IsVisible);
            }
        }

        public RectTransform Root => _root;

        public RectTransform Panel => _panel;

        public TextMeshProUGUI Title { get; }

        public TextMeshProUGUI Body { get; }


        public Button PrimaryButton { get; }

        public Button SecondaryButton { get; }

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public static ModalDialog Build(Transform parent, string name)
        {
            var backdrop = UIFactory.CreatePanel(name + "Backdrop", parent, Vector2.zero, Vector2.one,
                UIStyle.ModalBackdrop);
            var root = backdrop.rectTransform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var panel = WindowSurface.Create(root, name + "Panel", new Vector2(UIStyle.DialogW, UIStyle.DialogW));
            float textW = UIStyle.DialogW - 2f * UIStyle.ModalPad;
            var title = TextRow(panel, name + "Title", UIStyle.FontWindowTitle, UIStyle.Text, textW);
            WindowTitle.Mark(title);
            var body = TextRow(panel, name + "Body", UIStyle.FontBody, UIStyle.Text, textW);

            var buttons = WindowFooter.Create(panel, name + "Buttons", UIStyle.ControlH, ruled: false);
            var dialog = (ModalDialog?)null;
            var secondary = buttons.AddSecondary(name + "Secondary", Loc.T("common.cancel"),
                () => dialog?.Cancel());
            var primary = buttons.AddPrimary(name + "Primary", "", () => dialog?.Confirm());
            dialog = new ModalDialog(root, panel, title, body, buttons, primary, secondary);
            Live.Add(dialog);

            root.gameObject.AddComponent<ModalDialogKeys>().Init(dialog);
            root.gameObject.SetActive(false);
            return dialog;
        }

        public void Show(ModalDialogContent content)
        {
            _content = content;
            Title.text = content.Title;
            Body.text = content.Body;
            PrimaryButton.gameObject.SetActive(HasPrimary);
            SetCaption(PrimaryButton, content.PrimaryCaption);
            SetCaption(SecondaryButton, content.SecondaryCaption ?? Loc.T("common.cancel"));
            ButtonRoles.Paint(PrimaryButton, content.Danger ? ButtonRole.Danger : ButtonRole.Primary);
            _buttons.Relayout();
            Layout();

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        private bool HasPrimary => !string.IsNullOrEmpty(_content.PrimaryCaption);

        public void Hide()
        {
            if (!_root.gameObject.activeSelf) return;
            _root.gameObject.SetActive(false);
        }

        public void Confirm()
        {
            if (!HasPrimary) return;
            var action = _content.OnPrimary;
            Hide();
            action?.Invoke();
        }

        public void Cancel()
        {
            var action = _content.OnSecondary;
            Hide();
            action?.Invoke();
        }

        public ModalDialogLayout Layout()
        {
            float width = Body.rectTransform.sizeDelta.x;
            var layout = ModalDialogLayout.For(Height(Title, width), Height(Body, width),
                UIStyle.ControlH, UIStyle.ModalPad, UIStyle.Space2, ButtonsGap);

            _panel.sizeDelta = new Vector2(UIStyle.DialogW, layout.Height);
            Place(Title, layout.TitleTop, Height(Title, width));
            Place(Body, layout.BodyTop, Height(Body, width));
            var buttons = _buttons.Root;
            buttons.anchoredPosition = new Vector2(0f, UIStyle.ModalPad);
            buttons.offsetMin = new Vector2(UIStyle.ModalPad - UIStyle.Space4, buttons.offsetMin.y);
            buttons.offsetMax = new Vector2(-(UIStyle.ModalPad - UIStyle.Space4), buttons.offsetMax.y);
            return layout;
        }

        private static float Height(TMP_Text text, float width) =>
            string.IsNullOrEmpty(text.text) ? 0f : Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y);

        private static void Place(TMP_Text text, float top, float height)
        {
            var rt = text.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(UIStyle.ModalPad, -top);
        }

        private static TextMeshProUGUI TextRow(RectTransform panel, string name, int fontSize, Color color,
            float width)
        {
            var label = UIFactory.CreateLabel(name, panel, "", fontSize, Vector2.zero, new Vector2(width, 0f),
                TextAnchor.UpperLeft);
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            return label;
        }

        private static void SetCaption(Button button, string caption)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.text = caption;
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(WindowFooter.WidthFor(label, caption), rt.sizeDelta.y);
        }
    }
}
