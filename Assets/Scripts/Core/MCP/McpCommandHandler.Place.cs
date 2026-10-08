using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandlePlace(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsPlace>();
            if (p == null || p.items == null || p.items.Length == 0)
                return McpResponse.Error(req.id, -32602, "items required (non-empty array)");
            if (!McpReplyShape.TryParse(p.@ref, p.verbosity, out var reference, out var full, out var shapeError))
                return McpResponse.Error(req.id, -32602, shapeError);

            var report = McpMutationReport.Begin();
            var placer = new McpPlacer(FindElementByName);
            for (int i = 0; i < p.items.Length; i++) placer.Place(i, p.items[i]);

            if (placer.Failed)
            {
                placer.RollBack();
                SettleSceneAfterMutation();
                return McpResponse.Error(req.id, -1, "place rejected, NOTHING was applied: "
                    + string.Join(" | ", placer.Errors) + ". " + McpNameHints.ResendAll);
            }

            if (p.dry_run)
            {
                SettleSceneAfterMutation();
                var dryReply = report.Finish(placer.Changed, reference, full);
                dryReply.dryRun = true;
                dryReply.applied = false;
                placer.RollBack();
                SettleSceneAfterMutation();
                return McpResponse.Result(req.id, dryReply);
            }

            var changed = new List<KitchenElement>(placer.Changed);
            CommandStack.Execute(new CompositeCommand($"MCP place x{placer.Commands.Count}", placer.Commands.ToList()));
            SettleSceneAfterMutation();
            var reply = report.Finish(changed, reference, full);
            reply.dryRun = false;
            reply.applied = true;
            Debug.Log($"[MCP] place: {changed.Count} parts");
            return McpResponse.Result(req.id, reply);
        }
    }
}
