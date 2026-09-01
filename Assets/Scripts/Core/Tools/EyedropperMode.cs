using System;

namespace KitchenDesigner.Core.Tools
{
    public static class EyedropperMode
    {
        public static bool Active { get; private set; }

        public static string? PickedMaterialId { get; private set; }

        public static event Action? Changed;

        public static void Toggle() => SetActive(!Active);

        public static void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;

            if (on)
            {
                Measure.MeasureMode.SetActive(false);
                DropSelectionSoItsResizeHandlesStopEatingClicks();
            }

            Changed?.Invoke();
        }

        private static void DropSelectionSoItsResizeHandlesStopEatingClicks()
        {
            if (SelectionManager.Instance != null) SelectionManager.Instance.DeselectAll();
        }

        public static void Pick(string? materialId)
        {
            if (PickedMaterialId == materialId) return;
            PickedMaterialId = materialId;
            Changed?.Invoke();
        }

        public static void Reset()
        {
            Active = false;
            PickedMaterialId = null;
        }
    }
}
