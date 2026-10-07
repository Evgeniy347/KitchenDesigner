using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Подложки (<c>BasePlate</c>) больше нет: земля пустой сцены — <c>GroundQuad</c>,
    /// якорь валидации — <c>ImpliedGround</c>. Скорлупа <c>Elements/BasePlate.cs</c> и вызовы
    /// устаревших MCP-инструментов get_floor_info/resize_floor удалены, исключений у сторожа
    /// больше нет: имя не должно просочиться обратно НИГДЕ в продакшен-коде.</summary>
    public class BasePlateIsGoneTests
    {
        private const string Word = "BasePlate";

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        private static List<string> SourcesNaming(IEnumerable<string> files)
        {
            var hits = new List<string>();
            foreach (var file in files)
            {
                var lines = SourceCorpus.Lines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains(Word)) hits.Add(Path.GetFileName(file) + ":" + (i + 1));
            }
            return hits;
        }

        [Test]
        public void ProductionSources_DoNotNameTheBasePlate_Anywhere()
        {
            var hits = SourcesNaming(SourceCorpus.Files(ScriptsDir()));

            Assert.IsEmpty(hits,
                "Подложка удалена: землю рисует GroundQuad, опору валидации даёт ImpliedGround. "
                + "Имя BasePlate в этих файлах — возврат к якорю-элементу, который загораживал лучи и "
                + "попадал в снэп, выбор и иерархию: " + string.Join(", ", hits));
        }

        [Test]
        public void TheScan_ActuallySeesTheSources_SoAnEmptyResultMeansSomething()
        {
            Assert.Greater(SourceCorpus.Files(ScriptsDir()).Length, 100,
                "сканер не нашёл исходники — пустой результат ничего бы не значил");

            var thisFile = Path.Combine(RepoPaths.Subdir("Assets", "Tests", "EditMode", "Geometry"),
                nameof(BasePlateIsGoneTests) + ".cs");
            Assert.IsNotEmpty(SourcesNaming(new[] { thisFile }),
                "сканер не узнаёт слово в файле, где оно точно есть, — он бы прошёл на любом дереве");
        }
    }
}
