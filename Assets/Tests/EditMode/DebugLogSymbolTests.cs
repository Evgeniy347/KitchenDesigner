using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KitchenDesigner.Tests.EditMode
{
    /// <summary>
    /// Проверяет, что все строковые литералы в коде (Debug.Log*, StringBuilder.Append*,
    /// .text присваивания, и другие) содержат только символы из атласа LiberationSans SDF.
    ///
    /// Исключённые диапазоны (не входят в LiberationSans):
    /// - Box Drawing U+2500-U+257F (──, │, ┌, etc.)
    /// - Block Elements U+2580-U+259F (▀, █, etc.)
    /// - Miscellaneous Symbols U+2600-U+26FF (⚠, ☺, etc.)
    /// - Dingbats U+2700-U+27BF (✓, ✕, etc.)
    /// </summary>
    public class DebugLogSymbolTests
    {
        // Символы которые НЕ входят в LiberationSans SDF
        private static readonly Regex ForbiddenPattern = new Regex(
            @"[\u2500-\u257F\u2580-\u259F\u2600-\u26FF\u2700-\u27BF]",
            RegexOptions.Compiled);

        // Все строковые литералы: "..." или $"..." с поддержкой escape
        private static readonly Regex StringLiteralPattern = new Regex(
            @"\$?""((?:[^""\\]|\\.)*)""",
            RegexOptions.Compiled);

        [Test]
        public void AllStringLiterals_OnlyUseLiberationSansSymbols()
        {
            var scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            var violations = new List<string>();

            var csFiles = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                var lines = File.ReadAllLines(file);
                var relativePath = file.Replace(Application.dataPath + Path.DirectorySeparatorChar, "")
                                       .Replace(Application.dataPath + "/", "");

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var lineNumber = i + 1;

                    // Пропускаем строки-комментарии
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                        continue;

                    ExtractAndCheckAllStringLiterals(line, relativePath, lineNumber, violations);
                }
            }

            if (violations.Count > 0)
            {
                Assert.Fail($"Found {violations.Count} symbol(s) not in LiberationSans atlas:\n" +
                    string.Join("\n", violations));
            }
        }

        private void ExtractAndCheckAllStringLiterals(string line, string file, int lineNumber, List<string> violations)
        {
            foreach (Match match in StringLiteralPattern.Matches(line))
            {
                var text = match.Groups[1].Value;

                // Убрать escape-последовательности
                text = text.Replace("\\n", "")
                           .Replace("\\t", "")
                           .Replace("\\r", "")
                           .Replace("\\\"", "")
                           .Replace("\\\\", "");

                CheckStringForForbiddenSymbols(text, file, lineNumber, violations);
            }
        }

        private void CheckStringForForbiddenSymbols(string text, string file, int lineNumber, List<string> violations)
        {
            foreach (Match m in ForbiddenPattern.Matches(text))
            {
                var c = m.Value[0];
                var preview = text.Length > 60 ? text.Substring(0, 60) + "…" : text;
                violations.Add($"  {file}:{lineNumber}  U+{(int)c:X4} '{c}'  in: \"{preview}\"");
            }
        }
    }
}
