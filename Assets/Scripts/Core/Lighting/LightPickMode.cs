using System;

namespace KitchenDesigner.Core.Lighting
{
    public static class LightPickMode
    {
        public static LightSwitchElement? Source { get; private set; }

        public static bool Active => Source != null;

        public static event Action? Changed;

        public static bool IsPickingFor(LightSwitchElement? source)
            => source != null && Source == source;

        public static void Toggle(LightSwitchElement? source)
            => SetSource(IsPickingFor(source) ? null : source);

        public static void SetSource(LightSwitchElement? source)
        {
            if (Source == source) return;
            Source = source;

            if (source != null)
            {
                Measure.MeasureMode.SetActive(false);
                Tools.EyedropperMode.SetActive(false);
            }

            Changed?.Invoke();
        }

        public static void Reset() => Source = null;
    }
}
