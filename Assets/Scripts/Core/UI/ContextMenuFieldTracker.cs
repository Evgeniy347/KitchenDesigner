using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuFieldTracker
    {
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();
        private readonly List<TMP_InputField> _rejectedFields = new();
        private readonly Action _apply;
        private int _lastApplyFrame = -1;

        public ContextMenuFieldTracker(Action apply) => _apply = apply;

        public IReadOnlyList<TMP_InputField> RejectedFields => _rejectedFields;

        public void ForgetRejections() => _rejectedFields.Clear();

        public void Reject(TMP_InputField field)
        {
            if (!_rejectedFields.Contains(field)) _rejectedFields.Add(field);
        }

        public void ShowRejections()
        {
            foreach (var f in _rejectedFields) UIFactory.SetErrorHighlight(f);
        }

        public int ParseInt(TMP_InputField? field, int fallback)
        {
            if (field == null) return fallback;
            var expr = field.text;
            if (HasArithmetic(expr))
            {
                var result = ExpressionParser.EvaluateInt(expr);
                if (result.HasValue)
                {
                    field.text = result.Value.ToString();
                    return result.Value;
                }
            }
            if (int.TryParse(expr, out int v)) return v;
            Reject(field);
            return fallback;
        }

        public float ParseAngle(TMP_InputField? field, float fallback)
        {
            if (field == null) return fallback;
            var expr = field.text;
            if (HasArithmetic(expr))
            {
                var result = ExpressionParser.EvaluateFloat(expr);
                if (result.HasValue)
                {
                    field.text = result.Value.ToString("F1");
                    return result.Value;
                }
            }
            if (float.TryParse(expr, out float v)) return v;
            Reject(field);
            return fallback;
        }

        public float ParseMillimetresAsMetres(TMP_InputField? field, float fallbackMetres)
        {
            if (field == null) return fallbackMetres;
            var expr = field.text;
            if (HasArithmetic(expr))
            {
                var result = ExpressionParser.EvaluateInt(expr);
                if (result.HasValue)
                {
                    field.text = result.Value.ToString();
                    return result.Value * AppConstants.MM_TO_UNITS;
                }
            }
            if (int.TryParse(expr, out int mm)) return mm * AppConstants.MM_TO_UNITS;
            Reject(field);
            return fallbackMetres;
        }

        public float ParseDecimalInRange(TMP_InputField? field, float fallback, float min, float max)
        {
            if (field == null) return fallback;
            var dotted = field.text.Replace(',', '.');
            if (float.TryParse(dotted, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                && v >= min && v <= max)
                return v;

            Reject(field);
            return fallback;
        }

        public void Track(TMP_InputField? field, string cleanValue)
        {
            if (field == null) return;
            _cleanValues[field] = cleanValue;
            field.onValueChanged.RemoveAllListeners();
            field.onValueChanged.AddListener(_ => UpdateHighlight(field));
            field.onEndEdit.RemoveAllListeners();
            field.onEndEdit.AddListener(_ => ApplyOncePerFrame());
        }

        public void RefreshUnfocused(TMP_InputField? field, string newValue)
        {
            if (field == null || field.isFocused) return;
            field.SetTextWithoutNotify(newValue);
            _cleanValues[field] = newValue;
        }

        public void ApplyOncePerFrame()
        {
            if (Time.frameCount == _lastApplyFrame) return;
            _lastApplyFrame = Time.frameCount;
            _apply();
        }

        public void UpdateHighlight(TMP_InputField field)
        {
            if (field == null) return;
            var clean = _cleanValues.TryGetValue(field, out var v) ? v : field.text;
            UIFactory.SetHighlight(field, field.text != clean);
        }

        public void ClearHighlights()
        {
            foreach (var kv in _cleanValues)
            {
                UIFactory.SetHighlight(kv.Key, false);
                kv.Key.onValueChanged.RemoveAllListeners();
            }
            _cleanValues.Clear();
        }

        private static bool HasArithmetic(string expr) => expr.Contains('+') || expr.Contains('-');
    }
}
