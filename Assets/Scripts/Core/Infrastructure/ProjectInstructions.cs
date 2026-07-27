namespace KitchenDesigner.Core
{
    /// <summary>
    /// Свободный текст «инструкции проекта» — единый источник соглашений проекта
    /// (толщины несущих/перегородок, толщина ЛДСП, зазоры фасадов, правила посадки
    /// и т.п.). Хранится в файле проекта (ProjectData.projectInstructions), читается
    /// и пишется через MCP (get/set_project_instructions), выдаётся в get_status.
    /// Рантайм-холдер: единственная актуальная копия текста в памяти приложения.
    /// </summary>
    public static class ProjectInstructions
    {
        public static string Text { get; set; } = "";

        /// <summary>Reads a deterministic numeric convention from a line such as
        /// <c>bearing_wall_thickness_mm: 200</c>. Free prose remains allowed;
        /// geometry only consumes explicitly named key/value lines.</summary>
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
