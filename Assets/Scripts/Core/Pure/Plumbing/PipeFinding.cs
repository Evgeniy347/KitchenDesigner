namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeFinding
    {
        public readonly PipeFindingLevel Level;
        public readonly string Code;
        public readonly string ElementId;
        public readonly string? OtherElementId;
        public readonly string Message;

        public PipeFinding(PipeFindingLevel level, string code, string elementId,
            string? otherElementId, string message)
        {
            Level = level;
            Code = code;
            ElementId = elementId;
            OtherElementId = otherElementId;
            Message = message;
        }
    }
}
