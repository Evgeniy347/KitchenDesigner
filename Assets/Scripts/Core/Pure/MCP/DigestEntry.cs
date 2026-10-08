namespace KitchenDesigner.Core.MCP
{
    public sealed class DigestEntry
    {
        public string Name = string.Empty;
        public string Kind = string.Empty;
        public int PartsCount;
        public PlacementInfo Placement = new PlacementInfo();

        public bool IsModule => PartsCount > 0;

        public bool HasIssues => Placement.issues.Count > 0;
    }
}
