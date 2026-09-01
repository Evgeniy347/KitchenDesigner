using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class DropdownItemFit
    {
        public const float GlyphWidthFactor = 0.52f;

        public const float LineHeightFactor = 1.2f;

        public const float PadV = 6f;

        public const int MaxLines = 3;

        public const int WideGlyphReserve = 2;

        private const int SpaceChars = 1;

        public static int LinesFor(string? text, float textWidth, int fontSize,
            int maxLines = MaxLines)
        {
            if (string.IsNullOrEmpty(text) || textWidth <= 0f || fontSize <= 0) return 1;

            int charsPerLine = Mathf.Max(1,
                Mathf.FloorToInt(textWidth / (fontSize * GlyphWidthFactor)));
            int lines = 1;
            int used = 0;

            foreach (var word in text!.Split(' '))
            {
                int rest = word.Length;
                if (rest == 0) { used += SpaceChars; continue; }

                bool wholeWordMovesDown = used > 0 && used + SpaceChars + rest > charsPerLine;
                if (wholeWordMovesDown)
                {
                    lines++;
                    used = 0;
                }
                else if (used > 0)
                {
                    used += SpaceChars;
                }

                while (used + rest > charsPerLine)
                {
                    rest -= charsPerLine - used;
                    lines++;
                    used = 0;
                }
                used += rest;
            }

            return Mathf.Clamp(lines, 1, Mathf.Max(1, maxLines));
        }

        public static float WidthFor(IEnumerable<string>? options, int fontSize)
        {
            int longest = 0;
            if (options != null)
                foreach (var o in options)
                    if (!string.IsNullOrEmpty(o)) longest = Mathf.Max(longest, o!.Length);

            return longest > 0
                ? Mathf.Ceil((longest + WideGlyphReserve) * fontSize * GlyphWidthFactor)
                : 0f;
        }

        public static float HeightFor(IEnumerable<string>? options, float textWidth,
            int fontSize, int maxLines = MaxLines)
        {
            int lines = 1;
            if (options != null)
                foreach (var o in options)
                    lines = Mathf.Max(lines, LinesFor(o, textWidth, fontSize, maxLines));

            float h = lines * fontSize * LineHeightFactor + PadV;
            return Mathf.Max(UIStyle.DropdownItemMinH, Mathf.Ceil(h));
        }
    }
}
