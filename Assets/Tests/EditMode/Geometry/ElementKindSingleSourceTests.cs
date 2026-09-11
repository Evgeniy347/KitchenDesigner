using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож правила «роль элемента решается в ОДНОМ месте»
    /// (AGENTS.md → «Validation», CONVENTIONS.md → «Element type checks live in ONE
    /// place per layer»). Правило записано давно и проверено руками, но до сих пор
    /// держалось только на памяти — единственное архитектурное правило такой формы
    /// без сторожа.
    ///
    /// Ядро <c>ValidationCore</c> обязано исполняться без Unity, поэтому вопрос «это
    /// пол / мойка / ящик?» задаётся ровно один раз, на границе сцены, и уезжает в
    /// ядро флагами <see cref="ElementKind"/>. Стоит второму месту начать
    /// СОБИРАТЬ эти флаги — и роли расходятся: два ответа на один вопрос,
    /// ровно та же болезнь, что дала <c>public new MaterialId</c> два поля декора.
    ///
    /// Сторож проверяет три вещи: кто вообще НАЗЫВАЕТ ElementKind, кто его
    /// СОБИРАЕТ (присваивание и <c>|=</c>) и кто конструирует
    /// <c>ValidationElement</c> — единственную дверь, через которую флаги входят в
    /// ядро.
    ///
    /// Тест намеренно не трогает сцену: он читает ИСХОДНИКИ, поэтому спокойно
    /// живёт в каталоге, который собирается вторым проходом под dotnet.</summary>
    public class ElementKindSingleSourceTests
    {
        private const string Builder = "ValidationSnapshot.cs";

        /// <summary>Кому позволено НАЗЫВАТЬ ElementKind, с причиной у каждого.
        /// Собирать флаги при этом вправе только <see cref="Builder"/>.</summary>
        private static readonly (string file, string why)[] MayMention =
        {
            ("ElementKind.cs", "объявление самого перечисления"),
            ("ValidationElement.cs", "снимок элемента для ядра: хранит флаги и отвечает на Is()"),
            ("ValidationCore.cs", "потребитель: читает флаги через Is(), не решая, чей элемент"),
            (Builder, "ЕДИНСТВЕННОЕ место, где тип элемента превращается в роль"),
        };

        private static readonly string AssignmentPattern = @"(?<![=!<>])=(?!=)\s*ElementKind\s*\.";

        private static readonly string OrAssignmentPattern = @"\|=\s*ElementKind\s*\.";

        private static readonly string ConstructionPattern = @"\bnew\s+ValidationElement\s*\(";

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

        private static string[] SourceFiles() =>
            SourceCorpus.Files(ScriptsDir());

        private static bool MayMentionIt(string fileName)
        {
            foreach (var (file, _) in MayMention)
                if (string.Equals(file, fileName, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsComment(string line)
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*");
        }

        private static List<string> Hits(string pattern, Func<string, bool> exempt)
        {
            var hits = new List<string>();

            foreach (var file in SourceFiles())
            {
                var name = Path.GetFileName(file) ?? string.Empty;
                if (exempt(name)) continue;

                var lines = SourceCorpus.Lines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsComment(lines[i])) continue;
                    if (Regex.IsMatch(lines[i], pattern))
                        hits.Add(name + ":" + (i + 1) + "\n    " + lines[i].Trim());
                }
            }

            return hits;
        }

        [Test]
        public void ElementKind_IsBuilt_OnlyInValidationSnapshot()
        {
            var hits = Hits(AssignmentPattern, n => n == Builder);
            hits.AddRange(Hits(OrAssignmentPattern, n => n == Builder));

            Assert.IsEmpty(hits,
                "Роль элемента собирается ровно в одном месте — " + Builder + ".KindOf. Второе "
                + "место, собирающее ElementKind, означает два независимых ответа на вопрос «это "
                + "пол / мойка / ящик?», которые разъедутся молча. Найдено:\n"
                + string.Join("\n", hits));
        }

        [Test]
        public void ElementKind_IsNamed_OnlyByItsOwnerItsSnapshotAndItsConsumer()
        {
            var hits = Hits(@"\bElementKind\b", MayMentionIt);

            Assert.IsEmpty(hits,
                "ElementKind — валюта границы «сцена → ядро», а не общий словарь ролей по всему "
                + "проекту: спросите фасет, интерфейс или редактор своего слоя. Расширять список "
                + "разрешённых — осознанная правка этого теста. Найдено:\n"
                + string.Join("\n", hits));
        }

        [Test]
        public void ValidationElement_IsConstructed_OnlyByTheSnapshot()
        {
            var hits = Hits(ConstructionPattern, n => n == Builder);

            Assert.IsEmpty(hits,
                "ValidationElement — единственная дверь, через которую роли входят в ядро; "
                + "собранный мимо " + Builder + " снимок несёт роль, которую никто не решал. "
                + "Найдено:\n" + string.Join("\n", hits));
        }

        /// <summary>Грепу по несуществующему пути нечего найти — он зеленеет,
        /// ничего не проверив. Проверяем, что скан видит и подопечных, и того
        /// единственного, кому можно.</summary>
        [Test]
        public void TheScan_ActuallySeesTheLayerAndItsOneBuilder()
        {
            var names = new List<string>();
            foreach (var f in SourceFiles()) names.Add(Path.GetFileName(f) ?? string.Empty);

            CollectionAssert.Contains(names, Builder, "скан обязан видеть сам источник ролей");
            CollectionAssert.Contains(names, "ConstraintValidator.cs",
                "адаптер сцены обязан быть в скане: он рядом с источником и первый кандидат "
                + "начать собирать роли сам");
            CollectionAssert.Contains(names, "ContextMenuUI.cs",
                "скан обязан покрывать и слой UI, а не один каталог Validation");
            Assert.IsFalse(MayMentionIt("ConstraintValidator.cs"),
                "адаптеру сцены называть ElementKind не разрешено");
        }

        [Test]
        public void TheScan_RecognisesTheShapeOfBuildingAKind()
        {
            Assert.IsTrue(Regex.IsMatch("            var kind = ElementKind.None;", AssignmentPattern),
                "присваивание роли — это и есть её сборка");
            Assert.IsTrue(Regex.IsMatch("            if (isFloor) kind |= ElementKind.FloorAnchor;", OrAssignmentPattern),
                "накопление флагов через |= — та же сборка");
            Assert.IsFalse(Regex.IsMatch("            if (kind == ElementKind.None) return;", AssignmentPattern),
                "сравнение ролью не собирает — иначе сторож запретил бы чтение");
            Assert.IsFalse(Regex.IsMatch("        public bool Is(ElementKind kind) => (Kind & kind) != 0;", AssignmentPattern),
                "чтение флагов обязано остаться разрешённым потребителю ядра");
        }

        /// <summary>Список не должен переживать свои файлы: запись без файла молча
        /// освободит следующий файл, занявший это имя.</summary>
        [Test]
        public void EveryMentioningFile_ExplainsWhy_AndStillExists()
        {
            var names = new HashSet<string>();
            foreach (var f in SourceFiles()) names.Add(Path.GetFileName(f) ?? string.Empty);

            foreach (var (file, why) in MayMention)
            {
                Assert.IsNotEmpty(why,
                    "разрешение без причины через полгода не отличить от недосмотра: " + file);
                Assert.IsTrue(names.Contains(file),
                    "в списке разрешённых числится несуществующий " + file);
            }
        }
    }
}
