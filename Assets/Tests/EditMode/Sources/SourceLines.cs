using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Разбор исходника для сторожей, которые ГРЕПАЮТ код. Наивный греп
    /// по строке считает «https://» комментарием, а «|зазор| &lt; 0.5 мм» внутри
    /// текста подсказки MCP — зашитым порогом. Первое даёт ложную зелень,
    /// второе — ложную красноту, и оба уже случились здесь.
    ///
    /// Разбор идёт по ФАЙЛУ, а не по строке: @-строки и блочные комментарии
    /// переносятся через строку, и построчный сканер видит их содержимое как
    /// голый код — именно так текст McpGuideTexts попал в нарушители.
    ///
    /// <see cref="WithoutComments(IEnumerable{string})"/> убирает только
    /// комментарии (строковые литералы остаются — в них живёт TMP-разметка
    /// «&lt;color=#RRGGBB&gt;»), <see cref="CodeOnly(IEnumerable{string})"/>
    /// убирает и содержимое строк и символов.</summary>
    public static class SourceLines
    {
        public static IEnumerable<string> WithoutComments(IEnumerable<string> lines) =>
            Scan(lines, keepLiterals: true);

        public static IEnumerable<string> CodeOnly(IEnumerable<string> lines) =>
            Scan(lines, keepLiterals: false);

        public static string WithoutComments(string line) =>
            First(Scan(new[] { line }, keepLiterals: true));

        public static string CodeOnly(string line) =>
            First(Scan(new[] { line }, keepLiterals: false));

        private static string First(IEnumerable<string> lines)
        {
            foreach (var line in lines) return line;
            return string.Empty;
        }

        private static IEnumerable<string> Scan(IEnumerable<string> lines, bool keepLiterals)
        {
            bool inVerbatim = false, inBlockComment = false;
            foreach (var line in lines)
                yield return ScanLine(line, keepLiterals, ref inVerbatim, ref inBlockComment);
        }

        private static string ScanLine(string line, bool keepLiterals,
            ref bool inVerbatim, ref bool inBlockComment)
        {
            var sb = new StringBuilder(line.Length);
            bool inString = inVerbatim, inChar = false;
            bool verbatim = inVerbatim, blockComment = inBlockComment;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (blockComment)
                {
                    if (c == '*' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }

                if (!inString && !inChar)
                {
                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;

                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                    {
                        blockComment = true;
                        i++;
                        continue;
                    }

                    if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        inString = true;
                        verbatim = true;
                        if (keepLiterals) sb.Append("@\"");
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = true;
                        verbatim = false;
                        if (keepLiterals) sb.Append(c);
                        continue;
                    }

                    if (c == '\'')
                    {
                        inChar = true;
                        if (keepLiterals) sb.Append(c);
                        continue;
                    }

                    sb.Append(c);
                    continue;
                }

                if (!verbatim && c == '\\')
                {
                    if (keepLiterals)
                    {
                        sb.Append(c);
                        if (i + 1 < line.Length) sb.Append(line[i + 1]);
                    }
                    i++;
                    continue;
                }

                if (inString && c == '"')
                {
                    if (verbatim && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        if (keepLiterals) sb.Append("\"\"");
                        i++;
                        continue;
                    }

                    inString = false;
                    verbatim = false;
                    if (keepLiterals) sb.Append(c);
                    continue;
                }

                if (inChar && c == '\'')
                {
                    inChar = false;
                    if (keepLiterals) sb.Append(c);
                    continue;
                }

                if (keepLiterals) sb.Append(c);
            }

            inVerbatim = inString && verbatim;
            inBlockComment = blockComment;
            return sb.ToString();
        }
    }
}
