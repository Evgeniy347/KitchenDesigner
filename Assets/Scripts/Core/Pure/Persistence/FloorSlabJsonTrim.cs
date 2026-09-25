namespace KitchenDesigner.Core
{
    public static class FloorSlabJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
        private const string IsFloorSlabKey = "isFloorSlab";
        private const string FalseText = "false";

        private static readonly string[] DetailKeys =
        {
            "slabTechnology", "slabConcreteGrade", "slabRebarDiameterMm", "slabRebarStepMm",
        };

        public static string RemoveWhenNotFloorSlab(string projectJson)
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

            var items = JsonText.ArrayItems(projectJson, elements);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item.Start >= projectJson.Length || projectJson[item.Start] != '{') continue;
                projectJson = RemoveFromElement(projectJson, item);
            }
            return projectJson;
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
            var isFloorSlab = JsonText.MemberValue(source, objectSpan, IsFloorSlabKey);
            if (!isFloorSlab.Found || isFloorSlab.Text(source) != FalseText) return source;

            source = JsonText.RemoveMember(source, objectSpan, IsFloorSlabKey);
            foreach (var key in DetailKeys)
                source = JsonText.RemoveMember(source, objectSpan, key);
            return source;
        }
    }
}
