using System;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SettingsDefaultsButton : MonoBehaviour
    {
        private Button? _button;
        private Func<bool>? _atDefaults;

        public static SettingsDefaultsButton Attach(Button button, Func<bool> atDefaults)
        {
            var sync = button.gameObject.AddComponent<SettingsDefaultsButton>();
            sync._button = button;
            sync._atDefaults = atDefaults;
            sync.Refresh();
            return sync;
        }

        public void Refresh()
        {
            if (_button == null || _atDefaults == null) return;
            _button.interactable = !_atDefaults();
        }

        private void OnEnable() => Refresh();

        private void Update() => Refresh();
    }
}
