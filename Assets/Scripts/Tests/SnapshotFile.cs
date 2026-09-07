using System.IO;
using System.Text;

namespace KitchenDesigner.Tests
{
    public static class SnapshotFile
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public const string TrailingNewline = "\n";

        public static void Write(string path, string normalizedJson)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, normalizedJson + TrailingNewline, Utf8NoBom);
        }
    }
}
