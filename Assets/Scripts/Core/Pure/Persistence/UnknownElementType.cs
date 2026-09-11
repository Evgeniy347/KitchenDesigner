namespace KitchenDesigner.Core
{
    public static class UnknownElementType
    {
        public const string PlainBoard = "board";

        public static bool IsUnknown(string? elementType, bool restoredAsPlainBoard) =>
            restoredAsPlainBoard
            && !string.IsNullOrEmpty(elementType)
            && !string.Equals(elementType, PlainBoard, System.StringComparison.Ordinal);
    }
}
