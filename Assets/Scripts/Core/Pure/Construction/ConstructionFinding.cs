namespace KitchenDesigner.Core.Construction
{
    public readonly struct ConstructionFinding
    {
        public readonly ConstructionFindingLevel Level;
        public readonly string Code;
        public readonly string ElementId;
        public readonly string Message;
        public readonly string? OtherElementId;

        public ConstructionFinding(ConstructionFindingLevel level, string code,
            string elementId, string message, string? otherElementId = null)
        {
            Level = level;
            Code = code;
            ElementId = elementId;
            Message = message;
            OtherElementId = otherElementId;
        }
    }
}
