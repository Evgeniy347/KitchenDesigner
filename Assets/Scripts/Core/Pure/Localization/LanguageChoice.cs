using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core
{
    public static class LanguageChoice
    {
        public static string Decide(string? stored, IEnumerable<string?> systemLanguages,
            IReadOnlyCollection<string> available, bool testRun)
        {
            if (testRun) return Localizer.SourceLanguage;

            var storedCode = Match(stored, available);
            if (storedCode != null) return storedCode;

            foreach (var system in systemLanguages)
            {
                var code = Match(system, available);
                if (code != null) return code;
            }

            return available.Contains(Localizer.FallbackLanguage)
                ? Localizer.FallbackLanguage
                : Localizer.SourceLanguage;
        }

        public static string? Match(string? language, IReadOnlyCollection<string> available)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            var exact = language!.Trim().Replace('_', '-');
            foreach (var code in available)
                if (string.Equals(code, exact, StringComparison.OrdinalIgnoreCase)) return code;

            var primary = Normalize(language);
            if (primary == null) return null;
            if (available.Contains(primary)) return primary;
            return available
                .Where(code => Normalize(code) == primary)
                .OrderBy(code => code, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public static string? Normalize(string? language)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            var trimmed = language!.Trim();
            int cut = trimmed.IndexOfAny(new[] { '-', '_' });
            var primary = cut < 0 ? trimmed : trimmed.Substring(0, cut);
            return primary.Length == 0 ? null : primary.ToLowerInvariant();
        }
    }
}
