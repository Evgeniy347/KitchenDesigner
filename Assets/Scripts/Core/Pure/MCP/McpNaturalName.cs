using System;

namespace KitchenDesigner.Core.MCP
{
    public static class McpNaturalName
    {
        public static int Compare(string? a, string? b)
        {
            a ??= string.Empty;
            b ??= string.Empty;
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int byNumber = CompareDigitRuns(a, ref i, b, ref j);
                    if (byNumber != 0) return byNumber;
                    continue;
                }
                int byChar = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (byChar != 0) return byChar;
                i++;
                j++;
            }
            int byLength = (a.Length - i).CompareTo(b.Length - j);
            return byLength != 0 ? byLength : string.CompareOrdinal(a, b);
        }

        private static int CompareDigitRuns(string a, ref int i, string b, ref int j)
        {
            int startA = i, startB = j;
            while (i < a.Length && char.IsDigit(a[i])) i++;
            while (j < b.Length && char.IsDigit(b[j])) j++;
            var runA = a.Substring(startA, i - startA).TrimStart('0');
            var runB = b.Substring(startB, j - startB).TrimStart('0');
            int byWidth = runA.Length.CompareTo(runB.Length);
            return byWidth != 0 ? byWidth : string.CompareOrdinal(runA, runB);
        }
    }
}
