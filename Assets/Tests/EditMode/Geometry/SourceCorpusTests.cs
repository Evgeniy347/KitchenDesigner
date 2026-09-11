using System.IO;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож самого кэша исходников. Кэш, который на второй вопрос лезет
    /// на диск, не экономит ничего, а кэш, отдающий свой внутренний массив наружу,
    /// даёт соседнему сторожу испортить ответ третьему — оба отказа молчаливые,
    /// поэтому проверяются здесь явно.</summary>
    public class SourceCorpusTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void CreateScratchDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), "kd-source-corpus-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void RemoveScratchDir()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void Lines_RepeatTheFileOnDisk_LineForLine()
        {
            var file = Path.Combine(_dir, "Sample.cs");
            File.WriteAllLines(file, new[] { "class A", "{", "}" });

            CollectionAssert.AreEqual(File.ReadAllLines(file), SourceCorpus.Lines(file),
                "кэш обязан отдавать ровно то, что лежит в файле");
        }

        [Test]
        public void TheSecondAnswer_ComesFromTheCache_NotFromTheDisk()
        {
            var file = Path.Combine(_dir, "Changing.cs");
            File.WriteAllLines(file, new[] { "первое чтение" });
            var first = SourceCorpus.Lines(file);

            File.WriteAllLines(file, new[] { "файл переписан" });

            CollectionAssert.AreEqual(first, SourceCorpus.Lines(file),
                "второй ответ пришёл с диска — значит корпус читается заново на каждый тест, "
                + "ради чего кэша и не было");
        }

        [Test]
        public void Text_KeepsTheFileVerbatim_LineEndingsIncluded()
        {
            var file = Path.Combine(_dir, "Verbatim.cs");
            File.WriteAllText(file, "class A\r\n{\r\n}\r\n");

            Assert.AreEqual("class A\r\n{\r\n}\r\n", SourceCorpus.Text(file),
                "текст обязан приходить таким же, как с диска: сторожа гоняют по нему "
                + "многострочные выражения, и подменённый перевод строки тихо меняет ответ");
        }

        [Test]
        public void TheSecondTextAnswer_ComesFromTheCache_NotFromTheDisk()
        {
            var file = Path.Combine(_dir, "ChangingText.cs");
            File.WriteAllText(file, "первое чтение");
            var first = SourceCorpus.Text(file);

            File.WriteAllText(file, "файл переписан");

            Assert.AreEqual(first, SourceCorpus.Text(file),
                "второй ответ пришёл с диска — кэш текста не работает");
        }

        [Test]
        public void TheCachedLines_SurviveACallerThatOverwritesWhatItGot()
        {
            var file = Path.Combine(_dir, "Mutated.cs");
            File.WriteAllLines(file, new[] { "class A" });

            SourceCorpus.Lines(file)[0] = "испорчено соседом";

            Assert.AreEqual("class A", SourceCorpus.Lines(file)[0],
                "кэш отдал свой внутренний массив: правка одного сторожа видна другому");
        }

        [Test]
        public void TheCachedFileList_SurvivesACallerThatOverwritesWhatItGot()
        {
            File.WriteAllText(Path.Combine(_dir, "Only.cs"), "class A {}");

            SourceCorpus.Files(_dir)[0] = "испорчено соседом";

            Assert.AreEqual("Only.cs", Path.GetFileName(SourceCorpus.Files(_dir)[0]),
                "список файлов отдан наружу без копии");
        }
    }
}
