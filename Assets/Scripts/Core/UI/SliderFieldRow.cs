using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SliderFieldRow
    {
        private readonly Func<float, string> _format;
        private readonly Func<string, (bool ok, float value)> _parse;
        private readonly Action<float> _onChanged;
        private readonly float _min;
        private readonly float _max;

        private SliderFieldRow(Slider slider, TMP_InputField field, Func<float, string> format,
            Func<string, (bool ok, float value)> parse, Action<float> onChanged, float min, float max)
        {
            Slider = slider;
            Field = field;
            _format = format;
            _parse = parse;
            _onChanged = onChanged;
            _min = min;
            _max = max;
        }

        public Slider Slider { get; }

        public TMP_InputField Field { get; }

        public static SliderFieldRow Add(FormRows rows, RectTransform host, string node, string label, float min, float max,
            float value, string unit, Func<float, string> format, Func<string, (bool ok, float value)> parse,
            Action<float> onChanged)
        {
            var m = rows.Metrics;
            var root = UIFactory.CreateRect("Row_" + node, host);
            root.sizeDelta = new Vector2(m.Width, m.ControlH);

            var caption = UIFactory.CreateLabel("L_" + node, root, label, m.LabelFont, Vector2.zero,
                new Vector2(m.LabelW, m.ControlH), TextAnchor.MiddleLeft);
            caption.enableWordWrapping = false;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            Cell(root, caption.rectTransform, m.Width, 0f, m.LabelW);

            float trackW = m.ValueW - m.NumberW - UIStyle.Space2;
            var slider = SliderControl.Create("Sld_" + node, root, min, max, value, trackW, m.ControlH, null);
            Cell(root, (RectTransform)slider.transform, m.Width, m.ValueX, trackW);

            var field = UIFactory.CreateNumberField("F_" + node, root, format(value), Vector2.zero,
                new Vector2(m.NumberW, m.ControlH), unit);
            field.textComponent!.alignment = TextAlignmentOptions.Right;
            Cell(root, (RectTransform)field.transform, m.Width, m.ValueX + m.ValueW - m.NumberW, m.NumberW);

            var row = new SliderFieldRow(slider, field, format, parse, onChanged, min, max);
            slider.onValueChanged.AddListener(row.OnSlider);
            field.onEndEdit.AddListener(row.OnField);
            rows.Custom(root, m.ControlH);
            return row;
        }

        public void SetValue(float value)
        {
            Slider.SetValueWithoutNotify(value);
            Field.SetTextWithoutNotify(_format(value));
            UIFactory.SetHighlight(Field, false);
        }

        private void OnSlider(float value)
        {
            _onChanged(value);
            Field.SetTextWithoutNotify(_format(value));
            UIFactory.SetHighlight(Field, false);
        }

        private void OnField(string text)
        {
            var (ok, value) = _parse(text);
            if (!ok)
            {
                UIFactory.SetErrorHighlight(Field);
                Field.SetTextWithoutNotify(_format(Slider.value));
                return;
            }

            value = Mathf.Clamp(value, _min, _max);
            Slider.SetValueWithoutNotify(value);
            _onChanged(value);
            SetValue(value);
        }

        private static void Cell(RectTransform row, RectTransform rt, float rowWidth, float x, float width)
        {
            float left = LayoutDirection.IsRtl ? rowWidth - x - width : x;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left, 0f);
        }
    }
}
