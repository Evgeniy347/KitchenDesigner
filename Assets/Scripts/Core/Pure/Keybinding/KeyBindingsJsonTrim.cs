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

            return JsonText.RemoveMember(projectJson, settings, KeyBindingsKey);
        }
    }
}
