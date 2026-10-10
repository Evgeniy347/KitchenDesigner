namespace KitchenDesigner.Core.MCP
{
    internal sealed class RunStep
    {
        public readonly RunModuleSize Size;
        public readonly PlaceSpec Spec;
        public readonly float RotYDeg;
        public readonly bool IsNew;

        public RunStep(RunModuleSize size, PlaceSpec spec, float rotYDeg, bool isNew)
        {
            Size = size;
            Spec = spec;
            RotYDeg = rotYDeg;
            IsNew = isNew;
        }
    }
}
