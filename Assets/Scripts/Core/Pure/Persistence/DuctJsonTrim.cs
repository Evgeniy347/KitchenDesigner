namespace KitchenDesigner.Core
{
    public static class DuctJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string IsDuctKey = "isDuct";
        private const string IsGrilleKey = "isGrille";
        private const string FalseText = "false";

        private static readonly string[] DuctDetailKeys =
        {
            "ductProfileKind", "ductDiameterMm", "ductWidthMm", "ductHeightMm",
            "ductAirflowM3PerHour",
        };

        private static readonly string[] GrilleDetailKeys =
        {
            "grilleAirflowM3PerHour",
        };

        public static string RemoveWhenNotDuct(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var elements = JsonText.MemberValue(projectJson, root, ElementsKey);
            if (!elements.Found || projectJson[elements.Start] != '[') return projectJson;

            return JsonText.RewriteArrayItems(projectJson, elements, RemoveFromElement);
        }

        private static string RemoveFromElement(string source, JsonSpan objectSpan)
        {
            source = RemoveFamilyUnlessTrue(source, objectSpan, IsDuctKey, DuctDetailKeys);
            source = RemoveFamilyUnlessTrue(source, objectSpan, IsGrilleKey, GrilleDetailKeys);
            return source;
        }

        private static string RemoveFamilyUnlessTrue(string source, JsonSpan objectSpan,
            string flagKey, string[] detailKeys)
        {
            var flag = JsonText.MemberValue(source, objectSpan, flagKey);
            if (flag.Found && flag.Text(source) != FalseText) return source;

            source = JsonText.RemoveMember(source, objectSpan, flagKey);
            foreach (var key in detailKeys)
                source = JsonText.RemoveMember(source, objectSpan, key);
            return source;
        }
    }
}
