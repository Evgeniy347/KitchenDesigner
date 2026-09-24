using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class RecentProjectsList
    {
        public const int Capacity = 10;

        public static string[] WithPromoted(string[]? existing, string? path, int capacity = Capacity)
        {
            if (string.IsNullOrEmpty(path)) return Copy(existing);
            if (capacity <= 0) return Array.Empty<string>();

            var result = new List<string>(capacity) { path! };
            if (existing != null)
                foreach (var p in existing)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    if (string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) continue;
                    if (result.Count >= capacity) break;
                    result.Add(p);
                }
            return result.ToArray();
        }

        private static string[] Copy(string[]? existing) =>
            existing == null ? Array.Empty<string>() : (string[])existing.Clone();
    }
}
