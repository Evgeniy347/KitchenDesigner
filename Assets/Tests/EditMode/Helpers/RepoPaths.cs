using System;
using System.IO;
using System.Linq;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Поиск каталога репозитория для тестов, которые читают ИСХОДНИКИ, а не
    /// собранный код. Точка отсчёта у двух раннеров разная: под dotnet test
    /// AppContext.BaseDirectory лежит внутри проекта, а в Unity это каталог УСТАНОВКИ
    /// редактора; зато тестовая сборка Unity лежит в Library/ScriptAssemblies проекта,
    /// откуда подъём вверх приводит куда нужно. Application.dataPath не годится: он сам
    /// extern и под dotnet недоступен.</summary>
    public static class RepoPaths
    {
        public static string Subdir(params string[] parts)
        {
            var roots = new[]
            {
                Path.GetDirectoryName(typeof(RepoPaths).Assembly.Location),
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
            };

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;

                var dir = new DirectoryInfo(root);
                while (dir != null)
                {
                    var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }

            throw new DirectoryNotFoundException(
                "Не найден " + string.Join("/", parts) + " ни от одной из точек: "
                + string.Join(", ", roots));
        }
    }
}
