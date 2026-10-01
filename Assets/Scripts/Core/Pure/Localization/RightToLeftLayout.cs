using System.Text;
using System.Text.RegularExpressions;

namespace KitchenDesigner.Core
{
    public static class RightToLeftLayout
    {
        private static readonly Regex RichTextTag = new Regex(
            "</?(?:b|i|u|s|color|size|pos|align|alpha|font|indent|line-height|line-indent|link|lowercase|uppercase"
            + "|smallcaps|margin|mark|mspace|nobr|noparse|page|space|sprite|style|sub|sup|voffset|width|cspace|rotate|br)"
            + "(?:=[^<>]*)?>|<#[0-9a-fA-F]{3,8}>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Prepare(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lines = text.Split('\n');
            var prepared = new StringBuilder(text.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) prepared.Append('\n');
                prepared.Append(PrepareLine(lines[i]));
            }
            return prepared.ToString();
        }

        private static string PrepareLine(string line)
        {
            if (line.Length == 0) return line;
            bool rightToLeftBase = BidiLine.FirstStrongIsRightToLeft(RichTextTag.Replace(line, "")) ?? true;
            var prepared = new StringBuilder(line.Length);
            int start = 0;
            foreach (Match tag in RichTextTag.Matches(line))
            {
                prepared.Append(PrepareSegment(line.Substring(start, tag.Index - start), rightToLeftBase));
                prepared.Append(tag.Value);
                start = tag.Index + tag.Length;
            }
            prepared.Append(PrepareSegment(line.Substring(start), rightToLeftBase));
            return prepared.ToString();
        }

        private static string PrepareSegment(string segment, bool rightToLeftBase) =>
            segment.Length == 0 ? segment : BidiLine.ForRightToLeftLayout(ArabicShaper.Shape(segment), rightToLeftBase);
    }
}
