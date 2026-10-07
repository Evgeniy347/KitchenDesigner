namespace KitchenDesigner.Core.MCP
{
    public static class McpFace
    {
        public const string Words = "left|right (X), bottom|top (Y), back|front (Z)";

        private static readonly string[] Names = { "left", "right", "bottom", "top", "back", "front" };

        private static readonly string[] AxisLetters = { "x", "y", "z" };

        public static bool TryParse(string? text, out int axis, out bool maxSide)
        {
            axis = 0;
            maxSide = false;
            var word = (text ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i] != word) continue;
                axis = i / 2;
                maxSide = i % 2 == 1;
                return true;
            }
            return false;
        }

        public static string NameOf(int axis, bool maxSide) => Names[axis * 2 + (maxSide ? 1 : 0)];

        public static string AxisLetter(int axis) => AxisLetters[axis];

        public static bool TryParseAxis(string? text, out int axis)
        {
            axis = 0;
            var word = (text ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < AxisLetters.Length; i++)
            {
                if (AxisLetters[i] != word) continue;
                axis = i;
                return true;
            }
            return false;
        }
    }
}
