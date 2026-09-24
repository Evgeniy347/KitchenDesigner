namespace KitchenDesigner.Core
{
    public static class CreatedAtUtcJsonTrim
    {
        private const string Key = "createdAtUtc";
        private const string EmptyStringText = "\"\"";

        public static string RemoveWhenEmpty(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var value = JsonText.MemberValue(projectJson, root, Key);
            if (!value.Found) return projectJson;
            if (value.Text(projectJson) != EmptyStringText) return projectJson;

            return JsonText.RemoveMember(projectJson, root, Key);
        }
    }
}
