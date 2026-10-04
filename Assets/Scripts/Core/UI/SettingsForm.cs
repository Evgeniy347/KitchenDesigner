using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsForm
    {
        private readonly List<Action> _readBack = new();
        private readonly Dictionary<string, TextMeshProUGUI> _labels = new();

        public void ReadBackFromSettings()
        {
            foreach (var readBack in _readBack) readBack();
        }

        internal void OnReadBack(Action readBack) => _readBack.Add(readBack);

        internal void Register(string key, TextMeshProUGUI label) => _labels[key] = label;

        public TextMeshProUGUI? RowLabel(string key) =>
            _labels.TryGetValue(key, out var label) ? label : null;

        public void SetFieldEnabled(TMP_InputField? field, string labelKey, bool enabled)
        {
            if (field == null) return;
            UIRowEnabled.SetControlEnabled(field, enabled);
            SetLabelEnabled(labelKey, enabled);
        }

        public void SetToggleEnabled(Toggle? toggle, string labelKey, bool enabled)
        {
            if (toggle == null) return;
            UIRowEnabled.SetControlEnabled(toggle, enabled);
            SetLabelEnabled(labelKey, enabled);
        }

        public void SetLabelEnabled(string labelKey, bool enabled)
        {
            if (_labels.TryGetValue(labelKey, out var label))
                UIRowEnabled.SetLabelEnabled(label, enabled);
        }
    }
}
