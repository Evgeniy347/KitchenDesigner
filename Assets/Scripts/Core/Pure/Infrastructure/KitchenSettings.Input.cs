using System.Collections.Generic;
using KitchenDesigner.Core.Keybinding;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public partial class KitchenSettings
    {
        [SerializeField] private KeyBindings _keyBindings = new KeyBindings();

        public KeyBindings KeyBindings => _keyBindings;

        internal void ResetKeyBindings() => _keyBindings.ClearOverrides();

        private void CaptureKeyBindings(KitchenSettingsData data)
        {
            var overrides = new List<KeyBindingOverrideData>();
            foreach (var action in InputActionCatalog.All)
            {
                if (_keyBindings.IsDefault(action)) continue;

                overrides.Add(new KeyBindingOverrideData
                {
                    action = action.ToString(),
                    primary = KeyChord.Format(_keyBindings.Primary(action)),
                    alt = KeyChord.Format(_keyBindings.Alt(action)),
                });
            }
            data.keyBindings = overrides.ToArray();
        }

        private void ApplyKeyBindings(KitchenSettingsData data)
        {
            _keyBindings.ClearOverrides();
            if (data.keyBindings == null) return;

            foreach (var entry in data.keyBindings)
            {
                if (!System.Enum.TryParse<InputAction>(entry.action, out var action)) continue;

                _keyBindings.SetPrimary(action, KeyChord.Parse(entry.primary));
                _keyBindings.SetAlt(action, KeyChord.Parse(entry.alt));
            }
        }
    }
}
