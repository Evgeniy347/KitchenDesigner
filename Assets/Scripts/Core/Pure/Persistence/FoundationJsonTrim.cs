namespace KitchenDesigner.Core
{
    public static class FoundationJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
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

            projectJson = RemoveFromElementsArray(projectJson);
            projectJson = RemoveFromNamedObject(projectJson, BasePlateKey);
            return projectJson;
        }

        private static string RemoveFromElementsArray(string projectJson)
        {
            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var elements = JsonText.MemberValue(projectJson, root, ElementsKey);
            if (!elements.Found || projectJson[elements.Start] != '[') return projectJson;

            return JsonText.RewriteArrayItems(projectJson, elements, RemoveFromElement);
        }

        private static string RemoveFromNamedObject(string projectJson, string key)
        {
            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var obj = JsonText.MemberValue(projectJson, root, key);
            if (!obj.Found || projectJson[obj.Start] != '{') return projectJson;

            return RemoveFromElement(projectJson, obj);
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
