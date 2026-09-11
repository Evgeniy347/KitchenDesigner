using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож ЕДИНСТВЕННОГО читателя состояния торца.
    ///
    /// Вопрос «есть ли на этой стороне кромка» задают четыре независимых места:
    /// спецификация (<c>SpecificationManager</c>), ответ MCP
    /// (<c>McpSpecCodec</c>), тёмная подложка торца в 3D (<c>EdgeSubstrate</c>)
    /// и покраска полос в контекстном меню. Пока каждое спрашивало
    /// <c>coverage.HasEdge(side)</c> само, добавить третье состояние значило
    /// починить четыре места и не забыть ни одного: красная сторона исчезла бы
    /// из спецификации, но осталась в 3D и в <c>get_element</c>.
    ///
    /// Правило поэтому сформулировано МЕХАНИЗМОМ, а не списком починенных
    /// случаев (<c>conventions/CORRECTNESS.md</c> → «State a rule by its
    /// MECHANISM»): ответ даёт одна функция <c>EdgeBanding.HasEdgeEffective</c>,
    /// и сырой расчёт покрытия читает только она. Этот тест не даёт появиться
    /// ПЯТОМУ читателю мимо неё.</summary>
    public class EdgeStateSingleReaderTests
    {
        /// <summary>Единственный файл продакшена, которому дозволено спрашивать
        /// сырое покрытие: в нём и живёт разрешающая функция.</summary>
        private const string TheOnlyReader = "EdgeBanding.cs";

        /// <summary>Кто обязан спрашивать через неё. Список — не определение
        /// правила, а его вторая половина: правило запрещает сырой вызов всем,
        /// а здесь перечислены места, где ответ на вопрос ТОЧНО нужен, и
        /// молчаливая потеря вызова означала бы, что читатель отвалился.</summary>
        private static readonly (string path, string why)[] Consumers =
        {
            ("Infrastructure/SpecificationManager.cs", "четыре колонки кромки в спецификации — это и есть раскрой"),
            ("MCP/McpSpecCodec.cs", "поле edges в ответе get_elements"),
            ("Materials/EdgeSubstrate.cs", "тёмная подложка на торце без кромки в 3D"),
            ("UI/ContextMenuEdgeSection.cs", "покраска и заливка полос на схеме кромок"),
        };

        private static readonly Regex RawCoverageCall =
            new Regex(@"(?<!EdgeStates)\.(?:HasEdge|IsCovered)\s*\(");

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

        [Test]
        public void RawEdgeCoverage_IsReadByExactlyOneProductionFile()
        {
            var offenders = new List<string>();
            foreach (var file in SourceCorpus.Files(ScriptsDir()))
            {
                if (Path.GetFileName(file) == TheOnlyReader) continue;
                var text = SourceCorpus.Text(file);
                if (!RawCoverageCall.IsMatch(text)) continue;
                offenders.Add(file.Substring(ScriptsDir().Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/'));
            }

            Assert.IsEmpty(offenders,
                "сырое покрытие торца (coverage.HasEdge / IsCovered) читает только "
                + TheOnlyReader + "; остальным отвечает EdgeBanding.HasEdgeEffective, "
                + "иначе явные состояния «принудительно есть» и «убрать» видит не каждый "
                + "потребитель — а спецификация, 3D и MCP обязаны говорить одно и то же. "
                + "Нарушители: " + string.Join(", ", offenders));
        }

        [Test]
        public void EveryKnownConsumer_AsksThroughTheOneFunction()
        {
            foreach (var (path, why) in Consumers)
            {
                var full = Path.Combine(ScriptsDir(), "Core", path.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(full), $"потребитель переехал: {path}");
                StringAssert.Contains("HasEdgeEffective", SourceCorpus.Text(full),
                    $"{path}: {why} — вопрос обязан идти через EdgeBanding.HasEdgeEffective");
            }
        }
    }
}
