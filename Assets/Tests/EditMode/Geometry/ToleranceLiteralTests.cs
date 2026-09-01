using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Храповик по зашитым допускам (CONVENTIONS.md → «Tolerance
    /// constants — MANDATORY»). Правило записано КАПСОМ и ничем не проверялось,
    /// поэтому в ядре живут и 0.99f вместо Tolerance.UpDotThreshold, и 1e-4f
    /// вместо Tolerance.EpsilonUnits, и 0.5f вместо Tolerance.ContactMm.
    ///
    /// Ловим дробный литерал, стоящий РЯДОМ со сравнением: множитель 0.5f —
    /// это половина, а сравнение с 0.5f — это порог, и порог обязан иметь имя.
    /// Строки и комментарии вырезаются: «|зазор| &lt; 0.5 мм» в тексте подсказки
    /// MCP — документация, а не код.
    ///
    /// Потолок — на файл, причина обязательна. Рост валит тест; падение валит
    /// тоже — иначе храповик отдаёт назад вычищенное.</summary>
    public class ToleranceLiteralTests
    {
        private const string Fraction = @"(?:\d*\.\d+|\d+(?:\.\d+)?[eE]-\d+)";

        /// <summary>Сравнение с дробным литералом с любой стороны. Стрелка лямбды
        /// исключена явно: «=&gt;» это не «больше».</summary>
        private static readonly string Comparison =
            @"(?<![\w.])" + Fraction + @"f?\s*(?<![=!<>])(?:<=|>=|==|!=|<|>)"
            + @"|(?<![=!<>])(?:<=|>=|==|!=|<|>)\s*-?" + Fraction + @"f?(?![\w.])";

        /// <summary>Потолок на файл + причина, почему литерал ещё не заменён на
        /// константу. Существование каждого файла проверяет
        /// <see cref="EveryBudgetLine_NamesAFileThatExists"/> — иначе запись
        /// переживёт свой файл и начнёт освобождать следующий с этим именем.</summary>
        private static readonly (string file, int ceiling, string why)[] Budgets =
        {
            ("AttachLinks.cs", 1,
                "0.01° поворота — угловой порог «связь считается нарушенной», не длина; "
                + "своей константы в Tolerance нет"),
            ("AttachMove.cs", 1, "тот же угловой порог 0.01°, вторая половина той же пары"),
            ("CameraController.cs", 15,
                "камера: шаг колеса, затухание инерции и защита от деления на dt — это не "
                + "геометрия детали, но пороги всё равно безымянные"),
            ("CeilingGeometry.cs", 5,
                "1e-4f — ровно Tolerance.EpsilonUnits, зашитый копией: прямой долг"),
            ("ConstraintValidator.cs", 2,
                "0.01f как запас к сравнению зазора в мм — просится в Tolerance"),
            ("DoorElement.cs", 3,
                "1e-12f/1e-8f перед normalize и 0.05° «поворот уже доехал»: "
                + "Tolerance.EpsilonSqr рядом, но не та величина"),
            ("ElementMover.cs", 2,
                "1e-4f и 1e-8f как защита перед normalize; Tolerance.EpsilonSqr = 1e-6f"),
            ("ElementOutline.cs", 1, "1e-6f перед построением поворота отрезка = Tolerance.EpsilonSqr"),
            ("FacadeValidator.cs", 1,
                "0.001f на скалярном произведении — порог «фасад смотрит наружу»"),
            ("GrooveMesh.cs", 1, "1e-5f при склейке совпавших координат реза = Tolerance.SnapEpsilon"),
            ("InputCapture.cs", 1, "0.01f мёртвой зоны колеса мыши — ввод, а не геометрия"),
            ("MmGrid.cs", 1,
                "0.9999f — «дробная часть не дотянула до целого мм»; порог округления, не длина"),
            ("SpatialGridRenderer.cs", 1, "1e-3f при выборе крупной линии сетки"),
            ("StatusBarUI.cs", 1, "0.5f — порог «ширина плашки изменилась заметно», пиксели UI"),
            ("ToastNotification.cs", 2, "0.15f и 0.3f — длительности анимации в секундах"),
            ("Wall.cs", 2,
                "0.01° поворота и 0.001f высоты стены; второй — сравнение с нулём в юнитах"),
            ("WallCutaway.cs", 3,
                "1e-6f/1e-8f перед normalize и -0.1f как «камера точно с той стороны»"),
            ("WindowElement.cs", 3, "копия тройки из DoorElement — оба створчатые"),
        };

        private static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        public static int ThresholdsIn(IEnumerable<string> lines) =>
            SourceLines.CodeOnly(lines).Sum(line => Regex.Matches(line, Comparison).Count);

        private static Dictionary<string, int> CountsByFile()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories))
            {
                int found = ThresholdsIn(File.ReadAllLines(file));
                if (found == 0) continue;

                var name = Path.GetFileName(file);
                counts.TryGetValue(name, out int already);
                counts[name] = already + found;
            }

            return counts;
        }

        [Test]
        public void CoreSources_TakeEveryThresholdFromANamedConstant()
        {
            var counts = CountsByFile();
            var budget = Budgets.ToDictionary(b => b.file, b => b.ceiling, StringComparer.Ordinal);
            var grew = new List<string>();
            var shrank = new List<string>();

            foreach (var pair in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int ceiling = budget.TryGetValue(pair.Key, out int c) ? c : 0;
                if (pair.Value > ceiling) grew.Add(pair.Key + ": " + pair.Value + " > " + ceiling);
            }

            foreach (var (file, ceiling, _) in Budgets)
            {
                int actual = counts.TryGetValue(file, out int a) ? a : 0;
                if (actual < ceiling) shrank.Add(file + ": " + actual + " < " + ceiling);
            }

            Assert.IsEmpty(grew,
                "Число рядом со сравнением — это ПОРОГ, и он обязан иметь имя: Tolerance.cs "
                + "для геометрии, public const на владеющем классе для алгоритмических "
                + "величин (CONVENTIONS.md → «Tolerance constants — MANDATORY»). Копия "
                + "порога расходится с оригиналом молча. Выросло:\n" + string.Join("\n", grew));

            Assert.IsEmpty(shrank,
                "Пороги названы — опусти потолок в Budgets тем же коммитом, иначе храповик "
                + "отдаст назад вычищенное:\n" + string.Join("\n", shrank));
        }

        [Test]
        public void TheCounter_SeesAThreshold_AndIgnoresAMultiplierAndProse()
        {
            Assert.AreEqual(1, ThresholdsIn(new[] { "if (gap < 0.5f) return true;" }));
            Assert.AreEqual(1, ThresholdsIn(new[] { "if (v.sqrMagnitude < 1e-8f) return;" }),
                "экспоненциальная запись — та же зашитая величина");
            Assert.AreEqual(1, ThresholdsIn(new[] { "return coverage >= 0.5f;" }),
                "порог, прикинувшийся долей: ровно этот случай прошёл мимо словесного правила");
            Assert.AreEqual(1, ThresholdsIn(new[] { "if (0.001f > delta) return;" }),
                "литерал слева от сравнения — тот же порог");

            Assert.AreEqual(0, ThresholdsIn(new[] { "var half = size * 0.5f;" }),
                "умножение на 0.5f — это половина, а не порог");
            Assert.AreEqual(0, ThresholdsIn(new[] { "Func<float, bool> f = x => 0.5f;" }),
                "стрелка лямбды — не «больше»");
            Assert.AreEqual(0, ThresholdsIn(new[] { "if (gap < Tolerance.ContactMm) return true;" }),
                "именованная константа — это и есть правильный способ");
            Assert.AreEqual(0, ThresholdsIn(new[] { "// касание: |зазор| < 0.5 мм" }),
                "в комментарии порог объясняют, а не задают");
            Assert.AreEqual(0, ThresholdsIn(new[] { "text = \"touching < 0.5 mm - not a violation\";" }),
                "текст подсказки MCP — документация, а не сравнение");
            Assert.AreEqual(0, ThresholdsIn(new[] { "if (i < 3) return;" }),
                "целые — счётчики и индексы, а не допуски");

            Assert.AreEqual(0, ThresholdsIn(new[]
                {
                    "        private const string Guide = @\"",
                    "touching          < 0.5 mm  - NOT a violation (flush contact)",
                    "\";",
                }),
                "@-строка переносится через строку: построчный сканер видит её текст как "
                + "голый код, и ровно так подсказка MCP попала в нарушители");
        }

        [Test]
        public void TheScan_ActuallySeesTheCore_AndDoesNotExemptItsGeometry()
        {
            var files = Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFileName).ToArray();
            CollectionAssert.Contains(files, "Tolerance.cs",
                "скан не видит исходников ядра — грепу по несуществующему пути нечего найти, "
                + "и он зеленеет, ничего не проверив");
            CollectionAssert.Contains(files, "SnapCore.cs");

            var listed = Budgets.Select(b => b.file).ToArray();
            CollectionAssert.DoesNotContain(listed, "SnapCore.cs",
                "снэп обязан оставаться на нуле: он и есть тот код, ради которого Tolerance "
                + "заводили");
            CollectionAssert.DoesNotContain(listed, "ValidationCore.cs");
            CollectionAssert.DoesNotContain(listed, "Tolerance.cs",
                "сама Tolerance.cs держит величины в именованных константах и сравнивает "
                + "только с ними");
        }

        [Test]
        public void EveryBudgetLine_NamesAFileThatExists()
        {
            var files = new HashSet<string>(
                Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories)
                    .Select(f => Path.GetFileName(f) ?? string.Empty),
                StringComparer.Ordinal);

            foreach (var (file, _, why) in Budgets)
            {
                Assert.IsTrue(files.Contains(file),
                    "потолок для " + file + " пережил свой файл: запись начнёт молча освобождать "
                    + "следующий файл с этим именем");
                Assert.IsNotEmpty(why, "у записи " + file + " нет причины");
            }
        }
    }
}
