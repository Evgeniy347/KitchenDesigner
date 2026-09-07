using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Страж механизма «выключенная кнопка гаснет ЦЕЛИКОМ»
    /// (docs/UI-GUIDELINES.md §9). Гашение подписи живёт в UIButton, а UIButton
    /// на кнопку вешает только UIFactory — значит правило держится ровно до тех
    /// пор, пока кнопок мимо фабрики нет.
    ///
    /// Поэтому правило записано МЕХАНИЗМОМ, а не перечнем кнопок: список
    /// «тулбар, сайдбар, WideButton» устаревает на следующей панели, а этот скан
    /// краснеет на любой новой кнопке, собранной в обход. Поведение самого
    /// механизма проверяет DisabledButtonLabelTests.</summary>
    public class UiButtonFactoryTests
    {
        private const string FactoryFile = "UIFactory.cs";

        private static readonly Regex RawButton =
            new Regex(@"AddComponent\s*<\s*Button\s*>", RegexOptions.Compiled);

        /// <summary>Кнопки без подписи, которым гасить нечего, и гашение которым
        /// вредно. Причина обязательна: без неё через полгода не отличить
        /// осознанное исключение от недосмотра. Существование каждого файла
        /// проверяет <see cref="EveryExemption_NamesAFileThatStillExists"/>.</summary>
        private static readonly (string file, string why)[] Exempt =
        {
            ("ContextMenuEdgeSection.cs",
                "полоса-торец: цвет полосы НЕСЁТ состояние кромки (авто/есть/убрать), "
                + "и гашение перекрасило бы смысл, а не подпись. Кнопка без текста, "
                + "transition = None, выключенной не бывает"),
            ("MultiSelectDropdown.cs",
                "невидимая ловушка кликов мимо списка (alpha 0.01): ни подписи, ни иконки"),
        };

        private static string UiDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "UI");

        private static Dictionary<string, int> RawButtonsByFile()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(UiDir(), "*.cs", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name == FactoryFile) continue;
                int hits = SourceLines.WithoutComments(File.ReadAllLines(file))
                    .Sum(line => RawButton.Matches(line).Count);
                if (hits > 0) counts[name] = hits;
            }
            return counts;
        }

        [Test]
        public void EveryCaptionedButton_IsBuiltByUIFactory_SoItCannotSkipTheDimming()
        {
            var exempt = new HashSet<string>(Exempt.Select(e => e.file), StringComparer.Ordinal);
            var offenders = RawButtonsByFile()
                .Where(pair => !exempt.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ": " + pair.Value)
                .ToList();

            Assert.IsEmpty(offenders,
                "Правило: выключенная кнопка гаснет целиком — и фон, и подпись "
                + "(docs/UI-GUIDELINES.md §9). Красить подпись руками не надо и нельзя: "
                + "это делает UIButton, а вешает его UIFactory.CreateButton / "
                + "CreateIconButton. Кнопка, собранная через AddComponent<Button>(), "
                + "механизма не получает — её подпись останется яркой у неработающей "
                + "кнопки, и человек будет жать в неё снова и снова. Собирайте кнопку "
                + "фабрикой; если она и правда без подписи (полоса, ловушка кликов) — "
                + "впишите файл в Exempt С ПРИЧИНОЙ. Мимо фабрики: "
                + string.Join(" | ", offenders));
        }

        [Test]
        public void EveryExemption_NamesAFileThatStillExists()
        {
            foreach (var (file, why) in Exempt)
            {
                Assert.IsTrue(File.Exists(Path.Combine(UiDir(), file)),
                    "исключение пережило свой файл: «" + file + "» (" + why + ") больше нет, "
                    + "и запись начнёт молча освобождать следующий файл с тем же именем");
            }
        }

        [Test]
        public void TheScan_SeesTheUiLayer_AndCountsARawButton()
        {
            var files = Directory.GetFiles(UiDir(), "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 20,
                "скан по несуществующему пути зеленеет, ничего не проверив: слой UI обязан "
                + "найтись и быть непустым");
            Assert.IsTrue(files.Any(f => Path.GetFileName(f) == FactoryFile),
                "UIFactory.cs обязан лежать в сканируемом каталоге — иначе исключение для "
                + "него бессмысленно, а скан смотрит не туда");

            Assert.AreEqual(1,
                RawButton.Matches("var b = go.AddComponent<Button>();").Count,
                "прямая сборка кнопки — это и есть то, что ищем");
            Assert.AreEqual(0,
                RawButton.Matches("var b = go.AddComponent<UIButton>();").Count,
                "кнопка фабрики не должна попадать в улов: UIButton и есть механизм");
            Assert.AreEqual(0,
                SourceLines.WithoutComments(new[] { "// раньше тут был AddComponent<Button>()" })
                    .Sum(line => RawButton.Matches(line).Count),
                "в комментарии сборку упоминают, а не делают");
        }
    }
}
