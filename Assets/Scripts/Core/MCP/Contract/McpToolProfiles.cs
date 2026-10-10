using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpToolProfiles
    {
        public static bool Includes(McpToolDef tool, McpToolProfile profile) =>
            profile == McpToolProfile.Full || tool.Simple;

        public static List<McpToolDef> Select(IEnumerable<McpToolDef> tools, McpToolProfile profile)
        {
            var listed = new List<McpToolDef>();
            foreach (var tool in tools)
                if (Includes(tool, profile)) listed.Add(tool);
            return listed;
        }
    }
}
