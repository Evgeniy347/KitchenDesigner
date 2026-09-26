namespace KitchenDesigner.Core
{
    internal static class ElementFamilyJsonTrim
    {
        internal static string RemoveAllFamilyFlags(string source, JsonSpan objectSpan)
        {
            source = FoundationJsonTrim.RemoveFromElement(source, objectSpan);
            source = FloorSlabJsonTrim.RemoveFromElement(source, objectSpan);
            source = FenceJsonTrim.RemoveFromElement(source, objectSpan);
            source = WallLayerJsonTrim.RemoveFromElement(source, objectSpan);
            source = RoofJsonTrim.RemoveFromElement(source, objectSpan);
            source = DuctJsonTrim.RemoveFromElement(source, objectSpan);
            source = LevelsJsonTrim.RemoveEmptyLevelId(source, objectSpan);
            return source;
        }
    }
}
