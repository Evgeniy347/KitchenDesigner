using System;
using System.IO;

namespace KitchenDesigner.Core
{
    public static class ProjectFileExtension
    {
        public const string Current = ".kdproj";
        public const string Legacy = ".json";

        public static bool IsSupported(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return string.Equals(ext, Current, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, Legacy, StringComparison.OrdinalIgnoreCase);
        }

        public static string WithDefaultExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return IsSupported(path) ? path : path + Current;
        }
    }
}
