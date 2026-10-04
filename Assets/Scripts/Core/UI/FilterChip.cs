using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class FilterChip
    {
        public const string RimNode = "Rim";
        public const string GlyphNode = "Glyph";

        private readonly string _caption;
        private readonly TMP_Text _label;
        private readonly TMP_Text? _glyph;
        private readonly Image _fill;
        private readonly Image _rim;
        private int _count;

        private FilterChip(Button button, string caption, TMP_Text label, TMP_Text? glyph, Image fill, Image rim)
        {
            Button = button;
            _caption = caption;
            _label = label;
            _glyph = glyph;
            _fill = fill;
            _rim = rim;
        }

        public Button Button { get; }

        public RectTransform Rect => (RectTransform)Button.transform;

        public bool Selected { get; private set; }

        public float Width => Rect.sizeDelta.x;

        public static FilterChip Create(Transform parent, string name, string caption, string? glyph,
            Color glyphColor, Action onClick)
        {
            var button = UIFactory.CreateButton(name, parent, caption, Vector2.zero,
                new Vector2(UIStyle.ChipH, UIStyle.ChipH), onClick);
            var fill = button.GetComponent<Image>();
            RoundedRectSprites.Apply(fill, RoundedRectSprites.Fill(UIStyle.ChipRadius));

            var rimRect = UIFactory.CreateRect(RimNode, button.transform);
            rimRect.anchorMin = Vector2.zero;
            rimRect.anchorMax = Vector2.one;
            rimRect.offsetMin = rimRect.offsetMax = Vector2.zero;
            var rim = rimRect.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(rim, RoundedRectSprites.Ring(UIStyle.ChipRadius, UIStyle.DividerPx));
            rim.raycastTarget = false;

            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = UIStyle.FontSmall;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Left;

            TMP_Text? glyphLabel = null;
            if (glyph != null)
            {
                glyphLabel = UIFactory.CreateLabel(GlyphNode, button.transform, glyph, UIStyle.FontSmall,
                    Vector2.zero, new Vector2(UIStyle.FontSmall, UIStyle.ChipH), TextAnchor.MiddleCenter);
                glyphLabel.color = glyphColor;
                glyphLabel.raycastTarget = false;
            }

            var chip = new FilterChip(button, caption, label, glyphLabel, fill, rim);
            chip.SetCount(0);
            chip.SetSelected(false);
            return chip;
        }

        public void SetCount(int count)
        {
            _count = count;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            _fill.color = selected ? UIStyle.AccentSubtle : UIStyle.Panel;
            _rim.color = selected ? UIStyle.Accent : UIStyle.Separator;
            Refresh();
        }

        private void Refresh()
        {
            string number = NumberFormat.Integer(_count);
            _label.text = Selected ? _caption + " <b>" + number + "</b>" : _caption + " " + number;
            _label.color = Selected ? UIStyle.Text : UIStyle.TextSecondary;

            float glyphW = _glyph != null ? _glyph.rectTransform.sizeDelta.x + UIStyle.Space1 : 0f;
            float textW = Mathf.Ceil(_label.GetPreferredValues(_label.text).x);
            float width = UIStyle.ChipPadX * 2f + glyphW + textW;
            Rect.sizeDelta = new Vector2(width, UIStyle.ChipH);

            bool rtl = LayoutDirection.IsRtl;
            var labelRt = _label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(UIStyle.ChipPadX + (rtl ? 0f : glyphW), 0f);
            labelRt.offsetMax = new Vector2(-(UIStyle.ChipPadX + (rtl ? glyphW : 0f)), 0f);
            _label.alignment = rtl ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;

            if (_glyph == null) return;
            var rt = _glyph.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(rtl ? 1f : 0f, 0.5f);
            rt.anchoredPosition = new Vector2(rtl ? -UIStyle.ChipPadX : UIStyle.ChipPadX, 0f);
        }
    }
}
