using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож: правило «высота фиксирована» не выводится из ТИПА дивана.
    ///
    /// Вопрос задают панель размеров, ручки растяжки и проволочный отказ edit_elements; каждый
    /// когда-то спрашивал <c>is SofaElement</c> сам. Шестой тип с фиксированной высотой
    /// забыл бы кого-нибудь из них. Теперь ответ один: <c>FixedSize.IsHeightFixed</c>, признак на
    /// классе (<c>IFixedHeightElement</c>). Поэтому проверка типа дивана разрешена только там,
    /// где она означает «это диван» по другой причине, и каждое такое место названо с причиной.
    /// Новый файл с <c>is SofaElement</c> этот тест краснит: либо вопрос на самом деле про высоту
    /// и идёт через <c>FixedSize</c>, либо файл добавляется сюда сознательно.
    ///
    /// <c>SofaLayout.Normalise</c> (Geometry, чистая) спрашивает не тип, а геометрию: она держит
    /// <c>OverallHeightMM</c> как значение, из которого раскладка строит диван. Это нижняя
    /// гарантия данных (сохранённый или собранный вручную размер), а не правило интерфейса:
    /// <c>IFixedHeightElement</c> говорит UI и MCP «не предлагай и не принимай высоту», Normalise
    /// не даёт высоте разойтись с раскладкой, даже если её всё же передали.</summary>
    public class SofaHeightRuleSingleReaderTests
    {
        private static readonly Regex SofaTypeCheck =
            new Regex(@"\b(?:is|as|is not)\s+SofaElement\b");

        private static readonly (string file, string why)[] IdentityUses =
        {
            ("ElementSelector.cs", "имя вида «sofa» для выборки"),
            ("ElementDuplicators.cs", "копия дивана несёт стадию и подушки"),
            ("McpCommandHandler.Info.cs", "ответ cycle_open_state называет стадию раскладки"),
            ("ElementCapture.cs", "запись полей дивана в сохранение"),
            ("ElementRestorers.cs", "чтение полей дивана из сохранения"),
            ("ElementFacets.cs", "грань инспектора «диван»"),
            ("SofaFieldsEditor.cs", "редактор, который обрабатывает именно диван"),
        };

        private static readonly (string file, string why)[] HeightReaders =
        {
            ("Snap/ResizeHandleManager.cs", "ручки растяжки по высоте"),
            ("UI/ContextMenuSizeSection.cs", "поле высоты в панели размеров"),
            ("MCP/EditFieldRules.cs", "отказ edit_elements по height"),
        };

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

        [Test]
        public void SofaTypeChecks_LiveOnlyWhereTheTypeItselfIsTheQuestion_NotTheHeightRule()
        {
            var allowed = new HashSet<string>();
            foreach (var (file, _) in IdentityUses) allowed.Add(file);

            var offenders = new List<string>();
            foreach (var file in SourceCorpus.Files(ScriptsDir()))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("Sofa") || allowed.Contains(name)) continue;
                if (!SofaTypeCheck.IsMatch(SourceCorpus.Text(file))) continue;
                offenders.Add(file.Substring(ScriptsDir().Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/'));
            }

            Assert.IsEmpty(offenders,
                "высоту «фиксирована» решает FixedSize.IsHeightFixed (маркер IFixedHeightElement), "
                + "а не is SofaElement; если проверка типа здесь не про высоту, внеси файл в "
                + "IdentityUses с причиной. Нарушители: " + string.Join(", ", offenders));
        }

        [Test]
        public void EveryHeightReader_AsksTheOnePredicate()
        {
            foreach (var (path, why) in HeightReaders)
            {
                var full = Path.Combine(ScriptsDir(), "Core", path.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(full), $"читатель переехал: {path}");
                StringAssert.Contains("IsHeightFixed", SourceCorpus.Text(full),
                    $"{path}: {why} - вопрос обязан идти через FixedSize.IsHeightFixed");
            }
        }
    }
}
