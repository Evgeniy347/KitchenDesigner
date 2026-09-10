using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleSaveProject(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSaveProject>();
            if (p == null || string.IsNullOrEmpty(p.path))
                return McpResponse.Error(req.id, -32602, "path required");

            var directoryCheck = McpSaveDirectoryGuard.Evaluate(McpSaveDirectoryStatus.AllowedDirectory, p.path);
            if (!directoryCheck.Allowed)
                return McpResponse.Error(req.id, -1, directoryCheck.Reason ?? "Refused");

            if (DemoMode.Current.IsDemoFile(p.path))
                return McpResponse.Error(req.id, -1,
                    $"Refused: '{p.path}' is the demo project file. Save to a different path.");

            bool ok = SaveLoadManager.SaveToPath(p.path);
            if (!ok)
                return McpResponse.Error(req.id, -1,
                    $"save_project failed for '{p.path}': {LastSaveLoadLogReason()}");

            Debug.Log($"[MCP] Saved project to {p.path}");
            var (elementCount, violationCount) = SceneCounts();
            return McpResponse.Result(req.id, new
            {
                ok = true,
                path = p.path,
                elementCount,
                sceneViolationCount = violationCount
            });
        }

        private McpResponse HandleLoadProject(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsLoadProject>();
            if (p == null || string.IsNullOrEmpty(p.path))
                return McpResponse.Error(req.id, -32602, "path required");

            bool ok = SaveLoadManager.LoadFromPath(p.path);
            if (!ok)
                return McpResponse.Error(req.id, -1,
                    $"load_project failed for '{p.path}': {LastSaveLoadLogReason()}");

            SettleSceneAfterMutation();

            Debug.Log($"[MCP] Loaded project from {p.path}");
            var (elementCount, violationCount) = SceneCounts();
            return McpResponse.Result(req.id, new
            {
                ok = true,
                path = p.path,
                elementCount,
                sceneViolationCount = violationCount
            });
        }

        private static (int elementCount, int violationCount) SceneCounts()
        {
            var all = PartRegistry.GetAll();
            var vr = McpValidationCache.Get(all);
            return (all != null ? all.Count : 0, vr != null ? vr.violations.Count : 0);
        }

        private static string LastSaveLoadLogReason()
        {
            var recent = ConsoleLogCapture.GetRecent(20);
            for (int i = recent.Count - 1; i >= 0; i--)
                if (recent[i].message.StartsWith("[SaveLoad]"))
                    return recent[i].message;
            return "see Unity console (get_console_logs) for the reason";
        }
    }
}
