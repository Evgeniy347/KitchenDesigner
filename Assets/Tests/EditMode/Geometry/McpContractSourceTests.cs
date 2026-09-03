using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    public class McpContractSourceTests
    {
        private const string RecordDeclaration = "public record";
        private const string InitOnlySetter = "init;";

        private static string ContractDir() =>
            RepoPaths.Subdir("Assets", "Scripts", "Core", "MCP", "Contract");

        private static IReadOnlyList<string> ContractFiles() =>
            Directory.GetFiles(ContractDir(), "*.cs", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal).ToList();

        [Test]
        public void TheScan_SeesTheContractSources_AndTheRegistryAmongThem()
        {
            var files = ContractFiles().Select(Path.GetFileName).ToList();
            CollectionAssert.Contains(files, "McpToolRegistry.cs",
                "скан контракта смотрит в пустоту: по несуществующему каталогу он зеленеет, "
                + "ничего не проверив");
            Assert.GreaterOrEqual(files.Count, 4,
                "в Contract/ должно лежать больше одного файла — иначе скан ослеп");
        }

        [Test]
        public void ContractSources_ImportOnlySystem_SoTheGeneratorCompilesTheSameFiles()
        {
            var offenders = new List<string>();

            foreach (var file in ContractFiles())
                foreach (var line in SourceLines.WithoutComments(File.ReadAllLines(file)))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("using ", StringComparison.Ordinal)) continue;
                    var imported = trimmed.Substring("using ".Length).TrimEnd(';').Trim();
                    if (imported == "System" || imported.StartsWith("System.", StringComparison.Ordinal))
                        continue;
                    offenders.Add(Path.GetFileName(file) + ": " + trimmed);
                }

            CollectionAssert.IsEmpty(offenders,
                "Contract/ компилируют ДВА компилятора: Unity (KitchenDesigner.Runtime) и "
                + "dotnet (geometry/pure/Pure.csproj линкует ТЕ ЖЕ файлы, чтобы McpJsonSchema и "
                + "его тесты шли по быстрому пути; то же делает tools/McpContractGen, пока он "
                + "жив). Любой using вне System — UnityEngine, Newtonsoft — ломает вторую "
                + "сборку, и контракт перестаёт быть одним источником правды. Нарушители:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void ContractSources_StayOnConservativeCSharp_ThatUnityMonoAlsoCompiles()
        {
            var offenders = new List<string>();

            foreach (var file in ContractFiles())
            {
                var lines = SourceLines.WithoutComments(File.ReadAllLines(file)).ToList();
                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    var where = Path.GetFileName(file) + ":" + (i + 1) + " " + trimmed;

                    if (trimmed.StartsWith("namespace ", StringComparison.Ordinal)
                        && trimmed.EndsWith(";", StringComparison.Ordinal))
                        offenders.Add(where);
                    if (trimmed.Contains(RecordDeclaration, StringComparison.Ordinal))
                        offenders.Add(where);
                    if (trimmed.EndsWith(InitOnlySetter, StringComparison.Ordinal))
                        offenders.Add(where);
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "Контракт читают Unity-Mono и .NET сервера: блочные namespace, публичные поля, "
                + "никаких record и init-only. Нарушители:\n" + string.Join("\n", offenders));
        }
    }
}
