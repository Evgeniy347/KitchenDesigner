using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож правила «никакого <c>new</c> над членом базового класса»
    /// (CONVENTIONS.md → «<c>public new</c> over a non-virtual base member is the
    /// same trap, with a keyword»).
    ///
    /// <c>new</c> не заменяет базовый член, а ПРЯЧЕТ его — и только для тех, кто
    /// держит объект как подкласс. Все остальные читают базовый, и у объекта
    /// становится ДВА независимых значения одного понятия. Так
    /// <c>TableElement.MaterialId</c> дал три видимых пользователю бага сразу:
    /// применялся не тот цвет, правка ножек перекрашивала столешницу, декор
    /// пропадал при снятии выделения. Слой рендера держит элементы как
    /// <c>KitchenElement</c> и читал базовое поле, которое никто не обновлял.
    ///
    /// Починка (a286371c) убрала оба нарушения, поэтому сторож стартует с нуля:
    /// любое новое срабатывание — новый дефект, а не наследие.
    ///
    /// Скан идёт по СТРОКАМ исходников: анализатор здесь не нужен, а грепа
    /// достаточно, потому что модификатор доступа и <c>new</c> стоят рядом.</summary>
    public class NewOverBaseMemberTests
    {
        /// <summary>Каталоги продакшн-исходников, где правило действует.</summary>
        private static readonly string[][] ScannedDirs =
        {
            new[] { "Assets", "Scripts" },
            new[] { "Assets", "Editor" },
        };

        /// <summary>Формы объявления, прячущего базовый член. Порядок модификаторов
        /// в C# свободен, поэтому ловим <c>new</c> и справа, и слева от них.</summary>
        private static readonly (string pattern, string why)[] Banned =
        {
            (@"\b(public|protected|internal|private|static|readonly|virtual|abstract|sealed|unsafe)\s+new\s+(?!\()",
                "new прячет базовый член, а не заменяет его: держащие объект как базовый тип "
                + "продолжат читать базовое значение"),
            (@"\bnew\s+(public|protected|internal|private|static|readonly|virtual|abstract|sealed)\b",
                "тот же new, модификаторы в другом порядке"),
        };

        /// <summary>Послабления. Пусто — и должно оставаться пустым: если базовый
        /// член мешает, его делают <c>virtual</c> и переопределяют, а невозможность
        /// этого и есть задача, которую надо решить, а не обойти. Причина у записи
        /// ОБЯЗАТЕЛЬНА, и её файл обязан существовать — иначе запись переживёт свой
        /// файл и начнёт молча освобождать следующий с тем же именем.</summary>
        private static readonly (string file, string why)[] Allowed =
            Array.Empty<(string, string)>();

        private static IEnumerable<string> SourceFiles()
        {
            foreach (var parts in ScannedDirs)
            {
                string dir;
                try { dir = RepoPaths.Subdir(parts); }
                catch (DirectoryNotFoundException) { continue; }

                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    yield return f;
            }
        }

        private static List<string> ScannedFileNames()
        {
            var names = new List<string>();
            foreach (var f in SourceFiles()) names.Add(Path.GetFileName(f) ?? string.Empty);
            return names;
        }

        private static bool IsAllowed(string fileName)
        {
            foreach (var (file, _) in Allowed)
                if (string.Equals(file, fileName, StringComparison.Ordinal)) return true;
            return false;
        }

        [Test]
        public void ProductionSources_NeverHideABaseMemberWithNew()
        {
            var violations = new List<string>();
            int scanned = 0;

            foreach (var file in SourceFiles())
            {
                scanned++;
                var name = Path.GetFileName(file) ?? string.Empty;
                if (IsAllowed(name)) continue;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                        continue;

                    foreach (var (pattern, why) in Banned)
                        if (Regex.IsMatch(lines[i], pattern))
                        {
                            violations.Add(name + ":" + (i + 1) + " — " + why + "\n    " + trimmed);
                            break;
                        }
                }
            }

            Assert.Greater(scanned, 100, "скан не увидел исходников — тест бесполезен");
            Assert.IsEmpty(violations,
                "new над членом базового класса даёт объекту ДВА значения одного понятия: "
                + "подкласс пишет своё, а весь код, держащий объект как базовый тип, читает "
                + "базовое. Сделайте базовый член virtual и переопределите его. Найдено:\n"
                + string.Join("\n", violations));
        }

        /// <summary>Грепу по несуществующему пути нечего найти — он зеленеет,
        /// ничего не проверив. Файл исторического нарушения обязан быть в скане.</summary>
        [Test]
        public void TheScan_ActuallySeesTheFileThatCarriedTheDefect()
        {
            var names = ScannedFileNames();

            CollectionAssert.Contains(names, "TableElement.cs",
                "именно TableElement объявлял public new string MaterialId — он обязан быть в скане");
            CollectionAssert.Contains(names, "KitchenElement.cs");
            Assert.IsFalse(IsAllowed("TableElement.cs"),
                "исторический нарушитель не имеет права быть в списке послаблений");
        }

        [Test]
        public void TheScan_RecognisesTheShapeOfTheDefect()
        {
            var samples = new[]
            {
                "        public new string MaterialId => _materialId;",
                "        protected new int Count;",
                "        new public string Name;",
            };

            foreach (var line in samples)
            {
                bool caught = false;
                foreach (var (pattern, _) in Banned)
                    if (Regex.IsMatch(line, pattern)) { caught = true; break; }

                Assert.IsTrue(caught, "сторож обязан видеть объявление «" + line.Trim() + "»");
            }

            foreach (var (pattern, _) in Banned)
            {
                Assert.IsFalse(Regex.IsMatch("            var mesh = new Mesh();", pattern),
                    "обычное создание объекта не является сокрытием базового члена");
                Assert.IsFalse(Regex.IsMatch("        public static T Make<T>() where T : new() => new T();", pattern),
                    "ограничение where T : new() не является сокрытием базового члена");
            }
        }

        [Test]
        public void EveryAllowedFile_ExplainsWhy_AndStillExists()
        {
            var names = new HashSet<string>(ScannedFileNames());

            foreach (var (file, why) in Allowed)
            {
                Assert.IsNotEmpty(why,
                    "послабление без причины через полгода не отличить от недосмотра: " + file);
                Assert.IsTrue(names.Contains(file),
                    "в списке послаблений числится несуществующий файл " + file
                    + " — запись переживёт свой файл и начнёт освобождать следующий с этим именем");
            }
        }
    }
}
