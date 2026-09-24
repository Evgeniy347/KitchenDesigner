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

        private static string RemoveEmptyLevelIdFromElements(string source)
        {
            var root = JsonText.RootObject(source);
            if (!root.Found) return source;

            var elements = JsonText.MemberValue(source, root, ElementsKey);
            if (!elements.Found || source[elements.Start] != '[') return source;

            var items = JsonText.ArrayItems(source, elements);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item.Start >= source.Length || source[item.Start] != '{') continue;
                source = RemoveEmptyLevelId(source, item);
            }
            return source;
        }

        private static string RemoveEmptyLevelIdFromNamedObject(string source, string key)
        {
            var root = JsonText.RootObject(source);
            if (!root.Found) return source;

            var obj = JsonText.MemberValue(source, root, key);
            if (!obj.Found || source[obj.Start] != '{') return source;

            return RemoveEmptyLevelId(source, obj);
        }

        private static string RemoveEmptyLevelId(string source, JsonSpan objectSpan)
        {
            var levelId = JsonText.MemberValue(source, objectSpan, LevelIdKey);
            if (!levelId.Found) return source;
            if (levelId.Text(source) != EmptyStringText) return source;

            return JsonText.RemoveMember(source, objectSpan, LevelIdKey);
        }

        private static string RemoveEmptyLevelsArray(string source)
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
