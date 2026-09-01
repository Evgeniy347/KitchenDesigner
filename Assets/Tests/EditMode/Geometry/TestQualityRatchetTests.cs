using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Два дешёвых сторожа за качеством самих тестов
    /// , устроенные как храповик комментариев:
    /// потолок на группу, падение в ОБЕ стороны, тесты на собственный сканер.
    ///
    /// 1. Имя теста — это предложение о поведении (CONVENTIONS.md → «Test naming
    ///    conventions»): {Class}_{Method}_{Scenario}. Имя без подчёркивания
    ///    («StartsClosed») не говорит, чьё это поведение, а «..._Works» не говорит
    ///    вообще ничего — при красноте в списке из 2900 тестов такое имя стоит
    ///    отдельного захода в файл.
    /// 2. Assert без сообщения — это удалённый комментарий, не доехавший до теста.
    ///    Правило «comments live in tests» переносит ПОЧЕМУ в сообщение ассерта,
    ///    поэтому в наборах быстрого пути, куда это знание и переезжает,
    ///    сообщение обязательно. Пока только Geometry — почему, сказано у
    ///    <see cref="SilentAssertBudgets"/>.</summary>
    public class TestQualityRatchetTests
    {
        private static readonly (string group, int ceiling)[] NameBudgets =
        {
            ("EditMode", 13),
            ("EditMode/Geometry", 0),
            ("EditMode/Pure", 1),
            // Было 4: имена генераторов картинок для docs/ выправлены вместе с
            // переводом их на DocsArtifactFixture. Остался CapturePhotoMode.
            ("PlayMode", 1),
        };

        /// <summary>Долг по молчащим ассертам, снятый на 469f2e0f, по файлу.
        /// Потолок на ФАЙЛ, а не на каталог: общий счёт на каталог разрешал бы
        /// внести молчащий ассерт в один файл, вычистив его в другом.
        /// Каталог один — Geometry: Assets/Tests/EditMode/Pure сейчас наполняет
        /// другой агент (перенос ElementData), и потолки на его файлы ломали бы
        /// ему прогон каждым добавленным тестом. Pure входит сюда, когда перенос
        /// сядет; запись об этом — в потолках ниже.</summary>
        private static readonly (string file, int ceiling)[] SilentAssertBudgets =
        {
            ("CommentRatchetTests.cs", 2),
            ("DrawerConstantsTests.cs", 48),
            ("ElementGeometryTests.cs", 21),
            ("FloorDecorUvTests.cs", 2),
            ("GappedBoxTests.cs", 20),
            ("GrooveSpecTests.cs", 8),
            ("ResizeMathTests.cs", 17),
            ("ResizeSnapGrooveTests.cs", 10),
            ("ResizeSnapTests.cs", 8),
            ("SnapCoreContractTests.cs", 6),
            ("SnapCoreEdgeCaseTests.cs", 1),
            ("SnapCoreExistingContactTests.cs", 5),
            ("SnapCoreGrooveTests.cs", 9),
            ("SnapCoreLineContactTests.cs", 2),
            ("SnapPostEdgeDetentTests.cs", 1),
            ("ToleranceLiteralTests.cs", 1),
            ("ToleranceTests.cs", 32),
            ("UiColorLiteralTests.cs", 1),
            ("UnityMessageShadowingTests.cs", 1),
            ("ValidationCoreTests.cs", 51),
        };

        private static readonly string[] VacuousTails =
        {
            "Works", "Work", "Ok", "Okay", "Test", "Tests", "Basic", "Simple",
            "Correct", "Valid", "Passes", "Good", "Fine",
        };

        private static readonly string[] NotAnAssertion =
        {
            "Fail", "Ignore", "Inconclusive", "Pass", "Warn", "Multiple",
        };

        private const string TestAttributePattern =
            @"\[\s*(?:Test|TestCase|TestCaseSource|UnityTest|Theory)\b";

        private const string MethodDeclarationPattern =
            @"\b(?:public|private|internal|protected)\s+"
                + @"(?:(?:static|async|virtual|override|sealed|new|unsafe|extern|partial)\s+)*"
                + @"[\w<>,\[\]\.\?]+\s+(\w+)\s*\(";

        private const string AssertCallPattern =
            @"\b(?:Assert|CollectionAssert|StringAssert|FileAssert|DirectoryAssert)\.(\w+)\s*\(";

        private static string TestsDir() => RepoPaths.Subdir("Assets", "Tests");

        public static bool NameStatesNothing(string name)
        {
            var parts = name.Split('_').Where(p => p.Length > 0).ToArray();
            if (parts.Length < 2) return true;
            return VacuousTails.Contains(parts[parts.Length - 1], StringComparer.Ordinal);
        }

        public static IEnumerable<string> TestMethodNames(string[] lines)
        {
            var claimed = new HashSet<int>();

            for (int i = 0; i < lines.Length; i++)
            {
                var attribute = Regex.Match(lines[i], TestAttributePattern);
                if (!attribute.Success) continue;

                for (int j = i; j < lines.Length && j <= i + 8; j++)
                {
                    int from = j == i ? attribute.Index + attribute.Length : 0;
                    if (from > lines[j].Length) continue;

                    var declaration = Regex.Match(lines[j].Substring(from), MethodDeclarationPattern);
                    if (!declaration.Success) continue;
                    if (claimed.Add(j)) yield return declaration.Groups[1].Value;
                    break;
                }
            }
        }

        /// <summary>Тот же текст, где содержимое строк, символов и комментариев
        /// заменено пробелами, а ДЛИНА сохранена — позиции в маске и в оригинале
        /// совпадают. Без неё сканер считает нарушением собственные примеры
        /// («"Assert.AreEqual(2, x);"» внутри теста на сканер) и любой ассерт,
        /// процитированный в сообщении соседнего ассерта.</summary>
        public static string MaskLiteralsAndComments(string source)
        {
            var masked = new StringBuilder(source.Length);
            bool inString = false, inChar = false, verbatim = false, lineComment = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];

                if (lineComment)
                {
                    if (c == '\n') { lineComment = false; masked.Append(c); }
                    else masked.Append(' ');
                    continue;
                }

                if (inString || inChar)
                {
                    masked.Append(' ');
                    if (!verbatim && c == '\\' && i + 1 < source.Length)
                    {
                        masked.Append(' ');
                        i++;
                        continue;
                    }

                    if (inString && c == '"')
                    {
                        if (verbatim && i + 1 < source.Length && source[i + 1] == '"')
                        {
                            masked.Append(' ');
                            i++;
                            continue;
                        }

                        inString = false;
                        verbatim = false;
                    }
                    else if (inChar && c == '\'')
                    {
                        inChar = false;
                    }

                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    lineComment = true;
                    masked.Append(' ');
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    verbatim = i > 0 && source[i - 1] == '@';
                    masked.Append(' ');
                    continue;
                }

                if (c == '\'')
                {
                    inChar = true;
                    masked.Append(' ');
                    continue;
                }

                masked.Append(c);
            }

            return masked.ToString();
        }

        public static IReadOnlyList<string> AssertsWithoutMessage(string source)
        {
            var silent = new List<string>();

            foreach (Match call in Regex.Matches(MaskLiteralsAndComments(source), AssertCallPattern))
            {
                var method = call.Groups[1].Value;
                if (NotAnAssertion.Contains(method, StringComparer.Ordinal)) continue;

                var arguments = SplitArguments(source, call.Index + call.Length);
                if (arguments == null || arguments.Count == 0) continue;

                var last = arguments[arguments.Count - 1].TrimStart();
                if (LooksLikeAMessage(last)) continue;

                silent.Add(method + "(" + Shorten(string.Join(", ", arguments)) + ")");
            }

            return silent;
        }

        private static bool LooksLikeAMessage(string argument) =>
            argument.StartsWith("\"", StringComparison.Ordinal)
            || argument.StartsWith("$\"", StringComparison.Ordinal)
            || argument.StartsWith("@\"", StringComparison.Ordinal)
            || argument.StartsWith("$@\"", StringComparison.Ordinal);

        private static string Shorten(string text)
        {
            var flat = Regex.Replace(text, @"\s+", " ").Trim();
            return flat.Length <= 70 ? flat : flat.Substring(0, 70) + "…";
        }

        private static List<string>? SplitArguments(string source, int start)
        {
            var arguments = new List<string>();
            var current = new StringBuilder();
            int depth = 1;
            bool inString = false, inChar = false, verbatim = false;

            for (int i = start; i < source.Length; i++)
            {
                char c = source[i];

                if (inString || inChar)
                {
                    current.Append(c);
                    if (!verbatim && c == '\\' && i + 1 < source.Length)
                    {
                        current.Append(source[++i]);
                        continue;
                    }

                    if (inString && c == '"')
                    {
                        if (verbatim && i + 1 < source.Length && source[i + 1] == '"')
                        {
                            current.Append(source[++i]);
                            continue;
                        }

                        inString = false;
                        verbatim = false;
                    }
                    else if (inChar && c == '\'')
                    {
                        inChar = false;
                    }

                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    verbatim = i > 0 && source[i - 1] == '@';
                    current.Append(c);
                    continue;
                }

                if (c == '\'')
                {
                    inChar = true;
                    current.Append(c);
                    continue;
                }

                if (c == '(' || c == '[' || c == '{') { depth++; current.Append(c); continue; }

                if (c == ')' || c == ']' || c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        var tail = current.ToString();
                        if (tail.Trim().Length > 0 || arguments.Count > 0) arguments.Add(tail);
                        return arguments;
                    }

                    current.Append(c);
                    continue;
                }

                if (c == ',' && depth == 1)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            return null;
        }

        private static string GroupOf(string file, string root)
        {
            var relative = file.Substring(root.Length).TrimStart(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length > 2 ? parts[0] + "/" + parts[1] : parts[0];
        }

        private static Dictionary<string, List<string>> NamesThatStateNothing()
        {
            var root = TestsDir();
            var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var group = GroupOf(file, root);
                if (!found.TryGetValue(group, out var list))
                    found[group] = list = new List<string>();

                foreach (var name in TestMethodNames(File.ReadAllLines(file)))
                    if (NameStatesNothing(name))
                        list.Add(Path.GetFileName(file) + ": " + name);
            }

            return found;
        }

        private static Dictionary<string, int> SilentAssertsOnTheFastPath()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var root = RepoPaths.Subdir("Assets", "Tests", "EditMode", "Geometry");

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                int silent = AssertsWithoutMessage(File.ReadAllText(file)).Count;
                if (silent > 0) counts[Path.GetFileName(file)] = silent;
            }

            return counts;
        }

        [Test]
        public void TestNames_StateTheBehaviour_NotJustAWord()
        {
            var found = NamesThatStateNothing();
            var budget = NameBudgets.ToDictionary(b => b.group, b => b.ceiling, StringComparer.Ordinal);
            var grew = new List<string>();
            var shrank = new List<string>();

            foreach (var pair in found.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int ceiling = budget.TryGetValue(pair.Key, out int c) ? c : 0;
                if (pair.Value.Count > ceiling)
                    grew.Add(pair.Key + ": " + pair.Value.Count + " > " + ceiling + "\n    "
                        + string.Join("\n    ", pair.Value));
                else if (pair.Value.Count < ceiling)
                    shrank.Add(pair.Key + ": " + pair.Value.Count + " < " + ceiling);
            }

            Assert.IsEmpty(grew,
                "Имя теста обязано читаться как утверждение о поведении: "
                + "{Class}_{Method}_{Scenario} (CONVENTIONS.md). Имя без подчёркивания не "
                + "называет предмет, «..._Works» не называет ожидаемый результат:\n"
                + string.Join("\n", grew));

            Assert.IsEmpty(shrank,
                "Имена переименованы — опусти потолок в NameBudgets тем же коммитом, иначе "
                + "храповик отдаст назад ровно то, что только что выправлено:\n"
                + string.Join("\n", shrank));
        }

        [Test]
        public void Asserts_OnTheFastPath_CarryAMessage()
        {
            var counts = SilentAssertsOnTheFastPath();
            var budget = SilentAssertBudgets.ToDictionary(b => b.file, b => b.ceiling, StringComparer.Ordinal);
            var grew = new List<string>();
            var shrank = new List<string>();

            foreach (var pair in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int ceiling = budget.TryGetValue(pair.Key, out int c) ? c : 0;
                if (pair.Value > ceiling) grew.Add(pair.Key + ": " + pair.Value + " > " + ceiling);
            }

            foreach (var (file, ceiling) in SilentAssertBudgets)
            {
                counts.TryGetValue(file, out int actual);
                if (actual < ceiling) shrank.Add(file + ": " + actual + " < " + ceiling);
            }

            Assert.IsEmpty(grew,
                "Ассерт без сообщения на быстром пути — это ПОЧЕМУ, не доехавшее из удалённого "
                + "комментария (CONVENTIONS.md → «Comments live in tests»). Сообщение по-русски, "
                + "объясняющее причину, а не повторяющее вызов:\n" + string.Join("\n", grew));

            Assert.IsEmpty(shrank,
                "Сообщения дописаны — опусти потолок в SilentAssertBudgets тем же коммитом:\n"
                + string.Join("\n", shrank));
        }

        [Test]
        public void TheNameRule_RejectsAVacuousName_AndAcceptsASentence()
        {
            Assert.IsTrue(NameStatesNothing("Test1"), "имя-заглушка ничего не называет");
            Assert.IsTrue(NameStatesNothing("StartsClosed"), "без подчёркивания не назван предмет");
            Assert.IsTrue(NameStatesNothing("Foo_Works"),
                "«работает» — не ожидаемый результат, а отсутствие утверждения");
            Assert.IsTrue(NameStatesNothing("Pillar_ResizeCommand_UndoRedo_Works"),
                "хвост важнее длины: подчёркиваний три, а результат не назван");

            Assert.IsFalse(NameStatesNothing("Window_Open_FrameStays_SashRotates"),
                "предмет, условие и результат названы — имя ничего не должно ломать");
            Assert.IsFalse(NameStatesNothing("ThresholdMin1mm_GapAboveIt_NoSnap"),
                "отрицательный результат — тоже результат, а не пустой хвост");
        }

        [Test]
        public void TheAssertRule_SeesAMissingMessage_AndAStringInsideTheCondition()
        {
            Assert.AreEqual(1, AssertsWithoutMessage("Assert.AreEqual(2, x);").Count,
                "ассерт без третьего аргумента — молчащий");
            Assert.AreEqual(0, AssertsWithoutMessage("Assert.AreEqual(2, x, \"почему\");").Count,
                "третий аргумент-строка и есть сообщение");
            Assert.AreEqual(0, AssertsWithoutMessage("Assert.IsTrue(ok,\n    $\"почему {x}\");").Count,
                "интерполяция и перенос строки — тоже сообщение");

            Assert.AreEqual(1, AssertsWithoutMessage("Assert.IsTrue(s.Contains(\"x\"));").Count,
                "кавычка ВНУТРИ условия — не сообщение: последний аргумент должен ИМ начинаться");
            Assert.AreEqual(0, AssertsWithoutMessage("Assert.Fail(\"так нельзя\");").Count,
                "у Fail первый аргумент и есть сообщение");
            Assert.AreEqual(1, AssertsWithoutMessage("Assert.IsTrue(f(a, b));").Count,
                "запятая во вложенном вызове не делает второго аргумента");

            Assert.AreEqual(0, AssertsWithoutMessage("Assert.IsTrue(ok, \"нельзя Assert.AreEqual(1, x)\");").Count,
                "ассерт, ПРОЦИТИРОВАННЫЙ в чужом сообщении, не нарушение: без маски "
                + "литералов сторож считает нарушителями собственные примеры — "
                + "на этом он и попался, когда насчитал 7 в самом себе");

            StringAssert.AreEqualIgnoringCase("var s = " + new string(' ', 15) + ";",
                MaskLiteralsAndComments("var s = \"Assert.Fail()\";"),
                "маска обязана сохранять ДЛИНУ: позиции совпадают с оригиналом, "
                + "по которому потом режутся аргументы");
        }

        [Test]
        public void TheScans_ActuallySeeTheTestTree()
        {
            var found = NamesThatStateNothing();
            CollectionAssert.Contains(found.Keys, "EditMode",
                "скан не видит дерева тестов — по пустому пути он зеленеет, ничего не проверив");
            CollectionAssert.Contains(found.Keys, "EditMode/Geometry",
                "быстрый путь обязан попадать в скан: именно его потолок стоит на нуле");
            CollectionAssert.Contains(found.Keys, "PlayMode",
                "PlayMode лежит вне EditMode и теряется при наивном обходе");

            var names = TestMethodNames(File.ReadAllLines(
                Path.Combine(RepoPaths.Subdir("Assets", "Tests", "EditMode", "Geometry"),
                    "CommentRatchetTests.cs"))).ToList();
            CollectionAssert.Contains(names, "TheScan_ActuallySeesTheSource",
                "сканер имён не нашёл известного теста — он читает не то, что думает");
            Assert.AreEqual(4, names.Count,
                "в CommentRatchetTests ровно четыре [Test]: сканер не должен ни терять их, "
                + "ни считать вспомогательные методы");
        }

        [Test]
        public void EveryBudgetLine_NamesSomethingThatStillExists()
        {
            var groups = NamesThatStateNothing().Keys;
            foreach (var (group, _) in NameBudgets)
                CollectionAssert.Contains(groups, group,
                    "потолок для " + group + " пережил свой каталог: запись начнёт молча "
                    + "освобождать следующий каталог с этим именем");

            var geometry = RepoPaths.Subdir("Assets", "Tests", "EditMode", "Geometry");
            foreach (var (file, _) in SilentAssertBudgets)
                Assert.IsTrue(File.Exists(Path.Combine(geometry, file)),
                    "потолок для " + file + " пережил свой файл: запись начнёт молча "
                    + "освобождать следующий файл, занявший это имя");
        }
    }
}
