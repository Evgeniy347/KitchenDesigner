namespace KitchenDesigner.Core
{
    public static class WallLayerJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
        private const string IsInsulationKey = "isInsulation";
        private const string IsVentGapKey = "isVentGap";
        private const string IsCladdingKey = "isCladding";
        private const string HostWallNameKey = "wallLayerHostWallName";
        private const string VentGapBattenStepKey = "ventGapBattenStepMm";
        private const string TrueText = "true";

        private static readonly string[] FamilyKeys =
        {
            IsInsulationKey, IsVentGapKey, IsCladdingKey, HostWallNameKey, VentGapBattenStepKey,
        };

        public static string RemoveWhenNotWallLayer(string projectJson)
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
            if (IsTrue(source, objectSpan, IsInsulationKey)
                || IsTrue(source, objectSpan, IsVentGapKey)
                || IsTrue(source, objectSpan, IsCladdingKey))
                return RemoveBattenStepUnlessVentGap(source, objectSpan);

            foreach (var key in FamilyKeys)
                source = JsonText.RemoveMember(source, objectSpan, key);
            return source;
        }

        private static string RemoveBattenStepUnlessVentGap(string source, JsonSpan objectSpan)
        {
            if (IsTrue(source, objectSpan, IsVentGapKey)) return source;
            return JsonText.RemoveMember(source, objectSpan, VentGapBattenStepKey);
        }

        private static bool IsTrue(string source, JsonSpan objectSpan, string key)
        {
            var value = JsonText.MemberValue(source, objectSpan, key);
            return value.Found && value.Text(source) == TrueText;
        }
    }
}
