using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class FormRows
    {
        public const float CaptionH = 20f;

        private readonly RectTransform _host;
        private readonly List<FormRow> _rows = new();
        private readonly UIRowRegistry _registered = new();
        private CollapsibleSection? _section;

        public FormRows(RectTransform host, RowDensity density)
        {
            _host = host;
            Density = density;
            Metrics = RowMetrics.For(density);
        }

        public RowDensity Density { get; }

        public RowMetrics Metrics { get; }

        public float Height { get; private set; }

        public IReadOnlyList<FormRow> Rows => _rows;

        public IEnumerable<(TMP_Text? label, Selectable? control)> LabelledRows => _registered.Rows;

        public event Action? Relayouted;

        public void SyncEnabledState() => _registered.Sync();

        public (TextMeshProUGUI label, TMP_InputField field) Number(string node, string label, string unit,
            int indent = 0, string? hint = null, bool allowDecimal = false)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, indent);
            var field = UIFactory.CreateNumberField("F_" + node, row.Root, "", Vector2.zero,
                new Vector2(Metrics.NumberW, Metrics.ControlH), unit);
            field.textComponent!.alignment = TextAlignmentOptions.Right;
            field.contentType = TMP_InputField.ContentType.Custom;
            field.onValidateInput = DimensionFieldValidation.Char(allowDecimal);
            Value(row, (RectTransform)field.transform);
            return (Finish(row, lbl, field, hint), field);
        }

        public (TextMeshProUGUI label, TMP_InputField field) Text(string node, string label, string? hint = null)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, 0);
            var field = UIFactory.CreateInputField("F_" + node, row.Root, "", Vector2.zero,
                new Vector2(Metrics.ValueW, Metrics.ControlH));
            Value(row, (RectTransform)field.transform);
            return (Finish(row, lbl, field, hint), field);
        }

        public (TextMeshProUGUI label, TMP_Dropdown dropdown) Dropdown(string node, string label,
            List<string> options, int value, Action<int> onChanged, int indent = 0, string? hint = null)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, indent);
            var dropdown = UIFactory.CreateDropdown("Dd_" + node, row.Root, options, Vector2.zero,
                new Vector2(Metrics.ValueW, Metrics.ControlH), onChanged);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1)));
            dropdown.RefreshShownValue();
            Value(row, (RectTransform)dropdown.transform);
            UIFactory.FitDropdownItems(dropdown);
            return (Finish(row, lbl, dropdown, hint), dropdown);
        }

        public (TextMeshProUGUI label, Toggle toggle) Switch(string node, string label, bool value,
            Action<bool> onChanged, int indent = 0, string? hint = null)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, indent);
            var toggle = SwitchControl.Create("Sw_" + node, row.Root, value, onChanged);
            Value(row, (RectTransform)toggle.transform);
            return (Finish(row, lbl, toggle, hint), toggle);
        }

        public (TextMeshProUGUI label, SegmentedControl segmented) Segmented(string node, string label,
            IReadOnlyList<string> options, int value, Action<int> onChanged, string? hint = null)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, 0);
            var segmented = SegmentedControl.Create("Seg_" + node, row.Root, options, value, Metrics.ValueW,
                Metrics.ControlH, onChanged);
            Value(row, (RectTransform)segmented.transform);
            foreach (var segment in segmented.Segments) _registered.Add(lbl, segment);
            row.Label = lbl;
            row.Control = segmented.Segments.Count > 0 ? segmented.Segments[0] : null;
            if (hint != null) HintBadge.AttachAfterLabel(lbl, hint);
            return (lbl, segmented);
        }

        public (TextMeshProUGUI label, Slider slider, TextMeshProUGUI value) Slider(string node, string label,
            float min, float max, float value, Func<float, string> format, Action<float> onChanged,
            bool wholeNumbers = false, string? hint = null)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, 0);
            float trackW = Metrics.ValueW - UIStyle.SliderValueW - UIStyle.Space2;
            TextMeshProUGUI? readout = null;
            var slider = SliderControl.Create("Sld_" + node, row.Root, min, max, value, trackW, Metrics.ControlH,
                v =>
                {
                    onChanged(v);
                    if (readout != null) readout.text = format(v);
                });
            slider.wholeNumbers = wholeNumbers;
            Value(row, (RectTransform)slider.transform);

            readout = UIFactory.CreateLabel("Val_" + node, row.Root, format(value), UIStyle.FontSmall, Vector2.zero,
                new Vector2(UIStyle.SliderValueW, Metrics.ControlH), TextAnchor.MiddleRight);
            readout.color = UIStyle.TextSecondary;
            readout.enableWordWrapping = false;
            readout.raycastTarget = false;
            Cell(row, readout.rectTransform, Metrics.ValueX + Metrics.ValueW - UIStyle.SliderValueW, UIStyle.SliderValueW);
            return (Finish(row, lbl, slider, hint), slider, readout);
        }

        public (TextMeshProUGUI label, TextMeshProUGUI value) ReadOnly(string node, string label, string value,
            int indent = 0, string? hint = null)
        {
            var row = NewRow(node, Metrics.ControlH);
            var lbl = RowLabel(row, node, label, indent);
            UIRowEnabled.SetLabelEnabled(lbl, false);
            var text = UIFactory.CreateLabel("Val_" + node, row.Root, value, Metrics.LabelFont, Vector2.zero,
                new Vector2(Metrics.ValueW, Metrics.ControlH), TextAnchor.MiddleLeft);
            text.color = UIStyle.TextSecondary;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Value(row, text.rectTransform);
            row.Label = lbl;
            if (hint != null) HintBadge.AttachAfterLabel(lbl, hint);
            return (lbl, text);
        }

        public VectorField Vector(string node, string caption, IReadOnlyList<string>? rotateTooltips = null,
            Action<int>? onRotate90 = null)
        {
            var captionRow = NewRow(node + "_Caption", CaptionH);
            var lbl = UIFactory.CreateLabel("L_" + node, captionRow.Root, caption, UIStyle.FontSmall, Vector2.zero,
                new Vector2(Metrics.Width, CaptionH), TextAnchor.MiddleLeft);
            lbl.color = UIStyle.TextSecondary;
            Cell(captionRow, lbl.rectTransform, 0f, Metrics.Width);
            captionRow.Label = lbl;
            captionRow.GapAfter = UIStyle.Space1;

            var fieldRow = NewRow(node, Metrics.ControlH);
            var vector = VectorField.Create("Vec_" + node, fieldRow.Root, Metrics.Width, Metrics.ControlH,
                rotateTooltips, onRotate90);
            Cell(fieldRow, vector.Root, 0f, Metrics.Width);
            foreach (var field in vector.Fields) _registered.Add(lbl, field);
            return vector;
        }

        public CollapsibleSection Section(string node, string title, string? memoryKey = null,
            string? actionCaption = null, Action? onAction = null)
        {
            _section = null;
            var row = NewRow(node, UIStyle.SectionHeaderH);
            row.GapAfter = UIStyle.SectionGapAfter;
            row.IsSectionHeader = true;
            var section = CollapsibleSection.Create("Sec_" + node, row.Root, title, Metrics.Width, memoryKey,
                actionCaption, onAction);
            Cell(row, (RectTransform)section.transform, 0f, Metrics.Width);
            section.Toggled += _ => Relayout();
            _section = section;
            return section;
        }

        public void EndSection() => _section = null;

        public TextMeshProUGUI Note(string node, string text)
        {
            var label = UIFactory.CreateLabel(node, _host, text, UIStyle.FontSmall, Vector2.zero,
                new Vector2(Metrics.Width, 0f), TextAnchor.UpperLeft);
            label.color = UIStyle.TextSecondary;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            float h = Mathf.Ceil(label.GetPreferredValues(text, Metrics.Width, 0f).y);
            var row = Adopt(label.rectTransform, h);
            row.Label = label;
            return label;
        }

        public FormRow Custom(RectTransform content, float height) => Adopt(content, height);

        public void Gap(float px)
        {
            if (_rows.Count > 0) _rows[_rows.Count - 1].GapAfter += px;
        }

        public float Relayout()
        {
            bool rtl = LayoutDirection.IsRtl;
            float y = 0f;
            float pendingGap = 0f;
            bool first = true;
            foreach (var row in _rows)
            {
                bool shown = row.IsShown;
                row.Root.gameObject.SetActive(shown);
                if (!shown) continue;

                if (!first) y += row.IsSectionHeader ? Mathf.Max(pendingGap, UIStyle.SectionGapBefore) : pendingGap;
                row.Root.anchorMin = row.Root.anchorMax = row.Root.pivot = new Vector2(rtl ? 1f : 0f, 1f);
                row.Root.anchoredPosition = new Vector2(0f, -y);
                y += row.Height;
                pendingGap = row.GapAfter;
                first = false;
            }
            Height = y;
            Relayouted?.Invoke();
            return y;
        }

        private FormRow NewRow(string node, float height)
        {
            var root = UIFactory.CreateRect("Row_" + node, _host);
            root.sizeDelta = new Vector2(Metrics.Width, height);
            return Adopt(root, height);
        }

        private FormRow Adopt(RectTransform root, float height)
        {
            root.SetParent(_host, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(Metrics.Width, height);
            var row = new FormRow(root, height, _section) { GapAfter = Metrics.RowGap };
            _rows.Add(row);
            return row;
        }

        private TextMeshProUGUI RowLabel(FormRow row, string node, string text, int indent)
        {
            float inset = indent * UIStyle.SubRowIndent;
            var label = UIFactory.CreateLabel("L_" + node, row.Root, text, Metrics.LabelFont, Vector2.zero,
                new Vector2(Metrics.LabelW - inset, Metrics.ControlH), TextAnchor.MiddleLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Cell(row, label.rectTransform, inset, Metrics.LabelW - inset);
            return label;
        }

        private TextMeshProUGUI Finish(FormRow row, TextMeshProUGUI label, Selectable control, string? hint)
        {
            row.Label = label;
            row.Control = control;
            _registered.Add(label, control);
            if (hint != null) HintBadge.AttachAfterLabel(label, hint);
            return label;
        }

        private void Value(FormRow row, RectTransform control) =>
            Cell(row, control, Metrics.ValueX, control.sizeDelta.x);

        private void Cell(FormRow row, RectTransform rt, float x, float width)
        {
            bool rtl = LayoutDirection.IsRtl;
            float left = rtl ? Metrics.Width - x - width : x;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left, 0f);
        }
    }
}
