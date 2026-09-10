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
            ("PreviewAndHighlightRuleTests.cs",
                "тоже сторож: он ЧИТАЕТ исходники накладки и превью и ищет в них строку "
                + "«new GameObject(» — движка не касается, имя лежит в нём как образец поиска"),
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

        /// <summary>Тип общей изменяемой коллекции, из которой обычно строят
        /// скрэтч-буфер обхода. Список нарочно короткий: восемь существующих
        /// буферов покрывают ровно эти пять типов.</summary>
        private static readonly string[] MutableCollectionKinds =
            { "List", "Dictionary", "HashSet", "Stack", "Queue" };

        private static readonly Regex StaticMutableFieldPattern = new Regex(
            @"(private|internal)\s+static\s+(readonly\s+)?("
            + string.Join("|", MutableCollectionKinds) + @")<.*>\??\s+(_?\w+)\s*(;|=(?!>))");

        /// <summary>Поля, которым названный общий статик разрешён, с причиной.
        /// <see cref="MutableStaticAllowList_NamesOnlyFieldsThatExist"/> проверяет, что
        /// каждая запись всё ещё указывает на существующее поле — иначе запись переживёт
        /// поле и начнёт освобождать следующее с тем же именем в том же файле.</summary>
        private static readonly (string file, string field, string why)[] AllowedMutableStatics =
        {
            ("EventBus.cs", "_events",
                "настоящий разделяемый реестр подписок процесса, а не буфер обхода одного "
                + "вызова: живёт под lock и обязан быть общим для всех потоков, а не отдельным "
                + "на каждый"),
        };

        /// <summary>Одна строка исходника → 0 или 1 нарушение. Вынесено из
        /// <see cref="MutableStaticViolations"/> отдельной функцией, чтобы тест ниже мог
        /// подать её синтетическую строку и доказать, что скан вообще что-то ловит —
        /// без того, чтобы держать нарушение в реальном исходнике.</summary>
        private static IEnumerable<string> LineViolations(string fileName, int lineNumber, string line)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("///")
                || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) yield break;

            if (line.Contains("[ThreadStatic]")) yield break;

            var match = StaticMutableFieldPattern.Match(line);
            if (!match.Success) yield break;

            string field = match.Groups[4].Value;
            if (Array.Exists(AllowedMutableStatics, a => a.file == fileName && a.field == field))
                yield break;

            yield return fileName + ":" + lineNumber + " — " + trimmed;
        }

        private static List<string> MutableStaticViolations(IEnumerable<string> files)
        {
            var violations = new List<string>();
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    violations.AddRange(LineViolations(Path.GetFileName(file), i + 1, lines[i]));
            }
            return violations;
        }

        /// <summary>Сторож девятого общего статика. `[assembly: Parallelizable]` гоняет
        /// EditMode на нескольких потоках сразу, и статическое изменяемое поле без
        /// `[ThreadStatic]` в этот момент разделяется между ними — восемь буферов обхода
        /// (`ContactShadow._besidePerThread`, `ValidationBroadPhase._gridPerThread` и
        /// соседи, `ValidationCore._overlappingPerThread` и соседи) уже расшиты именно
        /// поэтому, и без расшивки давали плавающие красные, которые пропадали при
        /// одиночном запуске — самый дорогой вид флака.</summary>
        [Test]
        public void ScratchBufferSources_UseThreadStaticOrAreExplained()
        {
            var files = ScannedFiles(GeometrySourceDir())
                .Concat(ScannedFiles(PureSourceDir())).ToList();
            Assert.IsNotEmpty(files, "источников ядра и чистого слоя не найдено — тест бесполезен");

            var violations = MutableStaticViolations(files);
            Assert.IsEmpty(violations,
                "ПРАВИЛО: `[assembly: Parallelizable]` запускает тесты на нескольких потоках "
                + "одновременно; статическое поле изменяемой коллекции (List/Dictionary/HashSet/"
                + "Stack/Queue) без [ThreadStatic] в этот момент читается и пишется из разных "
                + "потоков одновременно, и итог зависит от того, кто первым успел — плавающие "
                + "красные то есть, то нет. ЧТО СЛОМАНО: поле ниже — девятый такой общий статик, "
                + "новый и незащищённый. ЧТО СДЕЛАТЬ: если это буфер обхода одного вызова — "
                + "пометить [ThreadStatic], как остальные восемь. Если это ДЕЙСТВИТЕЛЬНО общий "
                + "на весь процесс реестр (а не буфер) — завести под lock, как "
                + "EventBus._events, и добавить в AllowedMutableStatics с причиной. Найдено:\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void MutableStaticAllowList_NamesOnlyFieldsThatExist()
        {
            var sources = ScannedFiles(GeometrySourceDir()).Concat(ScannedFiles(PureSourceDir()));
            var byFileName = sources.ToLookup(Path.GetFileName);

            foreach (var (file, field, why) in AllowedMutableStatics)
            {
                Assert.IsNotEmpty(why, "у записи " + file + "." + field + " нет причины");

                var matches = byFileName[file].ToList();
                Assert.IsNotEmpty(matches,
                    "белый список освобождает " + file + "." + field + " (" + why + "), но такого "
                    + "файла больше нет — запись переживёт файл и начнёт освобождать следующий "
                    + "с этим именем");

                bool fieldStillThere = matches.Any(f => File.ReadAllLines(f).Any(l => l.Contains(field)));
                Assert.IsTrue(fieldStillThere,
                    "белый список освобождает поле " + field + " в " + file + " (" + why + "), но "
                    + "такого поля в файле больше нет — запись начнёт молча освобождать следующее "
                    + "поле с этим именем");
            }
        }

        [Test]
        public void ScratchBufferScan_ActuallyFindsTheKnownThreadStaticFields()
        {
            var names = ScannedFileNames(GeometrySourceDir());
            CollectionAssert.Contains(names, "ValidationCore.cs",
                "скан обязан видеть файлы с реальными [ThreadStatic]-буферами — иначе он "
                + "проверяет пустоту и молча зеленеет");
            CollectionAssert.Contains(names, "ContactShadow.cs");
            CollectionAssert.Contains(names, "ValidationBroadPhase.cs");
        }

        /// <summary>«Подсади статик и покажи, что видел красным» — в отличие от
        /// временной правки реального исходника, эта проверка подсаживает нарушение
        /// синтетической строкой на каждом прогоне: сравнение красноты не нужно делать
        /// руками один раз, оно доказано механически и навсегда.</summary>
        [Test]
        public void MutableStaticScan_FlagsAPlantedStaticField()
        {
            var violations = LineViolations("PlantedFixture.cs", 1,
                "        private static List<int> _plantedScratchBuffer;").ToList();

            Assert.AreEqual(1, violations.Count,
                "сканер обязан ловить голый статический List без [ThreadStatic] и без записи "
                + "в белом списке — если это не так, девятый общий статик проскочит точно так "
                + "же, как проскочил бы восьмой, не будь он расшит");
        }

        [Test]
        public void MutableStaticScan_DoesNotFlagThreadStaticOrAllowListedFields()
        {
            var threadStatic = LineViolations("Whatever.cs", 1,
                "        [ThreadStatic] private static List<int> _scratchPerThread;").ToList();
            Assert.IsEmpty(threadStatic,
                "поле с [ThreadStatic] — это ровно то, что сканер обязан пропускать; если он "
                + "ловит и его, сторож будет краснеть на каждом уже расшитом буфере");

            var allowListed = LineViolations("EventBus.cs", 1,
                "        private static readonly Dictionary<Type, Delegate> _events = "
                + "new Dictionary<Type, Delegate>();").ToList();
            Assert.IsEmpty(allowListed,
                "EventBus._events — записанное в белом списке исключение; если сканер его "
                + "тоже ловит, белый список не работает и AllowedMutableStatics бесполезен");
        }
    }
}
