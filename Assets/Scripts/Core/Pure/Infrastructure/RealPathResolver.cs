using System;
using System.Collections.Generic;
using System.IO;

namespace KitchenDesigner.Core
{
    internal static class RealPathResolver
    {
        public static string Resolve(string path)
        {
            var full = Path.GetFullPath(path);
            var (existingPrefix, remainder) = SplitAtExistingPrefix(full);
            var resolvedPrefix = Win32RealPath.TryGetFinalPath(existingPrefix);
            if (resolvedPrefix == null)
                throw new IOException("could not resolve the real path of '" + existingPrefix + "'");
            var combined = remainder.Length == 0
                ? resolvedPrefix
                : Path.GetFullPath(Path.Combine(resolvedPrefix, remainder));
            return Normalize(combined);
        }

        private static (string existingPrefix, string remainder) SplitAtExistingPrefix(string full)
        {
            var current = full;
            var remainderSegments = new List<string>();

            while (!Directory.Exists(current) && !File.Exists(current))
            {
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                    return (current, string.Empty);

                remainderSegments.Insert(0, Path.GetFileName(current));
                current = parent;
            }

            var remainder = remainderSegments.Count == 0 ? string.Empty : string.Join("\\", remainderSegments);
            return (current, remainder);
        }

        private static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');
    }
}
