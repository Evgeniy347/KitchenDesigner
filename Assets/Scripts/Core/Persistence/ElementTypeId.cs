namespace KitchenDesigner.Core
{
    public static class ElementTypeId
    {
        public static string Of(KitchenElement element)
        {
            if (element == null) return "";
            var marker = UnknownTypeMarker.On(element);
            if (marker != null && !string.IsNullOrEmpty(marker.TypeId)) return marker.TypeId;
            return Bulk.ElementSelector.TypeOf(element);
        }
    }
}
