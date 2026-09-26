namespace KitchenDesigner.Core
{
    public static class LevelsJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
        private const string LevelIdKey = "levelId";
        private const string LevelsKey = "levels";
        private const string EmptyStringText = "\"\"";
        private const string EmptyArrayText = "[]";

        public static string RemoveWhenEmpty(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            var result = RemoveEmptyLevelIdFromElements(projectJson);
            result = RemoveEmptyLevelIdFromNamedObject(result, BasePlateKey);
            result = RemoveEmptyLevelsArray(result);
            return result;
        }

        internal static string RemoveEmptyLevelIdFromElements(string source)
        {
            var root = JsonText.RootObject(source);
            if (!root.Found) return source;

            var elements = JsonText.MemberValue(source, root, ElementsKey);
            if (!elements.Found || source[elements.Start] != '[') return source;

            return JsonText.RewriteArrayItems(source, elements, RemoveEmptyLevelId);
        }

        private static string RemoveEmptyLevelIdFromNamedObject(string source, string key)
        {
            var root = JsonText.RootObject(source);
            if (!root.Found) return source;

            var obj = JsonText.MemberValue(source, root, key);
            if (!obj.Found || source[obj.Start] != '{') return source;

            return RemoveEmptyLevelId(source, obj);
        }

        internal static string RemoveEmptyLevelId(string source, JsonSpan objectSpan)
        {
            var levelId = JsonText.MemberValue(source, objectSpan, LevelIdKey);
            if (!levelId.Found) return source;
            if (levelId.Text(source) != EmptyStringText) return source;

            return JsonText.RemoveMember(source, objectSpan, LevelIdKey);
        }

        internal static string RemoveEmptyLevelsArray(string source)
        {
            var root = JsonText.RootObject(source);
            if (!root.Found) return source;

            var levels = JsonText.MemberValue(source, root, LevelsKey);
            if (!levels.Found) return source;
            if (levels.Text(source).Trim() != EmptyArrayText) return source;

            return JsonText.RemoveMember(source, root, LevelsKey);
        }
    }
}
