using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Один прочитанный корпус исходников на весь прогон, для сторожей,
    /// которые ГРЕПАЮТ код вместо того, чтобы строить сцену.
    ///
    /// Таких сторожей около двадцати, и каждый ТЕСТ в них перечитывал с диска всё
    /// дерево <c>Assets/Scripts</c> заново: четыре теста
    /// <see cref="GeometryArchitectureTests"/> стоили 4,3 с из четырёх секунд всего
    /// быстрого пути, хотя читали одни и те же файлы. Исходники за прогон не
    /// меняются, поэтому чтение платится один раз на путь, а не один раз на тест.
    ///
    /// Кэш обязан быть потокобезопасным: <c>[assembly: Parallelizable]</c> гоняет
    /// эти тесты на нескольких потоках сразу. И он обязан отдавать КОПИЮ — общий
    /// массив, который один сторож отсортировал бы под себя, молча испортил бы
    /// ответ соседнему. Копия массива ссылок стоит микросекунды против
    /// миллисекунд диска.
    ///
    /// Для файлов, которые тест сам СОЗДАЁТ на время (временные каталоги, планты),
    /// кэш не годится по определению — там остаётся обычный
    /// <see cref="File.ReadAllLines(string)"/>.</summary>
    public static class SourceCorpus
    {
        private static readonly ConcurrentDictionary<string, string[]> LinesByPath =
            new ConcurrentDictionary<string, string[]>();

        private static readonly ConcurrentDictionary<string, string> TextByPath =
            new ConcurrentDictionary<string, string>();

        private static readonly ConcurrentDictionary<string, Regex> RulesByPattern =
            new ConcurrentDictionary<string, Regex>();

        private static readonly ConcurrentDictionary<string, string[]> FilesByDir =
            new ConcurrentDictionary<string, string[]>();

        public static string[] Files(string dir) =>
            (string[])FilesByDir.GetOrAdd(dir,
                d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories)).Clone();

        public static string[] Lines(string file) =>
            (string[])LinesByPath.GetOrAdd(file, File.ReadAllLines).Clone();

        public static string Text(string file) => TextByPath.GetOrAdd(file, File.ReadAllText);

        /// <summary>Скомпилированное выражение по своему шаблону. Статический
        /// <c>Regex.IsMatch(line, pattern)</c> ходит за разобранным выражением в
        /// общий кэш на КАЖДЫЙ вызов, а вызовов у сторожа столько, сколько строк в
        /// дереве исходников, умноженное на число запретов.</summary>
        public static Regex Rule(string pattern) =>
            RulesByPattern.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled));

        /// <summary>Строка начинает комментарий. Отдельный метод не ради краткости:
        /// <c>StartsWith("//")</c> без указания сравнения сверяет строки ПО КУЛЬТУРЕ,
        /// и на дереве исходников это стоит дороже самого запрета, который проверяют
        /// следом.</summary>
        public static bool StartsAComment(string trimmed) =>
            trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal);
    }
}
