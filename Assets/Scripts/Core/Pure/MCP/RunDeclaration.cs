using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class RunDeclaration
    {
        public string Id = string.Empty;
        public string? From;
        public float StartMm;
        public float GapMm;
        public readonly List<RunModuleDecl?> Modules = new List<RunModuleDecl?>();
    }
}
