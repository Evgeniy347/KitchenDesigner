using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public sealed class DigestInput
    {
        public string Ref = McpReference.DefaultName;
        public string? Scope;
        public List<DigestGroup> Levels = new List<DigestGroup>();
        public List<DigestGroup> Rooms = new List<DigestGroup>();
        public List<DigestEntry> Entries = new List<DigestEntry>();
    }
}
