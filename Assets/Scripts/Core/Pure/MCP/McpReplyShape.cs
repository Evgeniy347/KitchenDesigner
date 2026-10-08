namespace KitchenDesigner.Core.MCP
{
    public static class McpReplyShape
    {
        public static bool TryParse(string? refText, string? verbosityText,
            out McpReference reference, out bool full, out string error)
        {
            full = false;
            if (!McpReference.TryParse(refText, out reference, out error)) return false;
            return McpVerbosity.TryParse(verbosityText, out full, out error);
        }
    }
}
