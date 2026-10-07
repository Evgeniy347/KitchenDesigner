namespace KitchenDesigner.Core
{
    public static class WallLayerJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string IsInsulationKey = "isInsulation";
        private const string IsVentGapKey = "isVentGap";
        private const string IsCladdingKey = "isCladding";
        private const string HostWallNameKey = "wallLayerHostWallName";
        private const string VentGapBattenStepKey = "ventGapBattenStepMm";
        private const string TrueText = "true";

        private static readonly string[] FamilyKeys =
        {
            IsInsulationKey, IsVentGapKey, IsCladdingKey, HostWallNameKey, VentGapBattenStepKey,
        };

        public static string RemoveWhenNotWallLayer(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            return RemoveFromElementsArray(projectJson);
        }

        internal static string RemoveFromElementsArray(string projectJson)
        {
            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var elements = JsonText.MemberValue(projectJson, root, ElementsKey);
            if (!elements.Found || projectJson[elements.Start] != '[') return projectJson;

            return JsonText.RewriteArrayItems(projectJson, elements, RemoveFromElement);
        }

        internal static string RemoveFromElement(string source, JsonSpan objectSpan) =>
            JsonObjectEdit.Apply(source, objectSpan, MarkRemovals);

        internal static void MarkRemovals(JsonObjectEdit element)
        {
            if (element.ValueIs(IsInsulationKey, TrueText)
                || element.ValueIs(IsVentGapKey, TrueText)
                || element.ValueIs(IsCladdingKey, TrueText))
            {
                if (!element.ValueIs(IsVentGapKey, TrueText)) element.Remove(VentGapBattenStepKey);
                return;
            }

            foreach (var key in FamilyKeys)
                element.Remove(key);
        }
    }
}
