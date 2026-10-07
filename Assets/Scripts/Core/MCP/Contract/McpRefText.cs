namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpRefText
    {
        public const string Input =
            "Which point of the part anchor_x_mm/anchor_y_mm/anchor_z_mm (MM) name, and which point of each part "
            + "posMm reports in the response: read posMm, send the same numbers with the same ref, and the part "
            + "does not move. " + McpReference.Syntax;

        public const string Output =
            "Which point of each part posMm reports in the response (MM). " + McpReference.Syntax;
    }
}
