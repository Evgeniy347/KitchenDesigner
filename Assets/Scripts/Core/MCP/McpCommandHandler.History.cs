using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleUndo(McpRequest req) => StepThroughHistory(req, undo: true);

        private McpResponse HandleRedo(McpRequest req) => StepThroughHistory(req, undo: false);

        private McpResponse StepThroughHistory(McpRequest req, bool undo)
        {
            var p = req.Params?.ToObjectStrict<ParamsUndoSteps>() ?? new ParamsUndoSteps();
            int wanted = Mathf.Clamp(p.steps, 1, McpHistorySteps.Max);
            var report = McpMutationReport.Begin();
            var described = new List<string>();
            int done = 0;
            while (done < wanted && (undo ? CommandStack.CanUndo : CommandStack.CanRedo))
            {
                if (undo)
                {
                    described.Add(CommandStack.PeekUndoDescription());
                    CommandStack.Undo();
                }
                else CommandStack.Redo();
                done++;
            }
            SettleSceneAfterMutation();

            var reply = report.Finish();
            reply.steps = undo ? described : null;
            reply.doneCount = done;
            reply.undoAvailableCount = CommandStack.UndoCount;
            reply.redoAvailableCount = CommandStack.RedoCount;
            Debug.Log($"[MCP] {(undo ? "Undo" : "Redo")} x{done}");
            return McpResponse.Result(req.id, reply);
        }
    }
}
