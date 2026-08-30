namespace KitchenDesigner.Core
{
    public interface IFixedSizeElement
    {
        bool HasFixedSize { get; }
    }

    public static class FixedSize
    {
        public static bool IsFixed(object? element) =>
            element is IFixedSizeElement fixedSize && fixedSize.HasFixedSize;

        public static bool IsYawOnly(object? element) => element is IFixedSizeElement;
    }

    public static class ApplianceModels
    {
        public static readonly string[] All =
        {
            CooktopElement.MODEL_BOSCH_PUE611BB5E,
            OvenElement.MODEL,
            DishwasherElement.MODEL,
        };

        public static bool IsKnown(string? model)
        {
            if (string.IsNullOrEmpty(model)) return false;
            foreach (var known in All)
                if (known == model) return true;
            return false;
        }
    }
}
