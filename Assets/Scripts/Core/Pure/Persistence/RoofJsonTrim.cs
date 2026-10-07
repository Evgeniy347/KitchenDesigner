namespace KitchenDesigner.Core
{
    public static class RoofJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string IsRoofKey = "isRoof";
        private const string FalseText = "false";

        private static readonly string[] DetailKeys =
        {
            "roofType", "roofRidgeAxis", "roofPitchDeg", "roofOverhangMm", "roofRafterStepMm",
        };

        public static string RemoveWhenNotRoof(string projectJson)
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
            if (!element.ValueIs(IsRoofKey, FalseText)) return;

            element.Remove(IsRoofKey);
            foreach (var key in DetailKeys)
                element.Remove(key);
        }
    }
}
