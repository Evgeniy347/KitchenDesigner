using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SegmentedControl : MonoBehaviour
    {
        private readonly List<Button> _segments = new();
        private Action<int>? _onChanged;

        public int Value { get; private set; }

        public IReadOnlyList<Button> Segments => _segments;

        public static SegmentedControl Create(string name, Transform parent, IReadOnlyList<string> options,
            int value, float width, float height, Action<int>? onChanged)
        {
            float pad = UIStyle.SegmentPad;
            float segW = (width - pad * 2f - pad * (options.Count - 1)) / Mathf.Max(1, options.Count);
            var widths = new float[options.Count];
            for (int i = 0; i < widths.Length; i++) widths[i] = segW;
            return Create(name, parent, options, value, widths, height, onChanged);
        }

        public static float WidthFor(IReadOnlyList<float> segmentWidths)
        {
            float pad = UIStyle.SegmentPad;
            float total = pad * 2f + pad * Mathf.Max(0, segmentWidths.Count - 1);
            foreach (var w in segmentWidths) total += w;
            return total;
        }

        public static SegmentedControl Create(string name, Transform parent, IReadOnlyList<string> options,
            int value, IReadOnlyList<float> segmentWidths, float height, Action<int>? onChanged)
        {
            float width = WidthFor(segmentWidths);
            var rect = UIFactory.CreateRect(name, parent);
            rect.sizeDelta = new Vector2(width, height);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = UIStyle.Field;
            RoundedRectSprites.Apply(bg, RoundedRectSprites.ControlFill);
            var rim = UIFactory.AddFieldStroke(rect);
            rim.color = UIStyle.Separator;

            var self = rect.gameObject.AddComponent<SegmentedControl>();
            self._onChanged = onChanged;
            float pad = UIStyle.SegmentPad;
            float segX = pad;
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                float segW = segmentWidths[i];
                var button = UIFactory.CreateButton(name + "_" + i, rect, options[i], Vector2.zero,
                    new Vector2(segW, height - pad * 2f), () => self.Choose(index));
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(segX, 0f);
                segX += segW + pad;
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = height <= UIStyle.ControlHCompact ? UIStyle.FontSmall : UIStyle.FontBody;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                self._segments.Add(button);
            }
            self.SetValueWithoutNotify(value);
            return self;
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= _segments.Count || index == Value) return;
            SetValueWithoutNotify(index);
            _onChanged?.Invoke(index);
        }

        public void SetValueWithoutNotify(int index)
        {
            Value = Mathf.Clamp(index, 0, Mathf.Max(0, _segments.Count - 1));
            for (int i = 0; i < _segments.Count; i++)
            {
                bool selected = i == Value;
                var image = (Image)_segments[i].targetGraphic;
                image.color = selected ? UIStyle.SurfaceActive : UIStyle.Transparent;
                _segments[i].GetComponentInChildren<TMP_Text>().color =
                    selected ? UIStyle.Text : UIStyle.TextSecondary;
            }
        }

        public void SetInteractable(bool enabled)
        {
            foreach (var segment in _segments) UIRowEnabled.SetControlEnabled(segment, enabled);
        }
    }
}
