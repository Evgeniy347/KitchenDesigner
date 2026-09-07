using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Правило «у центрующейся детали в отборе участвуют только грани оси
    /// крепления» уже один раз жило в двух местах: `SnapCandidateCollector` его
    /// знал, `SnapSystem.Diagnose` — нет, и свип получал 78 ложных
    /// MOVE-COMPETITION, а MCP-инструмент `snap_diagnose` отвечал «прилипнет» про
    /// пары, к которым снэп никогда не применится.
    ///
    /// AGENTS.md → «Snap subsystem — two implementations, one geometry» говорит про
    /// это прямым текстом: две реализации одного правила расходятся. Поэтому правило
    /// живёт в `SnapFacePairRules`, а всё остальное зовёт его. Сторож ниже краснеет,
    /// когда появляется ТРЕТЬЕ место, знающее правило по-своему: он ищет сами
    /// приметы правила — `CentresOnTarget`, `MountNormal`, `MountEdgeDetentUnits` —
    /// по всему `Assets/Scripts/Core`, а не список известных нарушителей, который
    /// устаревает на следующем файле.
    ///
    /// Список разрешённых файлов — с причиной у каждого: без причины через полгода
    /// не отличить осознанное исключение от недосмотра.</summary>
    public class SnapMountRuleSingleSourceTests
    {
        private static readonly Regex MountRule =
            new Regex(@"\b(CentresOnTarget|MountNormal|MountEdgeDetentUnits)");

        private static readonly (string file, string why)[] Allowed =
        {
            ("ElementGeometry.cs", "снимок объявляет ось крепления как ДАННЫЕ — решений не принимает"),
            ("ElementGeometryExtensions.cs", "единственный переводчик сцены в снимок: тут решается, у кого ось крепления вообще есть"),
            ("SnapFacePairRules.cs", "здесь правило и живёт — допуск пары к отбору"),
            ("SnapMountSeat.cs", "детент посадки: вторая половина того же правила, отдельным именем"),
        };

        private static string CoreSourceDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        private static string[] Sources() =>
            Directory.GetFiles(CoreSourceDir(), "*.cs", SearchOption.AllDirectories);

        [Test]
        public void TheScan_SeesTheCore_AndIsNotSilentlyEmpty()
        {
            Assert.Greater(Sources().Length, 100,
                "обход ушёл мимо каталога: пустой список файлов делает сторожа вечно "
                + "зелёным и ничего не проверяющим");
        }

        [Test]
        public void TheMountAxisRule_IsNamed_OnlyByTheFilesAllowedToKnowIt()
        {
            var offenders = new List<string>();
            foreach (var path in Sources())
            {
                string name = Path.GetFileName(path) ?? string.Empty;
                if (Allowed.Any(a => a.file == name)) continue;
                if (!MountRule.IsMatch(File.ReadAllText(path))) continue;
                offenders.Add(name);
            }

            Assert.IsEmpty(offenders,
                "правило про ось крепления описано ещё раз в: " + string.Join(", ", offenders)
                + ". Оно уже расходилось между отбором и диагностикой; зовите "
                + "SnapFacePairRules.RoleOf / SnapPairOffer.For, а не повторяйте условие. "
                + "Если файл действительно обязан его называть — впишите его в Allowed "
                + "вместе с причиной, и пусть решение будет видно.");
        }

        [Test]
        public void EveryAllowedEntry_NamesAFile_ThatStillExists()
        {
            var names = new HashSet<string>(
                Sources().Select(p => Path.GetFileName(p) ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var gone = Allowed.Where(a => !names.Contains(a.file)).Select(a => a.file).ToList();

            Assert.IsEmpty(gone,
                "запись пережила свой файл и теперь молча освобождает следующий, занявший "
                + "это имя: " + string.Join(", ", gone));
        }

        [Test]
        public void EveryAllowedFile_ActuallyNamesTheRule_SoNoPermissionIsACover()
        {
            var silent = new List<string>();
            foreach (var (file, _) in Allowed)
            {
                string? path = Sources().FirstOrDefault(p => Path.GetFileName(p) == file);
                if (path == null) continue;
                if (!MountRule.IsMatch(File.ReadAllText(path))) silent.Add(file);
            }

            Assert.IsEmpty(silent,
                "файл разрешён называть правило, но не называет его — разрешение стало "
                + "прикрытием для следующего, кто впишет туда своё условие: "
                + string.Join(", ", silent));
        }
    }
}
