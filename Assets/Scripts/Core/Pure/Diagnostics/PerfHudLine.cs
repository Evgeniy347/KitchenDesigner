namespace KitchenDesigner.Core
{
    public enum PerfHudLineKind
    {
        Normal,
        Secondary,
        Warning,
    }

    public readonly struct PerfHudLine
    {
        public PerfHudLine(string text, PerfHudLineKind kind)
        {
            Text = text;
            Kind = kind;
        }

        public string Text { get; }

        public PerfHudLineKind Kind { get; }
    }
}
