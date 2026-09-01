using System;

namespace KitchenDesigner.Core.Measure
{
    public static class MeasureMode
    {
        public static bool Active { get; private set; }

        public static event Action? Changed;

        public static void Toggle() => SetActive(!Active);

        public static void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;

            if (on)
            {
                Tools.EyedropperMode.SetActive(false);
                DropSelectionSoItsResizeHandlesStopEatingClicks();
            }
            else
            {
                MeasureStore.Clear();
            }

            Changed?.Invoke();
        }

        private static void DropSelectionSoItsResizeHandlesStopEatingClicks()
        {
            if (SelectionManager.Instance != null) SelectionManager.Instance.DeselectAll();
        }

        public static void Reset()
        {
            Active = false;
            MeasureStore.Clear();
        }
    }
}
