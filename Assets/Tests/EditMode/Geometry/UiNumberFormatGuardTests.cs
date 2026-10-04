using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    // docs/UI-GUIDELINES.md D3: один десятичный знак на продукт — из языка интерфейса, через
    // один форматтер NumberFormat (Core/Pure/Localization). Аудит 2026-10-04 нашёл в одной панели «1.0»
    // (кромка, InvariantCulture), «26.8» (Ø трубы, InvariantCulture) и «0,0» (угол, ToString("F1")
    // по культуре МАШИНЫ) — три правила одной величины. Сторож не даёт завести четвёртое:
    // дробное число в Core/UI форматирует только NumberFormat.
    public class UiNumberFormatGuardTests
    {
        private static readonly Regex DecimalFormatting = new Regex(
            @"ToString\(\s*""(?:[FfNnEe][1-9]|[^""]*[0#]\.[0#])"
            + @"|\{[^{}""]*:(?:[FfNn][1-9]|[0#]*\.[0#]+)[^}]*\}");

        private static readonly (string file, string why)[] Allowed =
        {
        };

        private static string UiDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "UI");

        public static int HitsIn(IEnumerable<string> lines) =>
            SourceLines.WithoutComments(lines).Sum(line => DecimalFormatting.Matches(line).Count);

        private static Dictionary<string, int> HitsByFile()
        {
            var hits = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var file in SourceCorpus.Files(UiDir()))
                hits[Path.GetFileName(file)] = HitsIn(SourceCorpus.Lines(file));
            return hits;
        }

        [Test]
        public void UiSources_FormatFractionsOnlyThroughNumberFormat()
        {
            var allowed = new HashSet<string>(Allowed.Select(a => a.file), StringComparer.Ordinal);
            var offenders = HitsByFile()
                .Where(p => p.Value > 0 && !allowed.Contains(p.Key))
                .Select(p => p.Key + ": " + p.Value)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            Assert.IsEmpty(offenders,
                "Дробное число в Core/UI форматируется мимо NumberFormat. ToString(\"F1\") берёт "
                + "десятичный знак у культуры МАШИНЫ, InvariantCulture — всегда точку; оба "
                + "расходятся с языком интерфейса. Пиши NumberFormat.Fixed (текст) или "
                + "NumberFormat.Input (поле ввода):\n" + string.Join("\n", offenders));
        }

        [Test]
        public void ThePattern_SeesEveryFormOfAFractionFormat_AndIgnoresComments()
        {
            Assert.AreEqual(1, HitsIn(new[] { "f.text = v.ToString(\"F1\");" }), "F1");
            Assert.AreEqual(1, HitsIn(new[] { "f.text = v.ToString(\"0.0\", CultureInfo.InvariantCulture);" }),
                "шаблон с точкой");
            Assert.AreEqual(1, HitsIn(new[] { "f.text = v.ToString(\"+0.0;-0.0;0.0\");" }),
                "шаблон со знаком");
            Assert.AreEqual(1, HitsIn(new[] { "f.text = $\"{v:0.##} мм\";" }), "интерполяция с шаблоном");
            Assert.AreEqual(1, HitsIn(new[] { "f.text = $\"{v:F2}\";" }), "интерполяция с F2");
            Assert.AreEqual(1, HitsIn(new[] { "f.text = string.Format(\"{0:0.0}\", v);" }), "string.Format");

            Assert.AreEqual(0, HitsIn(new[] { "f.text = NumberFormat.Fixed(v, 1);" }),
                "правильный путь не засчитывается");
            Assert.AreEqual(0, HitsIn(new[] { "f.text = v.ToString(\"F0\");" }),
                "целое без дробной части десятичного знака не несёт");
            Assert.AreEqual(0, HitsIn(new[] { "string n = $\"KitchenSpec_{DateTime.Now:yyyyMMdd_HHmmss}.csv\";" }),
                "шаблон даты — не дробное число");
            Assert.AreEqual(0, HitsIn(new[] { "// было v.ToString(\"F1\")" }), "комментарий");
        }

        [Test]
        public void TheScan_ActuallySeesTheUiLayer()
        {
            var hits = HitsByFile();
            CollectionAssert.Contains(hits.Keys, "ContextMenuUI.cs",
                "скан не видит Core/UI — греп по несуществующему пути зеленеет, ничего не проверив");
            CollectionAssert.Contains(hits.Keys, "SettingsRowFactory.cs");
            foreach (var (file, why) in Allowed)
            {
                CollectionAssert.Contains(hits.Keys, file, "исключение пережило свой файл: " + file);
                Assert.IsNotEmpty(why, "у исключения " + file + " нет причины");
            }
        }
    }
}
