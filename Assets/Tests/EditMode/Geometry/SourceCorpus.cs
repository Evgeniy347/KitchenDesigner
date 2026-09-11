using System.Collections.Concurrent;
using System.IO;

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

        private static readonly ConcurrentDictionary<string, string[]> FilesByDir =
            new ConcurrentDictionary<string, string[]>();

        public static string[] Files(string dir) =>
            (string[])FilesByDir.GetOrAdd(dir,
                d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories)).Clone();

        public static string[] Lines(string file) =>
            (string[])LinesByPath.GetOrAdd(file, File.ReadAllLines).Clone();

        public static string Text(string file) => TextByPath.GetOrAdd(file, File.ReadAllText);
    }
}
