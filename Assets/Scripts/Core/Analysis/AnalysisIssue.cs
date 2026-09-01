namespace KitchenDesigner.Core.Analysis
{
    public enum IssueLevel
    {
        Error,
        Warning,
        Info,
    }

    public readonly struct AnalysisIssue
    {
        public readonly IssueLevel Level;
        public readonly string Code;
        public readonly string Detail;
        public readonly string Message;
        public readonly KitchenElement? Target;
        public readonly KitchenElement? Secondary;

        public AnalysisIssue(IssueLevel level, string code, string detail, string message,
            KitchenElement? target = null, KitchenElement? secondary = null)
        {
            Level = level;
            Code = code;
            Detail = detail;
            Message = message;
            Target = target;
            Secondary = secondary;
        }
    }
}
