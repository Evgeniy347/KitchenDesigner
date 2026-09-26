namespace KitchenDesigner.Core
{
    public static class BasePlateFamilyJsonTrim
    {
        private const string BasePlateKey = "basePlate";

        public static string RemoveWhenNotNeeded(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var basePlate = JsonText.MemberValue(projectJson, root, BasePlateKey);
            if (!basePlate.Found || projectJson[basePlate.Start] != '{') return projectJson;

            return JsonText.RewriteNamedObject(projectJson, basePlate, ElementFamilyJsonTrim.RemoveAllFamilyFlags);
        }
    }
}
