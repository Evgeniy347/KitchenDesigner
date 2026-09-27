namespace KitchenDesigner.Core
{
    public static class DuctJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
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

            projectJson = RemoveFromElementsArray(projectJson);
            projectJson = RemoveFromNamedObject(projectJson, BasePlateKey);
            return projectJson;
        }

        internal static string RemoveFromElementsArray(string projectJson)
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

        internal static string RemoveFromElement(string source, JsonSpan objectSpan) =>
            JsonObjectEdit.Apply(source, objectSpan, MarkRemovals);

        internal static void MarkRemovals(JsonObjectEdit element)
        {
            MarkFamilyUnlessTrue(element, IsDuctKey, DuctDetailKeys);
            MarkFamilyUnlessTrue(element, IsGrilleKey, GrilleDetailKeys);
        }

        private static void MarkFamilyUnlessTrue(JsonObjectEdit element, string flagKey, string[] detailKeys)
        {
            if (element.Has(flagKey) && !element.ValueIs(flagKey, FalseText)) return;

            element.Remove(flagKey);
            foreach (var key in detailKeys)
                element.Remove(key);
        }
    }
}
