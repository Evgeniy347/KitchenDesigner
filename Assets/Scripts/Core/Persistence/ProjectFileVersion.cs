using System.IO;

namespace KitchenDesigner.Core
{
    public static class ProjectFileVersion
    {
        public const string Key = "appVersion";

        public static string Of(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
            try
            {
                return In(File.ReadAllText(path));
            }
            catch (IOException)
            {
                return "";
            }
        }

        public static string In(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var value = JsonText.MemberValue(json, JsonText.RootObject(json), Key);
            if (!value.Found) return "";
            string text = value.Text(json);
            return text.Length >= 2 && text[0] == '"'
                ? text.Substring(1, text.Length - 2)
                : "";
        }
    }
}
