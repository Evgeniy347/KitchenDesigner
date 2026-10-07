using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Подложки (<c>BasePlate</c>) больше нет: земля пустой сцены — <c>GroundQuad</c>,
    /// якорь валидации — <c>ImpliedGround</c>. Единственное, что ещё называет старый тип, —
    /// скорлупа <c>Elements/BasePlate.cs</c> и вызовы устаревших MCP-инструментов
    /// get_floor_info/resize_floor в <c>Core/MCP</c>; скорлупа живёт, пока те вызовы не
    /// удалены. Сторож не даёт имени просочиться обратно в остальной код.</summary>
    public class BasePlateIsGoneTests
    {
        private const string Word = "BasePlate";

        private static readonly string[] MayNameIt =
        {
            Path.Combine("Core", "MCP") + Path.DirectorySeparatorChar,
            Path.Combine("Core", "Elements", "BasePlate.cs"),
        };

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        private static List<string> SourcesNaming(IEnumerable<string> files, Func<string, bool> exempt)
        {
            var hits = new List<string>();
            foreach (var file in files)
            {
                if (exempt(file)) continue;
                var lines = SourceCorpus.Lines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains(Word)) hits.Add(Path.GetFileName(file) + ":" + (i + 1));
            }
            return hits;
        }

        private static bool IsExempt(string file)
        {
            foreach (var part in MayNameIt)
                if (file.Contains(part)) return true;
            return false;
        }

        [Test]
        public void ProductionSources_OutsideMcpAndTheShell_DoNotNameTheBasePlate()
        {
            var hits = SourcesNaming(SourceCorpus.Files(ScriptsDir()), IsExempt);

            Assert.IsEmpty(hits,
                "Подложка удалена: землю рисует GroundQuad, опору валидации даёт ImpliedGround. "
                + "Имя BasePlate в этих файлах — возврат к якорю-элементу, который загораживал лучи и "
                + "попадал в снэп, выбор и иерархию: " + string.Join(", ", hits));
        }

        [Test]
        public void TheScan_ActuallySeesTheShellAndTheMcpCallers_SoAnEmptyResultMeansSomething()
        {
            var all = SourcesNaming(SourceCorpus.Files(ScriptsDir()), _ => false);

            Assert.IsTrue(all.Exists(h => h.StartsWith("BasePlate.cs:", StringComparison.Ordinal)),
                "сканер не видит скорлупу — он бы прошёл, ничего не проверив");
            Assert.IsTrue(all.Exists(h => h.StartsWith("McpCommandHandler", StringComparison.Ordinal)),
                "сканер не видит MCP-вызовы подложки — исключение для Core/MCP стоит на пустом месте");
        }
    }
}
