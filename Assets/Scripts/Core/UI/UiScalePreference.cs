using System;

namespace KitchenDesigner.Core.UI
{
    internal static class UiScalePreference
    {
        internal const string Key = "KitchenUiScalePercent";

        private static int? _percent;

        public static event Action? Changed;

        internal static int Percent
        {
            get
            {
                _percent ??= UiScale.ClampPercent(PreferenceStore.Current.GetInt(Key, UiScale.AutoPercent));
                return _percent.Value;
            }
        }

        internal static void Choose(int percent)
        {
            int clamped = UiScale.ClampPercent(percent);
            if (_percent == clamped) return;
            _percent = clamped;
            PreferenceStore.Current.SetInt(Key, clamped);
            Changed?.Invoke();
        }
    }
}
