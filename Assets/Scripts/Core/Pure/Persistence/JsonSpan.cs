namespace KitchenDesigner.Core
{
    public readonly struct JsonSpan
    {
        public static readonly JsonSpan None = new JsonSpan(-1, -1);

        public readonly int Start;
        public readonly int End;

        public JsonSpan(int start, int end)
        {
            Start = start;
            End = end;
        }

        public bool Found => Start >= 0 && End >= Start;

        public int Length => End - Start;

        public string Text(string source) => Found ? source.Substring(Start, End - Start) : "";
    }
}
