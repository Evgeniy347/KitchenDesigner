using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class RunLayout
    {
        public readonly List<RunModuleSize> Modules = new List<RunModuleSize>();
        public readonly List<RunStep> Steps = new List<RunStep>();
        public readonly List<string> Problems = new List<string>();

        public bool Ok => Problems.Count == 0;
    }
}
