namespace KitchenDesigner.Core
{
    internal static class ElementFamilyJsonTrim
    {
        internal static string RemoveAllFamilyFlags(string source, JsonSpan objectSpan) =>
            JsonObjectEdit.Apply(source, objectSpan, MarkRemovals);

        internal static void MarkRemovals(JsonObjectEdit element)
        {
            FoundationJsonTrim.MarkRemovals(element);
            FloorSlabJsonTrim.MarkRemovals(element);
            FenceJsonTrim.MarkRemovals(element);
            WallLayerJsonTrim.MarkRemovals(element);
            RoofJsonTrim.MarkRemovals(element);
            DuctJsonTrim.MarkRemovals(element);
            LevelsJsonTrim.MarkEmptyLevelId(element);
        }
    }
}
