namespace KitchenDesigner.Core
{
    public static class FoundationJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string IsFoundationKey = "isFoundation";
        private const string FalseText = "false";

        private static readonly string[] DetailKeys =
        {
            "foundationSoilKind", "foundationSandMm", "foundationGravelMm",
            "foundationCompacted", "foundationConcreteGrade", "foundationRebarDiameterMm",
            "foundationRebarStepMm", "foundationCoverMm",
        };

        public static string RemoveWhenNotFoundation(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var elements = JsonText.MemberValue(projectJson, root, ElementsKey);
            if (!elements.Found || projectJson[elements.Start] != '[') return projectJson;

            var items = JsonText.ArrayItems(projectJson, elements);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item.Start >= projectJson.Length || projectJson[item.Start] != '{') continue;
                projectJson = RemoveFromElement(projectJson, item);
            }
            return projectJson;
        }

        private static string RemoveFromElement(string source, JsonSpan objectSpan)
        {
            var isFoundation = JsonText.MemberValue(source, objectSpan, IsFoundationKey);
            if (!isFoundation.Found || isFoundation.Text(source) != FalseText) return source;

            source = JsonText.RemoveMember(source, objectSpan, IsFoundationKey);
            foreach (var key in DetailKeys)
                source = JsonText.RemoveMember(source, objectSpan, key);
            return source;
        }
    }
}
