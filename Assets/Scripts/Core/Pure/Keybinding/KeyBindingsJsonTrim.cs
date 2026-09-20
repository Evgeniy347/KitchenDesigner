using System.Collections.Generic;
using KitchenDesigner.Core;

namespace KitchenDesigner.Core.Keybinding
{
    public static class KeyBindingsJsonTrim
    {
        private const string SettingsKey = "settings";
        private const string KeyBindingsKey = "keyBindings";
        private const string EmptyArrayText = "[]";

        public static string RemoveWhenEmpty(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;

            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return projectJson;

            var settings = JsonText.MemberValue(projectJson, root, SettingsKey);
            if (!settings.Found || projectJson[settings.Start] != '{') return projectJson;

            var keyBindings = JsonText.MemberValue(projectJson, settings, KeyBindingsKey);
            if (!keyBindings.Found) return projectJson;
            if (keyBindings.Text(projectJson).Trim() != EmptyArrayText) return projectJson;

            return RemoveMember(projectJson, settings, KeyBindingsKey);
        }

        private static string RemoveMember(string source, JsonSpan objectSpan, string key)
        {
            var members = JsonText.Members(source, objectSpan);
            int index = members.FindIndex(m => m.Key == key);
            if (index < 0) return source;

            int prevBoundary = index == 0 ? objectSpan.Start + 1 : members[index - 1].Value.End;
            int keyStart = source.IndexOf('"', prevBoundary);
            if (keyStart < 0) return source;

            int valueEnd = members[index].Value.End;
            int afterValue = JsonText.SkipWhitespace(source, valueEnd);
            bool hasTrailingComma = afterValue < objectSpan.End && source[afterValue] == ',';
            int removalEnd = hasTrailingComma ? afterValue + 1 : valueEnd;

            if (!hasTrailingComma && index > 0)
            {
                int prevValueEnd = members[index - 1].Value.End;
                int commaPos = JsonText.SkipWhitespace(source, prevValueEnd);
                if (commaPos < objectSpan.End && source[commaPos] == ',')
                    return source.Substring(0, commaPos) + source.Substring(removalEnd);
            }

            return source.Substring(0, keyStart) + source.Substring(removalEnd);
        }
    }
}
