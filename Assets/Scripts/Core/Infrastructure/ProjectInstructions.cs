namespace KitchenDesigner.Core
{
    public static class ProjectInstructions
    {
        public static string Text { get; set; } = "";

        public static bool TryGetPositiveMm(string key, out int value)
        {
            value = 0;
            if (TryGetValue(key, out var raw) && int.TryParse(raw,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0)
                return true;
            value = 0;
            return false;
        }

        public static bool TryGetValue(string key, out string value)
        {
            value = "";
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(Text)) return false;
            foreach (var raw in Text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0 || !string.Equals(line.Substring(0, colon).Trim(), key,
                        System.StringComparison.OrdinalIgnoreCase)) continue;
                value = line.Substring(colon + 1).Trim();
                return value.Length > 0;
            }
            return false;
        }

        public static void Reset() => Text = "";
    }
}
