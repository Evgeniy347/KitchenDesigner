namespace KitchenDesigner.Core.MCP
{
    public static class McpVerbosity
    {
        public const string Terse = "terse";

        public const string Full = "full";

        public const string Words = Terse + " | " + Full;

        public const string Syntax =
            "How much the reply says about every changed part. terse (default) = the placement (pos, footprint, on, touches, "
            + "gaps, issues) + sceneViolationDelta, about 100 bytes per part. full = the same PLUS the complete ElementInfo of "
            + "each changed part under elements (about 1 KB per part, the legacy shape): ask for it only when you need a field "
            + "the placement does not carry";

        public static bool TryParse(string? text, out bool full, out string error)
        {
            full = false;
            error = string.Empty;
            var word = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (word.Length == 0 || word == Terse) return true;
            if (word == Full)
            {
                full = true;
                return true;
            }
            error = $"Unknown verbosity '{text}'. Valid: {Words}";
            return false;
        }
    }
}
