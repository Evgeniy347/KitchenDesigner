using System;

namespace KitchenDesigner.Core.MCP
{
    internal readonly struct RunStart
    {
        public const string LeftWord = "left";
        public const string RightWord = "right";

        public readonly bool FromRight;
        public readonly string? PartName;

        private RunStart(bool fromRight, string? partName)
        {
            FromRight = fromRight;
            PartName = partName;
        }

        public bool IsPart => PartName != null;

        public bool GrowsTowardMax => !FromRight;

        public static RunStart Parse(string? text)
        {
            var word = (text ?? string.Empty).Trim();
            if (word.Length == 0 || string.Equals(word, LeftWord, StringComparison.OrdinalIgnoreCase))
                return new RunStart(false, null);
            if (string.Equals(word, RightWord, StringComparison.OrdinalIgnoreCase))
                return new RunStart(true, null);
            return new RunStart(false, word);
        }
    }
}
