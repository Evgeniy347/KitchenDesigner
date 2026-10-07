namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpDryRunText
    {
        public const string Param =
            "true = DRY-RUN: do everything, report the resulting placements and sceneViolationDelta, then revert "
            + "(nothing stays in the scene, nothing enters the undo history). Default false.";
    }
}
