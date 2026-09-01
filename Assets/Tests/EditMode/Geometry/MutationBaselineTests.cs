using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож за самими воротами по mutation score (geometry/mutation-baseline.txt).
    ///
    /// Мутационный прогон долгий, поэтому ворота стоят не здесь: порог передаётся
    /// Стрейкеру ключом --break-at, и роняет прогон он. Этот класс — за секунды —
    /// стережёт то, что иначе сгниёт молча:
    ///
    ///   * порог, отставший от замера, стрелять перестаёт. «72 при замере 96» —
    ///     это не ворота, а число на память;
    ///   * проект быстрого пути, у которого строки нет, не гейтится вообще, и
    ///     заметить это по зелёному прогону невозможно;
    ///   * скрипт, перестав читать файл, оставляет число декоративным — а файл
    ///     продолжает выглядеть источником правды.
    ///
    /// Порогов ДВА, по одному на сборку; почему их нельзя слить в один — в шапке
    /// geometry/mutation-baseline.txt.</summary>
    public class MutationBaselineTests
    {
        /// <summary>Насколько порогу позволено отстать от замера. Порог = замер,
        /// округлённый вниз, поэтому больше единицы отставание значить не может;
        /// два процента на 1039 мутантах — это два десятка выживших, то есть
        /// ровно тот запас, в который прячется потерянная проверка.</summary>
        public const double MaxSlackPercent = 1.0;

        private static string GeometryDir() => RepoPaths.Subdir("geometry");

        private static string BaselineFile() =>
            Path.Combine(GeometryDir(), "mutation-baseline.txt");

        private static string ScriptFile() =>
            Path.Combine(RepoPaths.Subdir("tools"), "mutation-test.ps1");

        public static List<(string project, double measured, int threshold)> Parse(
            IEnumerable<string> lines)
        {
            var rows = new List<(string, double, int)>();

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3)
                    throw new FormatException(
                        "строка базового уровня должна быть «<проект> <замер> <порог>»: " + line);

                rows.Add((
                    parts[0],
                    double.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture)));
            }

            return rows;
        }

        private static List<(string project, double measured, int threshold)> Baseline() =>
            Parse(File.ReadAllLines(BaselineFile()));

        /// <summary>Проекты, которые Stryker мутирует: всё под geometry/ с *.csproj,
        /// кроме тестовых. Список берётся С ДИСКА, а не из таблицы: новый проект
        /// быстрого пути обязан попасть под ворота сам, а не когда о нём вспомнят.</summary>
        private static List<string> MutatedProjects()
        {
            var projects = new List<string>();

            foreach (var dir in Directory.GetDirectories(GeometryDir()))
            {
                var name = Path.GetFileName(dir);
                if (name == null || name.EndsWith("tests", StringComparison.OrdinalIgnoreCase)) continue;
                if (Directory.GetFiles(dir, "*.csproj").Length == 0) continue;
                projects.Add(name);
            }

            return projects;
        }

        [Test]
        public void Baseline_NamesEveryProjectThatStrykerMutates()
        {
            var named = Baseline().Select(r => r.project).ToList();
            var onDisk = MutatedProjects();

            CollectionAssert.IsNotEmpty(onDisk,
                "скан не нашёл ни одного проекта под geometry/ — по пустому пути он зеленеет, "
                + "ничего не проверив");

            foreach (var project in onDisk)
                CollectionAssert.Contains(named, project,
                    "проект " + project + " мутируется, но порога у него нет: негейтящийся "
                    + "проект не отличить от зелёного, и первым это заметит регресс");

            foreach (var project in named)
                CollectionAssert.Contains(onDisk, project,
                    "порог для " + project + " пережил свой проект: строка начнёт молча "
                    + "гейтить пустоту");
        }

        [Test]
        public void EveryThreshold_StaysCloseEnoughToItsMeasurement_ToStillFire()
        {
            foreach (var (project, measured, threshold) in Baseline())
            {
                Assert.LessOrEqual((double)threshold, measured,
                    "порог " + project + " выше замера — прогон падает на месте, "
                    + "а не при регрессе");
                Assert.GreaterOrEqual((double)threshold, measured - MaxSlackPercent,
                    "порог " + project + " отстал от замера " + measured.ToString("0.00",
                        CultureInfo.InvariantCulture) + " больше чем на " + MaxSlackPercent
                    + " — в этот зазор проходит потерянная проверка, и ворота её не увидят. "
                    + "Порог = замер, округлённый вниз");
                Assert.Greater(threshold, 0,
                    "нулевой порог у " + project + " отключает ворота: у Stryker --break-at 0 "
                    + "не проверяет ничего");
            }
        }

        [Test]
        public void TheScript_TakesTheNumberFromTheFile_NotFromItself()
        {
            var script = File.ReadAllText(ScriptFile());

            StringAssert.Contains("mutation-baseline.txt", script,
                "скрипт обязан читать базовый уровень из файла: число, продублированное "
                + "в скрипте, расходится с файлом молча, и оба выглядят источником правды");

            foreach (var (project, _, threshold) in Baseline())
                StringAssert.DoesNotContain("--break-at', '" + threshold, script,
                    "порог " + project + " зашит в скрипт литералом — файл станет декорацией");
        }

        [Test]
        public void TheParser_ReadsARow_AndSkipsCommentsAndBlankLines()
        {
            var rows = Parse(new[]
            {
                "# комментарий",
                "",
                "core 72.42 72",
                "   pure\t81.00\t81   ",
            });

            Assert.AreEqual(2, rows.Count,
                "комментарии и пустые строки не строки данных");
            Assert.AreEqual("core", rows[0].project,
                "первым полем идёт имя проекта — по нему скрипт выбирает --project");
            Assert.AreEqual(72.42, rows[0].measured, 1e-9,
                "замер читается по инвариантной культуре: под русской локалью точка "
                + "иначе станет разделителем тысяч и 72.42 превратится в 7242");
            Assert.AreEqual(81, rows[1].threshold,
                "табуляция — такой же разделитель, как пробел");

            Assert.Throws<FormatException>(() => Parse(new[] { "core 72.42" }),
                "строка без порога обязана падать: молча прочитанная половина строки "
                + "оставила бы проект без ворот");
        }

        [Test]
        public void TheScan_ActuallySeesTheBaselineFile()
        {
            Assert.IsTrue(File.Exists(BaselineFile()),
                "файл базового уровня не найден — тест, читающий пустоту, зеленеет "
                + "и не проверяет ничего: " + BaselineFile());
            Assert.IsTrue(File.Exists(ScriptFile()),
                "скрипт мутационного прогона не найден: " + ScriptFile());
            CollectionAssert.IsNotEmpty(Baseline(),
                "в файле нет ни одной строки данных — ворота открыты настежь");
        }
    }
}
