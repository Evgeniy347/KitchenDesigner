namespace KitchenDesigner.Core
{
    public static class ConvertStateHistoryJsonTrim
    {
        private const string UndoHistoryKey = "undoHistory";
        private const string RedoHistoryKey = "redoHistory";
        private const string ConvertStateKey = "convertState";
        private const string ChildrenKey = "children";

        public static string RemoveWhenNotNeeded(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            projectJson = RewriteHistoryArray(projectJson, UndoHistoryKey);
            projectJson = RewriteHistoryArray(projectJson, RedoHistoryKey);
            return projectJson;
        }

        private static string RewriteHistoryArray(string source, string key)
        {
            var root = JsonText.RootObject(source);
            if (!root.Found) return source;

            var history = JsonText.MemberValue(source, root, key);
            if (!history.Found || history.Start >= source.Length || source[history.Start] != '[')
                return source;

            return JsonText.RewriteArrayItems(source, history, RewriteOneCommandRecord);
        }

        private static string RewriteOneCommandRecord(string recordText, JsonSpan recordRoot)
        {
            var convertState = JsonText.MemberValue(recordText, recordRoot, ConvertStateKey);
            if (convertState.Found && convertState.Start < recordText.Length && recordText[convertState.Start] == '[')
                recordText = JsonText.RewriteArrayItems(recordText, convertState, ElementFamilyJsonTrim.RemoveAllFamilyFlags);

            recordRoot = JsonText.RootObject(recordText);
            var children = JsonText.MemberValue(recordText, recordRoot, ChildrenKey);
            if (children.Found && children.Start < recordText.Length && recordText[children.Start] == '[')
                recordText = JsonText.RewriteArrayItems(recordText, children, RewriteOneCommandRecord);

            return recordText;
        }
    }
}
