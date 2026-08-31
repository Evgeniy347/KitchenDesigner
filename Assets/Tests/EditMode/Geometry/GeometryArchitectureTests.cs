using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож границы ядра. KitchenDesigner.Geometry собирается ДВАЖДЫ:
    /// Unity по asmdef и dotnet по geometry/core/Geometry.csproj. Unity проглотит
    /// любой вызов движка, а под CoreCLR он упадёт в РАНТАЙМЕ на
    /// «SecurityException: ECall methods must be packaged into a system module».
    /// Ловим на этапе тестов, а не в ночном прогоне Stryker.
    ///
    /// Тесты ядра (Assets/Tests/EditMode/Geometry) собираются той же второй
    /// сборкой — geometry/tests/Geometry.Tests.csproj глобит этот каталог целиком.
    /// Поэтому запрет на сцену действует и там, и проверяется тем же списком.
    ///
    /// Путь к исходникам ищем обходом вверх от каталога сборки: Application.dataPath
    /// сам по себе extern и под dotnet недоступен.</summary>
    public class GeometryArchitectureTests
    {
        private static readonly (string pattern, string why)[] Banned =
        {
            (@"\bMonoBehaviour\b",          "сцена: ядро работает со снимками геометрии"),
            (@"\bGameObject\b",             "сцена"),
            (@"\bGetComponent\s*<",         "сцена"),
            (@"\btransform\s*\.",           "ядро не читает и не пишет трансформ"),
            (@"\bInstantiate\s*\(",         "сцена"),
            (@"\bDestroyImmediate\s*\(",    "сцена"),
            (@"\bDebug\s*\.",               "логгер движка: ядро возвращает данные, а не пишет в консоль"),
            (@"Quaternion\s*\.\s*Euler",       "ECall: конструирование поворота недоступно под CoreCLR"),
            (@"Quaternion\s*\.\s*AngleAxis",   "ECall"),
            (@"Quaternion\s*\.\s*LookRotation","ECall"),
            (@"Quaternion\s*\.\s*Inverse",     "ECall"),
            (@"\bMatrix4x4\b",              "ECall"),
            (@"\bJsonUtility\b",            "лежит в UnityEngine.JSONSerializeModule и вызывает ECall: под dotnet не собирается вовсе"),
        };

        /// <summary>Файлы, которым НАЗЫВАТЬ запрещённые символы можно, с причиной.
        /// Причина обязательна: без неё через полгода не отличить осознанное
        /// исключение от недосмотра. Существование каждого файла проверяет
        /// <see cref="AllowList_NamesOnlyFilesThatExist"/> — иначе запись переживёт
        /// свой файл и начнёт молча освобождать следующий, занявший это имя.</summary>
        private static readonly (string file, string why)[] Allowed =
        {
            ("GeometryArchitectureTests.cs",
                "сам сторож: запрещённые символы лежат в его таблице как строки-шаблоны"),
        };
        private static string RepoSubdir(params string[] parts) => RepoPaths.Subdir(parts);

        /// <summary>Каталог исходников ядра. Ищем вверх от нескольких точек, потому
        /// что у двух раннеров они разные: под dotnet test AppContext.BaseDirectory
        /// лежит внутри проекта, а в Unity это каталог УСТАНОВКИ редактора. Зато
        /// сама тестовая сборка в Unity лежит в Library/ScriptAssemblies проекта,
        /// откуда подъём вверх приводит куда нужно.</summary>
        private static string GeometrySourceDir() =>
            RepoSubdir("Assets", "Scripts", "Core", "Geometry");

        private static string GeometryTestSourceDir() =>
            RepoSubdir("Assets", "Tests", "EditMode", "Geometry");

        private static string PureSourceDir() =>
            RepoSubdir("Assets", "Scripts", "Core", "Pure");

        private static string PureTestSourceDir() =>
            RepoSubdir("Assets", "Tests", "EditMode", "Pure");

        private static string[] ScannedFiles(string dir) =>
            Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);

        private static string[] ScannedFileNames(string dir) =>
            Array.ConvertAll(ScannedFiles(dir), f => Path.GetFileName(f) ?? string.Empty);

        private static List<string> EngineReferences(IEnumerable<string> files)
        {
            var violations = new List<string>();

            foreach (var file in files)
            {
                if (Array.Exists(Allowed, a => a.file == Path.GetFileName(file))) continue;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    // Комментарии не код: в них запрещённые имена законны —
                    // ими объясняют, ПОЧЕМУ ядро их не использует.
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("///")
                        || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

                    foreach (var (pattern, why) in Banned)
                        if (Regex.IsMatch(line, pattern))
                            violations.Add(
                                Path.GetFileName(file) + ":" + (i + 1) + " — "
                                + pattern.Trim('\\', 'b') + " (" + why + ")\n    " + trimmed);
                }
            }

            return violations;
        }

        [Test]
        public void CoreSources_DoNotTouchTheEngine()
        {
            var dir = GeometrySourceDir();
            var files = ScannedFiles(dir);
            Assert.IsNotEmpty(files, "в " + dir + " нет исходников — тест бесполезен");

            var violations = EngineReferences(files);
            Assert.IsEmpty(violations,
                "Ядро обязано исполняться вне Unity. Найдено:\n" + string.Join("\n", violations));
        }

        [Test]
        public void CoreTestSources_DoNotTouchTheEngine()
        {
            var dir = GeometryTestSourceDir();
            var files = ScannedFiles(dir);
            Assert.IsNotEmpty(files, "в " + dir + " нет тестов — тест бесполезен");

            var violations = EngineReferences(files);
            Assert.IsEmpty(violations,
                "geometry/tests/Geometry.Tests.csproj глобит этот каталог целиком: сценовый тест "
                + "здесь ломает СБОРКУ второго прогона, и dotnet test со Stryker умирают на CS0246, "
                + "оставаясь зелёными для Unity. Такому тесту место в Assets/Tests/EditMode. Найдено:\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void PureSources_DoNotTouchTheEngine()
        {
            var dir = PureSourceDir();
            var files = ScannedFiles(dir);
            Assert.IsNotEmpty(files, "в " + dir + " нет исходников — тест бесполезен");

            var violations = EngineReferences(files);
            Assert.IsEmpty(violations,
                "Assets/Scripts/Core/Pure собирается вторым проходом (geometry/pure/Pure.csproj) и "
                + "обязан исполняться вне Unity. Классу, которому нужен движок, здесь не место — "
                + "он живёт в своём слое Core. Найдено:\n" + string.Join("\n", violations));
        }

        [Test]
        public void PureTestSources_DoNotTouchTheEngine()
        {
            var dir = PureTestSourceDir();
            var files = ScannedFiles(dir);
            Assert.IsNotEmpty(files, "в " + dir + " нет тестов — тест бесполезен");

            var violations = EngineReferences(files);
            Assert.IsEmpty(violations,
                "geometry/pure-tests/Pure.Tests.csproj глобит этот каталог целиком: сценовый тест "
                + "здесь ломает быструю сборку, оставаясь зелёным для Unity. Такому тесту место "
                + "в Assets/Tests/EditMode. Найдено:\n" + string.Join("\n", violations));
        }

        [Test]
        public void PureSources_AreActuallyScanned()
        {
            CollectionAssert.Contains(ScannedFileNames(PureSourceDir()), "ExpressionParser.cs",
                "скан чистого слоя должен видеть его файлы — грепу по несуществующему пути "
                + "нечего найти, и он зеленеет, ничего не проверив");
            CollectionAssert.Contains(ScannedFileNames(PureTestSourceDir()), "ExpressionParserTests.cs");
        }

        [Test]
        public void CoreSources_AreActuallyPresent()
        {
            CollectionAssert.Contains(ScannedFileNames(GeometrySourceDir()), "Tolerance.cs");
        }

        [Test]
        public void CoreTestSources_AreActuallyScanned_AndNotBlanketExempted()
        {
            var names = ScannedFileNames(GeometryTestSourceDir());
            CollectionAssert.Contains(names, "SnapCoreTestBase.cs",
                "скан каталога тестов ядра должен видеть его файлы — грепу по несуществующему "
                + "пути нечего найти, и он зеленеет, ничего не проверив");
            CollectionAssert.DoesNotContain(Array.ConvertAll(Allowed, a => a.file), "SnapCoreTestBase.cs",
                "белый список не вправе освобождать обычный тест ядра");
        }

        [Test]
        public void AllowList_NamesOnlyFilesThatExist()
        {
            var names = new HashSet<string>(ScannedFileNames(GeometrySourceDir()));
            names.UnionWith(ScannedFileNames(GeometryTestSourceDir()));

            foreach (var (file, why) in Allowed)
            {
                Assert.IsTrue(names.Contains(file),
                    "белый список освобождает " + file + " (" + why + "), но такого файла больше "
                    + "нет — запись переживёт свой файл и начнёт освобождать следующий с этим именем");
                Assert.IsNotEmpty(why, "у записи " + file + " нет причины");
            }
        }
    }
}
