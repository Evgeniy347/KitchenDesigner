namespace KitchenDesigner.Core
{
    public static class FenceJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
        private const string IsFenceKey = "isFence";
        private const string FalseText = "false";

        private static readonly string[] DetailKeys =
        {
            "fencePostSectionMm", "fencePostStepMm", "fencePitDepthMm", "fenceSheetMark",
        };

        public static string RemoveWhenNotFence(string projectJson)
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
            if (!element.ValueIs(IsFenceKey, FalseText)) return;

            element.Remove(IsFenceKey);
            foreach (var key in DetailKeys)
                element.Remove(key);
        }
    }
}
