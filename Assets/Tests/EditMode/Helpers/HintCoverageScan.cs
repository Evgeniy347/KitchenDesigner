using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Скан покрытия подсказок «i»: сколько числовых полей/дропдаунов
    /// `*FieldsEditor` и ключей `SettingKeys` остались без `hint`. Живёт отдельно от
    /// <see cref="HintKeyScan"/> — тот отвечает «у каждого заявленного ключа есть текст»,
    /// этот — «у каждого контрола есть заявленный ключ», и это РАЗНЫЕ множества
    /// (conventions/STRUCTURE.md → «A capability table has a twin in the contract»).
    ///
    /// Работает по ИСХОДНИКАМ, а не по построенной панели: строка `Rows.NumberField(...)`
    /// или `_rows.AddToggle(...)` — то же самое место, где стоял бы `hint:`, поэтому его
    /// отсутствие видно текстом, без Unity и без спавна ~40 типов элементов.
    ///
    /// Обёртки одного файла (`LightFieldsEditor.Bind`, `PipeFittingFieldsEditor.ReadOnlyField`,
    /// `PipeFieldsEditor.ReadOnlyField`) читаются как ОТДЕЛЬНЫЕ имена создателей — их вызовы
    /// несут `hint:` литералом ровно там же, где его нёс бы `Rows.NumberField`. Хвостовой
    /// позиционный `hint` (без двоеточия) — это ПЕРЕДАЧА параметра дальше, а не отсутствие
    /// подсказки: такой вызов помечается как «делегирует» и в перебор не идёт — его накроет
    /// вызов уже этой обёртки, найденный отдельно по её собственному имени.</summary>
    public static class HintCoverageScan
    {
        public readonly struct Gap
        {
            public readonly string Id;
            public readonly string Detail;

            public Gap(string id, string detail)
            {
                Id = id;
                Detail = detail;
            }

            public override string ToString() => Id;
        }

        private readonly struct CreatorName
        {
            public readonly string Name;
            public readonly string? OnlyInFile;

            public CreatorName(string name, string? onlyInFile = null)
            {
                Name = name;
                OnlyInFile = onlyInFile;
            }
        }

        private enum Coverage
        {
            Covered,
            Delegated,
            Uncovered,
        }

        private static readonly Regex HintLiteral = new Regex(@"\bhint\s*:\s*""[^""]*""");
        private static readonly Regex HintNamedIdentifier = new Regex(@"\bhint\s*:\s*[A-Za-z_]\w*\b");
        private static readonly Regex Modifier = new Regex(@"\b(private|protected|public|internal|static)\b");

        private static readonly IReadOnlyList<CreatorName> FieldsEditorCreators = new[]
        {
            new CreatorName("Rows.NumberField"),
            new CreatorName("Rows.Dropdown"),
            new CreatorName("Rows.NamedDropdown"),
            new CreatorName("Rows.LabelledNumberField"),
            new CreatorName("Bind", "LightFieldsEditor.cs"),
            new CreatorName("ReadOnlyField", "PipeFieldsEditor.cs"),
            new CreatorName("ReadOnlyField", "PipeFittingFieldsEditor.cs"),
        };

        public static string UiDir() => Path.Combine(HintKeyScan.CoreDir(), "UI");

        public static string SettingKeysFile() =>
            Path.Combine(HintKeyScan.CoreDir(), "MCP", "SettingKeys.cs");

        // ---------------- *FieldsEditor: number fields and dropdowns ----------------

        public static List<Gap> FieldsEditorGaps()
        {
            var gaps = new List<Gap>();

            foreach (var file in FieldsEditorFiles())
            {
                var name = Path.GetFileName(file);
                var text = JoinedText(file);

                foreach (var creator in FieldsEditorCreators)
                {
                    if (creator.OnlyInFile != null && creator.OnlyInFile != name) continue;

                    foreach (var (matchStart, openParen) in FindCallSites(text, creator.Name))
                    {
                        int lineStart = LineStart(text, matchStart);
                        if (Modifier.IsMatch(text.Substring(lineStart, matchStart - lineStart))) continue;

                        var (content, afterClose) = ExtractBalanced(text, openParen);
                        var args = SplitTopLevelArgs(content);
                        var verdict = Classify(args);
                        if (verdict != Coverage.Uncovered) continue;

                        int stmtSemi = text.IndexOf(';', afterClose);
                        int nextStatementStart = stmtSemi < 0 ? text.Length : stmtSemi + 1;
                        if (LooksAheadForAnAttachedHint(text, nextStatementStart)) continue;

                        string row = args.Count > 0 ? Unquote(args[0]) : Snippet(content);
                        gaps.Add(new Gap(name + " → " + creator.Name + "(" + row + "…)",
                            name + ": вызов " + creator.Name + " без hint — " + Snippet(content)));
                    }
                }
            }

            return Dedup(gaps);
        }

        public static IReadOnlyList<string> FieldsEditorFiles() =>
            Directory.GetFiles(UiDir(), "*FieldsEditor.cs", SearchOption.TopDirectoryOnly);

        // ---------------- SettingKeys: every wire entry ----------------

        public readonly struct SettingKeyEntry
        {
            public readonly string Wire;
            public readonly string Member;

            public SettingKeyEntry(string wire, string member)
            {
                Wire = wire;
                Member = member;
            }
        }

        private static readonly Regex SettingKeyCall = new Regex(
            @"SettingKey\.(?:Flag|Number)\(\s*""([^""]+)""\s*,\s*""[^""]+""\s*,\s*\(\)\s*=>\s*([^,]+),");

        public static IReadOnlyList<SettingKeyEntry> SettingKeysDeclared()
        {
            var text = SourceCorpus.Text(SettingKeysFile());
            var result = new List<SettingKeyEntry>();
            foreach (Match m in SettingKeyCall.Matches(text))
                result.Add(new SettingKeyEntry(m.Groups[1].Value, LastMember(m.Groups[2].Value)));
            return result;
        }

        public static List<Gap> SettingKeysGaps()
        {
            var gaps = new List<Gap>();
            var tabFiles = Directory.GetFiles(UiDir(), "Settings*Tab.cs", SearchOption.TopDirectoryOnly);
            var tabTexts = tabFiles.ToDictionary(f => Path.GetFileName(f)!, JoinedText);

            foreach (var entry in SettingKeysDeclared())
            {
                bool found = false;
                bool covered = false;
                var reference = new Regex(@"\." + Regex.Escape(entry.Member) + @"\b");

                foreach (var text in tabTexts.Values)
                {
                    foreach (Match m in reference.Matches(text))
                    {
                        found = true;
                        int stmtStart = StatementStart(text, m.Index);
                        int stmtEnd = StatementEnd(text, m.Index);
                        var statement = text.Substring(stmtStart, stmtEnd - stmtStart + 1);
                        if (HintLiteral.IsMatch(statement) || LooksAheadForAnAttachedHint(text, stmtEnd + 1))
                            covered = true;
                    }
                }

                if (!found)
                    gaps.Add(new Gap("SettingKeys." + entry.Wire,
                        entry.Wire + " (" + entry.Member
                        + "): в панелях настроек нет строки для этого ключа"));
                else if (!covered)
                    gaps.Add(new Gap("SettingKeys." + entry.Wire,
                        entry.Wire + " (" + entry.Member + "): строка есть, подсказки нет"));
            }

            return Dedup(gaps);
        }

        // ---------------- shared text primitives ----------------

        private static string JoinedText(string file) =>
            string.Join("\n", SourceLines.WithoutComments(SourceCorpus.Lines(file)));

        private static List<Gap> Dedup(List<Gap> gaps) =>
            gaps.GroupBy(g => g.Id).Select(g => g.First())
                .OrderBy(g => g.Id, StringComparer.Ordinal).ToList();

        private static string LastMember(string expr)
        {
            var matches = Regex.Matches(expr, @"\.([A-Za-z_]\w*)");
            return matches.Count > 0 ? matches[matches.Count - 1].Groups[1].Value : expr.Trim();
        }

        private static IEnumerable<(int matchStart, int openParen)> FindCallSites(string text, string name)
        {
            var pattern = new Regex(@"(?<![\w.])" + Regex.Escape(name) + @"\s*(?:<[^>]*>)?\s*\(");
            foreach (Match m in pattern.Matches(text))
                yield return (m.Index, m.Index + m.Length - 1);
        }

        private static int LineStart(string text, int index)
        {
            int i = text.LastIndexOf('\n', Math.Max(0, index - 1));
            return i < 0 ? 0 : i + 1;
        }

        private static int StatementStart(string text, int index)
        {
            int i = text.LastIndexOf(';', Math.Max(0, index - 1));
            return i < 0 ? 0 : i + 1;
        }

        private static int StatementEnd(string text, int index)
        {
            int i = text.IndexOf(';', index);
            return i < 0 ? text.Length - 1 : i;
        }

        /// <summary>Балансирует скобки начиная с `text[openParen] == '('`. Строковые литералы
        /// не считаются источником скобок — иначе `"Ø корпуса"`, случись в ней `(`, сдвинул бы
        /// границу вызова.</summary>
        private static (string content, int afterClose) ExtractBalanced(string text, int openParen)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = openParen; i < text.Length; i++)
            {
                char c = text[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                        return (text.Substring(openParen + 1, i - openParen - 1), i + 1);
                }
            }
            return (text.Substring(openParen + 1), text.Length);
        }

        private static List<string> SplitTopLevelArgs(string content)
        {
            var args = new List<string>();
            int depth = 0;
            bool inStr = false;
            int start = 0;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '(' || c == '<' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == '>' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    args.Add(content.Substring(start, i - start));
                    start = i + 1;
                }
            }
            args.Add(content.Substring(start));
            return args.Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
        }

        private static Coverage Classify(IReadOnlyList<string> args)
        {
            var callContent = string.Join(",", args);
            if (HintLiteral.IsMatch(callContent)) return Coverage.Covered;
            if (HintNamedIdentifier.IsMatch(callContent)) return Coverage.Delegated;

            if (args.Count > 0 && args[args.Count - 1] == "hint") return Coverage.Delegated;
            return Coverage.Uncovered;
        }

        private static string Unquote(string arg)
        {
            var m = Regex.Match(arg, "^\"([^\"]*)\"$");
            return m.Success ? m.Groups[1].Value : arg;
        }

        /// <summary>Второй способ повесить «i»: не именованным аргументом самого вызова, а
        /// отдельной строкой `Hint(key, hint: "...")` / `HintBadge.AttachAfterLabel(label,
        /// hint: "...")` следом — так делают `WallFieldsEditor` (маска/шов/запас через
        /// `LabelledNumberField`+`HintBadge.AttachAfterLabel`, с промежуточным присваиванием
        /// `_masonry = masonryRow.dropdown;` между ними) и панели настроек (`Hint(rowKey,
        /// hint)` после `_rows.AddXxx`).
        ///
        /// Окно — не «ближайшая пустая строка»: секции `SettingsLightTab` идут БЕЗ пустых
        /// строк между рядами, и такое окно захватывало `Hint(...)` уже СЛЕДУЮЩЕГО ряда,
        /// выдавая чужую подсказку за подсказку этого поля. Вместо этого — переход по
        /// операторам: пока очередной оператор не содержит вызова (голое присваивание вроде
        /// `_masonry = masonryRow.dropdown;`), пропускаем его и идём к следующему; первый
        /// оператор С вызовом решает вопрос — это либо сама подсказка, либо (создание
        /// следующего контрола, что угодно ещё) ответ «нет».</summary>
        private static bool LooksAheadForAnAttachedHint(string text, int nextStatementStart)
        {
            int pos = nextStatementStart;
            for (int hop = 0; hop < 4; hop++)
            {
                if (pos >= text.Length) return false;
                int nextSemi = text.IndexOf(';', pos);
                if (nextSemi < 0 || nextSemi - nextStatementStart > 600) return false;

                var statement = text.Substring(pos, nextSemi - pos + 1);
                if (statement.Contains('('))
                    return HintLiteral.IsMatch(statement)
                           && (statement.Contains("HintBadge.Attach") || Regex.IsMatch(statement, @"\bHint\("));

                pos = nextSemi + 1;
            }
            return false;
        }

        private static string Snippet(string content) =>
            content.Length > 70 ? content.Substring(0, 70) + "…" : content;
    }
}
