using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleRenderPlan(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsRenderPlan>() ?? new ParamsRenderPlan();
            if (p.px < PlanRender.MinPx || p.px > PlanRender.MaxPx)
                return McpResponse.Error(req.id, -32602,
                    $"px must be {PlanRender.MinPx}..{PlanRender.MaxPx} pixels, got {p.px}");
            if (!PlanViewWord.TryParse(p.view, out var view))
                return McpResponse.Error(req.id, -32602, $"view must be {PlanViewWord.Words}, got \"{p.view}\"");
            if (!McpSceneDigest.TryCollect(p.scope, McpReference.MinCorner, out var input, out var scopeError))
                return McpResponse.Error(req.id, -1, scopeError);
            return McpResponse.Result(req.id, PlanRender.Render(input, view, p.labels, p.px));
        }
    }
}
