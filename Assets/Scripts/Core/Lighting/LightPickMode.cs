using System;

namespace KitchenDesigner.Core.Lighting
{
    public static class LightPickMode
    {
        public static ILightSwitch? Source { get; private set; }

        public static bool Active => Source != null;

        public static event Action? Changed;

        public static bool IsPickingFor(ILightSwitch? source)
            => source != null && ReferenceEquals(Source, source);

        public static void Toggle(ILightSwitch? source)
            => SetSource(IsPickingFor(source) ? null : source);

        public static void SetSource(ILightSwitch? source)
        {
            if (ReferenceEquals(Source, source)) return;
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
