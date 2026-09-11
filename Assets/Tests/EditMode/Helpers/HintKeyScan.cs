using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Скан ключей подсказок по исходникам `Assets/Scripts/Core`. Живёт
    /// отдельно от обоих своих сторожей, потому что их ДВА и они в разных сборках:
    /// наполнение проверяет <c>HintTextGuardTests</c> (чистый слой), видимость —
    /// <c>HintBadgeVisibilityTests</c> (сцена), и второе описание того же множества
    /// разъехалось бы с первым, как разъезжается любая вторая копия таблицы
    /// (conventions/STRUCTURE.md → «A capability table has a twin in the contract»).
    ///
    /// Дерево читается ОДИН раз на прогон: четыре теста звали скан заново, то есть
    /// ~2700 чтений диска вместо 690. Что скан действительно что-то видит, стережёт
    /// <c>HintTextGuardTests.TheScan_FindsTheSamplePanel</c>.</summary>
    public static class HintKeyScan
    {
        public static readonly Regex Declared = new Regex(@"\bhint\s*:\s*""([^""]*)""");

        private static List<(string file, string line)>? _codeLines;

        private static List<(string file, string key)>? _keysInCode;

        public static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        public static List<(string file, string line)> CodeLines() =>
            _codeLines ??= SourceCorpus.Files(CoreDir())
                .SelectMany(file => SourceLines.WithoutComments(SourceCorpus.Lines(file))
                    .Select(line => (Path.GetFileName(file), line)))
                .ToList();

        public static IReadOnlyList<(string file, string key)> DeclaredKeys() =>
            _keysInCode ??= CodeLines()
                .SelectMany(p => Declared.Matches(p.line).Cast<Match>()
                    .Select(m => (p.file, m.Groups[1].Value)))
                .ToList();
    }
}
