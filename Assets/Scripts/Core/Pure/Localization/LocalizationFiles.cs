using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class LocalizationFiles
    {
        public const string FolderName = "Localization";
        public const string ContextFileName = "_context.json";

        private static readonly string[] RelativeToProject = { "Assets", "StreamingAssets", FolderName };

        public static string? Directory { get; set; }

        public static string? Resolve()
        {
            if (!string.IsNullOrEmpty(Directory) && System.IO.Directory.Exists(Directory)) return Directory;
            foreach (var start in SearchRoots())
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(new[] { dir.FullName }.Concat(RelativeToProject).ToArray());
                    if (System.IO.Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            return null;
        }

        public static bool IsLanguageFile(string fileName) =>
            fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("_", StringComparison.Ordinal);

        public static IReadOnlyList<StringTable> LoadAll(string directory) =>
            System.IO.Directory.GetFiles(directory, "*.json")
                .Where(path => IsLanguageFile(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => StringTable.Parse(
                    Path.GetFileNameWithoutExtension(path),
                    File.ReadAllText(path, Encoding.UTF8)))
                .ToList();

        public static Localizer Load(string language)
        {
            var directory = Resolve();
            return directory == null
                ? Localizer.Empty
                : new Localizer(LoadAll(directory), language);
        }

        private static IEnumerable<string> SearchRoots()
        {
            var roots = new[]
            {
                SafeCurrentDirectory(),
                AppContext.BaseDirectory,
                Path.GetDirectoryName(typeof(LocalizationFiles).Assembly.Location),
            };
            return roots.Where(r => !string.IsNullOrEmpty(r)).Select(r => r!);
        }

        private static string? SafeCurrentDirectory()
        {
            try { return System.IO.Directory.GetCurrentDirectory(); }
            catch (Exception) { return null; }
        }
    }
}
