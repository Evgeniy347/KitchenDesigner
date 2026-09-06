using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsRowFactory
    {
        public const float ContentW = 480f;
        public const float RowH = 32f;
        public const float RowStep = 38f;
        public const float LabelW = 300f;
        public const float ControlW = 150f;
        public const float IndentPx = 20f;
        public const float GapPx = 6f;

        private readonly List<Action> _readBackFromSettings = new();
        private readonly Dictionary<string, TextMeshProUGUI> _rowLabels = new();
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();

        public void ReadBackFromSettings()
        {
            foreach (var readBack in _readBackFromSettings) readBack();
        }

        public TextMeshProUGUI? RowLabel(string key) =>
            _rowLabels.TryGetValue(key, out var lbl) ? lbl : null;

        public Toggle AddToggle(Transform parent, ref float y, string label, bool value,
            Action<bool> onChanged, string? id = null, int indentLevel = 0, Func<bool>? read = null)
        {
            string key = id ?? label;
            var rowRect = CreateRow("RowTgl_" + key, parent, y);

            _rowLabels[key] = CreateRowLabel("Lbl_" + key, rowRect, label, indentLevel * IndentPx);

            var toggle = CreateRightToggle("Tgl_" + key, rowRect, value, onChanged);
            if (read != null)
                _readBackFromSettings.Add(() =>
                {
                    if (toggle != null) toggle.SetIsOnWithoutNotify(read());
                });

            y -= RowStep;
            return toggle;
        }

        public void AddHeader(Transform parent, ref float y, string label)
        {
            var rowRect = CreateRow("RowHdr_" + label, parent, y);
            CreateRowLabel("Lbl_" + label, rowRect, label, 0f);
            y -= RowStep;
        }

        public Slider AddSpeedSlider(Transform parent, ref float y, string label,
            float value, Action<float> onChanged, Func<float>? read = null)
        {
            var rowRect = CreateRow("RowSld_" + label, parent, y);
            CreateRowLabel("Lbl_" + label, rowRect, label, 0f);

            var valueLabel = UIFactory.CreateLabel("Val_" + label, rowRect, FormatMultiplier(value),
                UIStyle.FontBody, new Vector2(ContentW * 0.5f - 20, 0), new Vector2(40, RowH),
                TextAnchor.MiddleRight);

            var slider = UIFactory.CreateSlider("Sld_" + label, rowRect,
                KitchenSettings.MIN_INPUT_SPEED, KitchenSettings.MAX_INPUT_SPEED, value,
                new Vector2(ContentW * 0.5f - ControlW * 0.5f - 20, 0), new Vector2(110, RowH),
                v =>
                {
                    onChanged(v);
                    if (valueLabel != null) valueLabel.text = FormatMultiplier(v);
                });
            if (read != null)
                _readBackFromSettings.Add(() =>
                {
                    if (slider == null) return;
                    float v = read();
                    slider.SetValueWithoutNotify(v);
                    if (valueLabel != null) valueLabel.text = FormatMultiplier(v);
                });

            y -= RowStep;
            return slider;
        }

        public Slider AddIntSlider(Transform parent, ref float y, string label,
            int min, int max, int value, Func<int, string> format, Action<int> onChanged,
            Func<int>? read = null)
        {
            var rowRect = CreateRow("RowSld_" + label, parent, y);
            _rowLabels[label] = CreateRowLabel("Lbl_" + label, rowRect, label, 0f);

            var valueLabel = UIFactory.CreateLabel("Val_" + label, rowRect, format(value),
                UIStyle.FontBody, new Vector2(ContentW * 0.5f - 32, 0), new Vector2(64, RowH),
                TextAnchor.MiddleRight);

            var slider = UIFactory.CreateSlider("Sld_" + label, rowRect, min, max, value,
                new Vector2(ContentW * 0.5f - ControlW * 0.5f - ValueColumnW, 0), new Vector2(110, RowH),
                v =>
                {
                    int iv = Mathf.RoundToInt(v);
                    onChanged(iv);
                    if (valueLabel != null) valueLabel.text = format(iv);
                });
            slider.wholeNumbers = true;
            if (read != null)
                _readBackFromSettings.Add(() =>
                {
                    if (slider == null) return;
                    int v = read();
                    slider.SetValueWithoutNotify(v);
                    if (valueLabel != null) valueLabel.text = format(v);
                });

            y -= RowStep;
            return slider;
        }

        public TMP_InputField AddInput(Transform parent, ref float y, string label,
            string initial, TMP_InputField.ContentType contentType,
            Action<TMP_InputField> onEndEdit, string cleanValue,
            string? unit = null, bool indent = false, Func<string>? read = null)
        {
            var rowRect = CreateRow("RowFld_" + label, parent, y);
            _rowLabels[label] = CreateRowLabel("Lbl_" + label, rowRect, label, indent ? IndentPx : 0f);

            var field = unit != null
                ? UIFactory.CreateNumberField("Fld_" + label, rowRect, initial,
                    new Vector2(ContentW * 0.5f - ControlW * 0.5f, 0), new Vector2(ControlW, RowH), unit)
                : UIFactory.CreateInputField("Fld_" + label, rowRect, initial,
                    new Vector2(ContentW * 0.5f - ControlW * 0.5f, 0), new Vector2(ControlW, RowH));
            field.contentType = TMP_InputField.ContentType.Custom;
            bool isDecimal = contentType == TMP_InputField.ContentType.DecimalNumber;
            field.onValidateInput = DimensionFieldValidation.Char(allowDecimal: isDecimal);

            _cleanValues[field] = cleanValue;
            field.onValueChanged.AddListener(_ => UpdateFieldHighlight(field));
            field.onEndEdit.AddListener(_ =>
            {
                onEndEdit?.Invoke(field);
                UpdateFieldHighlight(field);
            });

            if (read != null)
                _readBackFromSettings.Add(() =>
                {
                    if (field == null) return;
                    string text = read();
                    field.SetTextWithoutNotify(text);
                    _cleanValues[field] = text;
                    UpdateFieldHighlight(field);
                });

            y -= RowStep;
            return field;
        }

        public void SetFieldEnabled(TMP_InputField? field, string labelKey, bool enabled)
        {
            if (field == null) return;
            field.interactable = enabled;
            if (field.textComponent != null)
                field.textComponent.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
            SetLabelEnabled(labelKey, enabled);
        }

        public void SetToggleEnabled(Toggle? toggle, string labelKey, bool enabled)
        {
            if (toggle == null) return;
            toggle.interactable = enabled;
            if (toggle.graphic != null)
                toggle.graphic.color = enabled ? UIStyle.Accent : UIStyle.TextDisabled;
            SetLabelEnabled(labelKey, enabled);
        }

        public void SetLabelEnabled(string labelKey, bool enabled)
        {
            if (_rowLabels.TryGetValue(labelKey, out var lbl) && lbl != null)
                lbl.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
        }

        public static RectTransform CreateRow(string name, Transform parent, float y)
        {
            var rowRect = UIFactory.CreateRect(name, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);
            return rowRect;
        }

        public static TextMeshProUGUI CreateRowLabel(string name, Transform parent, string text, float indentPx) =>
            UIFactory.CreateLabel(name, parent, text, UIStyle.FontBody,
                new Vector2(-(ContentW - LabelW) * 0.5f + indentPx, 0), new Vector2(LabelW, RowH),
                TextAnchor.MiddleLeft);

        private const float ValueColumnW = 46f;

        private static Toggle CreateRightToggle(string name, Transform parent, bool value, Action<bool> onChanged)
        {
            var rect = UIFactory.CreateRect(name, parent);
            rect.sizeDelta = new Vector2(UIStyle.HitTarget, RowH);
            rect.anchoredPosition = new Vector2(ContentW * 0.5f - UIStyle.HitTarget * 0.5f, 0);

            var toggle = rect.gameObject.AddComponent<Toggle>();

            var box = UIFactory.CreatePanel(name + "_Box", rect,
                Vector2.zero, new Vector2(22, 22), UIFactory.FieldColor);
            toggle.graphic = UIFactory.CreateCheckmark(name + "_Check", box.transform);
            toggle.targetGraphic = box;

            toggle.isOn = value;
            if (onChanged != null)
                toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        private void UpdateFieldHighlight(TMP_InputField field)
        {
            if (field == null) return;
            var clean = _cleanValues.TryGetValue(field, out var v) ? v : field.text;
            UIFactory.SetHighlight(field, field.text != clean);
        }

        private static string FormatMultiplier(float v) =>
            v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "×";
    }
}
