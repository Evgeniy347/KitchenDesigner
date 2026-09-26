using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        private static readonly Regex NormativeDocRef =
            new Regex(@"^(ГОСТ|СП|СНиП|ЕНиР|ТР|ISO|EN)\s[\w.\-/]+(\sп\.\d+(\.\d+)*)?$",
                RegexOptions.Compiled);

        public static int CommentStartIndex(string line)
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
                    return i;
            }

            return -1;
        }

        public static bool StartsAComment(string line) => CommentStartIndex(line) >= 0;

        /// <summary>CONVENTIONS.md → COMMENTS «a normative source on a constant»: a trailing
        /// "// ГОСТ|СП|СНиП|ЕНиР|ТР|ISO|EN …" on a line that declares a const is exempt from the
        /// ordinary-comment ceiling. Only a "//" line comment qualifies — "/*" does not, and the
        /// reference must trail the SAME line as the const, not stand alone above it.</summary>
        public static bool IsNormativeConstComment(string line)
        {
            int idx = CommentStartIndex(line);
            if (idx < 0 || idx + 1 >= line.Length || line[idx + 1] != '/')
                return false;

            if (!Regex.IsMatch(line.Substring(0, idx), @"\bconst\b"))
                return false;

            return NormativeDocRef.IsMatch(line.Substring(idx + 2).Trim());
        }

        private static int CommentLines(string file) =>
            File.ReadAllLines(file).Count(l => StartsAComment(l) && !IsNormativeConstComment(l));

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
        public void IsNormativeConstComment_TrailingDocIdOnAConst_IsTrue()
        {
            Assert.IsTrue(IsNormativeConstComment(
                "        private const float MinSoleMarginMm = 100f; // СП 22.13330.2016"),
                "СП — признанный код нормативного документа");
            Assert.IsTrue(IsNormativeConstComment(
                "        internal const int RebarStepMm = 200; // ГОСТ 34028"),
                "ГОСТ — признанный код нормативного документа");
            Assert.IsTrue(IsNormativeConstComment(
                "        public const int SlabDefaultMm = 200; // EN 1992-1-1"),
                "EN — признанный код нормативного документа (иностранный стандарт)");
        }

        [Test]
        public void IsNormativeConstComment_OppositeInputs_AreFalse()
        {
            Assert.IsFalse(IsNormativeConstComment(
                "        private const float MinSoleMarginMm = 100f; // approx, not sourced"),
                "хвост не начинается с кода нормы — обычный комментарий, не исключение");

            Assert.IsFalse(IsNormativeConstComment(
                "        private float MinSoleMarginMm = 100f; // СП 22.13330.2016"),
                "строка не объявляет const — исключение не для полей вообще");

            Assert.IsFalse(IsNormativeConstComment(
                "        // СП 22.13330.2016 обосновывает значение ниже"),
                "ссылка не хвостовая на той же строке, что const — отдельный комментарий сверху");

            Assert.IsFalse(IsNormativeConstComment(
                "        private const float Gap = 100f; /* СП 22.13330.2016 */"),
                "блочный комментарий /* */, а исключение — только для хвостового //");
        }

        [Test]
        public void IsNormativeConstComment_ProseAfterTheDocId_IsFalse()
        {
            Assert.IsFalse(IsNormativeConstComment(
                "        private const int RebarStepMm = 200; // ГОСТ 34028, шаг арматуры"),
                "запятая и слова после номера документа — это уже объяснение, а не сам "
                + "идентификатор; храповик обязан считать эту строку обычным комментарием");

            Assert.IsFalse(IsNormativeConstComment(
                "        private const int X = 7; // EN: temporary workaround for Unity bug"),
                "«EN:» — это код стандарта БЕЗ номера и с двоеточием вместо пробела перед "
                + "текстом; \\b совпадает между N и :, но идентификатора документа тут нет");

            Assert.IsFalse(IsNormativeConstComment(
                "        public const int SlabDefaultMm = 200; // СП 60.13330.2020, "
                + "приложение Л, табл. Л.1"),
                "перечисление приложения и таблицы после номера документа — прозаический "
                + "хвост, исключение — только для голого идентификатора");
        }

        [Test]
        public void NormativeConstComments_AreExcludedFromTheOrdinaryCount()
        {
            var dir = Path.Combine(Path.GetTempPath(), "CommentRatchetTests_" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            try
            {
                var file = Path.Combine(dir, "Sample.cs");
                File.WriteAllLines(file, new[]
                {
                    "class Sample",
                    "{",
                    "    private const float A = 100f; // СП 22.13330.2016",
                    "    private const float B = 50f; // just a hunch, not sourced",
                    "}",
                });

                Assert.AreEqual(1, CommentLines(file),
                    "строка A — нормативная ссылка на const, исключена; строка B — обычный "
                    + "комментарий и должна попасть в счёт");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
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
