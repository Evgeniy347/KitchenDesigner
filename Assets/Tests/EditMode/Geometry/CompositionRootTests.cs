using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    public class CompositionRootTests
    {
        private static readonly (string file, string why)[] MayConstructAnImplementation =
        {
            ("DefaultGameServices.cs",
                "композиционный корень: единственное место, которое знает, КАКИЕ реализации "
                + "поднимаются в Unity. Ради него конструирование и вынесено из GameContext"),
            ("CommandStack.cs",
                "статический фасад: ленивый _fallback на случай, когда GameContext не поднят "
                + "(EditMode-тест без Bootstrap)"),
            ("ElementFactory.cs", "тот же ленивый _fallback статического фасада"),
            ("GroupManager.cs", "тот же ленивый _fallback статического фасада"),
            ("PartRegistry.cs", "тот же ленивый _fallback статического фасада"),
            ("SaveLoadManager.cs", "тот же ленивый _fallback статического фасада"),
        };

        /// <summary>Квалификатор перед именем необязателен: тот же сторож соседнего
        /// правила (<see cref="LayerDependencyDirectionTests"/>) обходили именно полным
        /// именем типа вместо using, и здесь дыра была та же — <c>new</c> плюс
        /// <c>KitchenDesigner.Core.Infrastructure.PartRegistryInstance(</c> проходило
        /// мимо, потому что сразу после <c>new</c> стоит не имя класса, а начало
        /// пространства имён.</summary>
        private const string ConstructionPattern =
            @"new\s+(?:[A-Za-z_]\w*\s*\.\s*)*[A-Z]\w*Instance\s*\(";

        private static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        private static List<string> ConstructionsIn(string file)
        {
            var hits = new List<string>();
            var lines = File.ReadAllLines(file);
            var code = new List<string>(SourceLines.CodeOnly(lines));
            for (int i = 0; i < code.Count; i++)
                if (Regex.IsMatch(code[i], ConstructionPattern))
                    hits.Add(Path.GetFileName(file) + ":" + (i + 1) + "\n    " + lines[i].Trim());
            return hits;
        }

        private static Dictionary<string, List<string>> ConstructionsByFile()
        {
            var byFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories))
            {
                var hits = ConstructionsIn(file);
                if (hits.Count > 0) byFile[Path.GetFileName(file)] = hits;
            }
            return byFile;
        }

        private static bool IsAllowed(string fileName)
        {
            foreach (var (file, _) in MayConstructAnImplementation)
                if (string.Equals(file, fileName, StringComparison.Ordinal)) return true;
            return false;
        }

        [Test]
        public void OnlyTheCompositionRootAndTheStaticFacades_ConstructAServiceImplementation()
        {
            var offenders = new List<string>();
            foreach (var pair in ConstructionsByFile())
                if (!IsAllowed(pair.Key))
                    offenders.Add(string.Join("\n", pair.Value));

            Assert.IsEmpty(offenders,
                "конкретные *Instance поднимает ОДИН композиционный корень, живущий в Unity. "
                + "Класс, который конструирует их сам, тянет за собой весь слой сцены и "
                + "перестаёт собираться вторым проходом под dotnet. Найдено:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void GameContext_ConstructsNothing_SoItOnlyDependsOnTheInterfaces()
        {
            var path = Path.Combine(CoreDir(), "Infrastructure", "GameContext.cs");
            Assume.That(File.Exists(path), Is.True,
                "сторож обязан читать настоящий файл: по несуществующему пути он зеленеет, "
                + "ничего не проверив");

            CollectionAssert.IsEmpty(ConstructionsIn(path),
                "GameContext — держатель услуг, а не их создатель. Пока он звал "
                + "PartRegistryInstance и остальных сам, «кто именно поднимается» было "
                + "зашито в него, и подменить набор можно было только правкой этого файла");
            Assert.IsFalse(IsAllowed("GameContext.cs"),
                "и в список исключений он попасть не вправе: запись в этом списке вернула "
                + "бы конструирование обратно молча");
        }

        [Test]
        public void EveryAllowedFile_StillExists_AndExplainsWhy()
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories))
                found.Add(Path.GetFileName(file));

            foreach (var (file, why) in MayConstructAnImplementation)
            {
                Assert.IsTrue(found.Contains(file),
                    "исключение для " + file + " пережило свой файл: запись начнёт молча "
                    + "освобождать следующий файл с этим именем");
                Assert.IsNotEmpty(why,
                    "исключение без причины через полгода не отличить от недосмотра: " + file);
            }
        }

        [Test]
        public void TheScan_SeesTheCompositionRoot_AndTellsAConstructionFromAMention()
        {
            var byFile = ConstructionsByFile();
            CollectionAssert.Contains(byFile.Keys, "DefaultGameServices.cs",
                "скан обязан видеть сам композиционный корень — пустой результат означает "
                + "сломанный скан, а не вычищенное ядро");
            Assert.GreaterOrEqual(byFile["DefaultGameServices.cs"].Count, 5,
                "корень поднимает все пять услуг разом");

            Assert.IsTrue(Regex.IsMatch("new PartRegistryInstance()", ConstructionPattern),
                "конструирование — основная форма, которую ловит сторож");
            Assert.IsFalse(Regex.IsMatch("PartRegistryInstance registry = Get();", ConstructionPattern),
                "упоминание типа в объявлении — не конструирование");
            Assert.IsFalse(Regex.IsMatch("(PartRegistryInstance)GameContext.Services!.PartRegistry",
                ConstructionPattern), "приведение типа — тем более");

            Assert.IsTrue(Regex.IsMatch(
                    "new KitchenDesigner.Core.Infrastructure.PartRegistryInstance()",
                    ConstructionPattern),
                "полное имя — то же конструирование; именно этой записью обходят сторожей");
            Assert.IsFalse(Regex.IsMatch("new List<PartRegistryInstance>()", ConstructionPattern),
                "коллекция ИЗ услуг услугой не является — иначе правило станет шумом");
        }

        [Test]
        public void TheScan_IgnoresAConstructionInACommentOrAString()
        {
            var commented = new List<string>(SourceLines.CodeOnly(
                new[] { "        // new PartRegistryInstance();" }));
            Assert.IsFalse(Regex.IsMatch(commented[0], ConstructionPattern),
                "закомментированное конструирование нарушением не является");

            var quoted = new List<string>(SourceLines.CodeOnly(
                new[] { "        var s = \"new PartRegistryInstance()\";" }));
            Assert.IsFalse(Regex.IsMatch(quoted[0], ConstructionPattern),
                "и текст в строковом литерале тоже — иначе сторож краснеет на собственных "
                + "примерах");
        }
    }
}
