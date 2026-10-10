using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleApplyRun(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsApplyRun>();
            if (p == null || p.modules == null || p.modules.Length == 0)
                return McpResponse.Error(req.id, -32602, "modules required (non-empty array)");
            if (!McpReplyShape.TryParse(p.@ref, p.verbosity, out var reference, out var full, out var shapeError))
                return McpResponse.Error(req.id, -32602, shapeError);
            return new McpRunApplier(req.id, p, reference, full, FindElementByName, SettleSceneAfterMutation).Apply();
        }
    }
}
