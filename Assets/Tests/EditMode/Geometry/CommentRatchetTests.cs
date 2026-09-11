using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Храповик по комментариям. Правило «комментарии живут в тестах»
    /// (CONVENTIONS.md) до сих пор держалось только на памяти агента, и счётчик
    /// после кампании зачистки пополз обратно вверх. Тест фиксирует ПОТОЛОК на
    /// каталог: выше — падение с именами файлов, ниже — тоже падение, с просьбой
    /// опустить число. Второе не придирка: незакрытый храповик отдаёт назад ровно
    /// то, что только что вычистили.
    ///
    /// Считаются НАЧАЛА комментариев вне строковых литералов, поэтому «https://»
    /// и «"/*"» в коде не идут в счёт.</summary>
    public class CommentRatchetTests
    {
        private static readonly (string dir, int ceiling)[] Budgets =
        {
            (".", 0),
            ("Analysis", 0),
            ("Bulk", 0),
            ("Commands", 0),
            ("Diagnostics", 0),
            ("Elements", 0),
            ("Geometry", 0),
            ("Infrastructure", 0),
            ("Interfaces", 0),
            ("MCP", 0),
            ("Materials", 0),
            ("Measure", 0),
            ("Persistence", 0),
            ("Platform", 0),
            ("Pure", 0),
            ("Rendering", 0),
            ("Snap", 38),
            ("Tools", 0),
            ("UI", 0),
            ("Update", 0),
            ("Validation", 0),
        };

        private static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        public static bool StartsAComment(string line)
        {
            bool inString = false, inChar = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '\\' && (inString || inChar)) { i++; continue; }
                if (c == '"' && !inChar) { inString = !inString; continue; }
                if (c == '\'' && !inString) { inChar = !inChar; continue; }
                if (inString || inChar) continue;

                if (c == '/' && i + 1 < line.Length && (line[i + 1] == '/' || line[i + 1] == '*'))
                    return true;
            }

            return false;
        }

        private static int CommentLines(string file) =>
            File.ReadAllLines(file).Count(StartsAComment);

        private static Dictionary<string, int> CountsByDirectory()
        {
            var core = CoreDir();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                var relative = file.Substring(core.Length).TrimStart(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dir = parts.Length > 1 ? parts[0] : ".";

                counts.TryGetValue(dir, out int already);
                counts[dir] = already + CommentLines(file);
            }

            return counts;
        }

        [Test]
        public void ProductionSource_CarriesNoNewComments()
        {
            var counts = CountsByDirectory();
            var budget = Budgets.ToDictionary(b => b.dir, b => b.ceiling, StringComparer.Ordinal);
            var grew = new List<string>();
            var shrank = new List<string>();

            foreach (var pair in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int ceiling = budget.TryGetValue(pair.Key, out int c) ? c : 0;
                if (pair.Value > ceiling)
                    grew.Add(pair.Key + ": " + pair.Value + " > " + ceiling);
                else if (pair.Value < ceiling)
                    shrank.Add(pair.Key + ": " + pair.Value + " < " + ceiling);
            }

            Assert.IsEmpty(grew,
                "Комментарий в исходнике — это объяснение, которому место в ТЕСТЕ "
                + "(CONVENTIONS.md → «Comments live in tests»). Новый каталог начинает с нуля. "
                + "Выросло:\n" + string.Join("\n", grew));

            Assert.IsEmpty(shrank,
                "Зачистка прошла — опусти потолок в Budgets до нового значения тем же коммитом, "
                + "иначе храповик отдаст назад то, что вычищено:\n" + string.Join("\n", shrank));
        }

        [Test]
        public void TheCounter_SeesAComment_AndIgnoresAUrlAndAStringLiteral()
        {
            Assert.IsTrue(StartsAComment("    // объяснение"), "строчный комментарий");
            Assert.IsTrue(StartsAComment("    /// <summary>"), "XML-doc — тоже комментарий");
            Assert.IsTrue(StartsAComment("    /* блок */"), "блочный комментарий");
            Assert.IsTrue(StartsAComment("var x = 1; // хвостовой"),
                "хвостовые комментарии — половина всех: счёт по началу строки их теряет");

            Assert.IsFalse(StartsAComment("var url = \"https://example.com\";"),
                "две косые внутри строки — не комментарий");
            Assert.IsFalse(StartsAComment("var s = \"/* not a comment */\";"));
            Assert.IsFalse(StartsAComment("var c = '/';"));
            Assert.IsFalse(StartsAComment("var q = \"\\\"//\\\"\";"),
                "экранированная кавычка не закрывает строку");
        }

        [Test]
        public void TheScan_ActuallySeesTheSource()
        {
            var counts = CountsByDirectory();
            CollectionAssert.Contains(counts.Keys, "Geometry",
                "скан не видит каталогов ядра — по пустому пути он зеленеет, ничего не проверив");
            CollectionAssert.Contains(counts.Keys, "UI");
        }

        [Test]
        public void EveryBudgetLine_NamesADirectoryThatExists()
        {
            var counts = CountsByDirectory();
            foreach (var (dir, _) in Budgets)
                CollectionAssert.Contains(counts.Keys, dir,
                    "потолок для " + dir + " пережил свой каталог: запись начнёт молча "
                    + "освобождать следующий каталог с этим именем");
        }
    }
}
