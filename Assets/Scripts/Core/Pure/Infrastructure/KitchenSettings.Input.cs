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
                    primary = InputBinding.Format(_keyBindings.PrimaryBinding(action)),
                    alt = InputBinding.Format(_keyBindings.AltBinding(action)),
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

                _keyBindings.SetPrimaryBinding(action, InputBinding.Parse(entry.primary));
                _keyBindings.SetAltBinding(action, InputBinding.Parse(entry.alt));
            }
        }
    }
}
