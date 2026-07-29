using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож границы ядра. KitchenDesigner.Geometry собирается ДВАЖДЫ:
    /// Unity по asmdef и dotnet по geometry/Geometry.csproj. Unity проглотит
    /// любой вызов движка, а под CoreCLR он упадёт в РАНТАЙМЕ на
    /// «SecurityException: ECall methods must be packaged into a system module».
    /// Ловим на этапе тестов, а не в ночном прогоне Stryker.
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
        };

        /// <summary>Каталог исходников ядра. Ищем вверх от нескольких точек, потому
        /// что у двух раннеров они разные: под dotnet test AppContext.BaseDirectory
        /// лежит внутри проекта, а в Unity это каталог УСТАНОВКИ редактора. Зато
        /// сама тестовая сборка в Unity лежит в Library/ScriptAssemblies проекта,
        /// откуда подъём вверх приводит куда нужно.</summary>
        private static string GeometrySourceDir()
        {
            var roots = new[]
            {
                Path.GetDirectoryName(typeof(GeometryArchitectureTests).Assembly.Location),
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
            };

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;

                var dir = new DirectoryInfo(root);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Scripts", "Core", "Geometry");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }

            throw new DirectoryNotFoundException(
                "Не найден Assets/Scripts/Core/Geometry ни от одной из точек: "
                + string.Join(", ", roots));
        }

        [Test]
        public void CoreSources_DoNotTouchTheEngine()
        {
            var dir = GeometrySourceDir();
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, $"в {dir} нет исходников — тест бесполезен");

            var violations = new List<string>();

            foreach (var file in files)
            {
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
                                $"{Path.GetFileName(file)}:{i + 1} — {pattern.Trim('\\', 'b')} ({why})\n    {trimmed}");
                }
            }

            Assert.IsEmpty(violations,
                "Ядро обязано исполняться вне Unity. Найдено:\n" + string.Join("\n", violations));
        }

        [Test]
        public void CoreSources_AreActuallyPresent()
        {
            var files = Directory.GetFiles(GeometrySourceDir(), "*.cs", SearchOption.AllDirectories);
            CollectionAssert.Contains(
                Array.ConvertAll(files, Path.GetFileName), "Tolerance.cs");
        }
    }
}
